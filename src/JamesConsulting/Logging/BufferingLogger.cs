using System;
using System.Threading;
using JamesConsulting.Internal;
using Microsoft.Extensions.Logging;

namespace JamesConsulting.Logging;

/// <summary>
/// An <see cref="ILogger" /> decorator that implements the buffering ("dump-on-error") pattern.
/// Low-level records are captured into the active <see cref="LogBufferScope" /> and only emitted if
/// a record at or above the configured flush level occurs, at which point the entire buffer is
/// dumped to the inner logger so you get the diagnostic context leading up to the failure.
/// </summary>
/// <remarks>
/// See <see cref="BufferingLoggerOptions" /> for the routing rules. The decorator forwards
/// <see cref="ILogger.BeginScope{TState}" /> to the inner logger; buffered records do not capture
/// logging-scope state.
/// </remarks>
public sealed class BufferingLogger : ILogger
{
    private readonly ILogger inner;
    private readonly BufferingLoggerOptions options;
    private int filterWarningEmitted;

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferingLogger" /> class.
    /// </summary>
    /// <param name="inner">The inner logger that records are written through to and replayed against.</param>
    /// <param name="options">The buffering configuration. Validated by the constructor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner" /> or <paramref name="options" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The options fail <see cref="BufferingLoggerOptions.Validate" />.</exception>
    public BufferingLogger(ILogger inner, BufferingLoggerOptions options)
    {
        Guard.NotNull(inner);
        Guard.NotNull(options);
        options.Validate();
        this.inner = inner;
        this.options = options;
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => inner.BeginScope(state);

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
    {
        if (logLevel == LogLevel.None)
        {
            return false;
        }

        if (logLevel >= options.PassthroughLevel)
        {
            // Passthrough and flush levels are written live; honour the inner logger's filter, but
            // always allow flush-level records through so the dump can be triggered.
            return inner.IsEnabled(logLevel) || logLevel >= options.FlushLevel;
        }

        if (logLevel >= options.BufferLevel)
        {
            // Buffer-range records are only worth producing when there is somewhere to buffer them.
            var scope = LogBuffer.Current;
            if (scope is null || scope.IsDisposed)
            {
                return false;
            }

            return options.SuspendBufferingAfterFlush && scope.IsFlushed
                ? inner.IsEnabled(logLevel)
                : true;
        }

        return false;
    }

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Guard.NotNull(formatter);

        if (logLevel == LogLevel.None)
        {
            return;
        }

        if (logLevel >= options.FlushLevel)
        {
            // Dump the accrued context first, then write the triggering record. Flush is
            // best-effort and never throws, so the triggering record is always written. The whole
            // live scope chain is dumped (ancestors first) for chronological context; only the
            // innermost scope is suspended.
            var flushScope = LogBuffer.Current;
            flushScope?.FlushForError();

            // Only warn about a filtered dump when a dump was actually attempted (a scope is
            // active). A bare flush-level record outside any buffering scope dumps nothing, so it
            // must not raise the backstop warning or consume the once-only guard.
            if (flushScope is not null)
            {
                WarnIfDumpFiltered();
            }

            inner.Log(logLevel, eventId, state, exception, formatter);
            return;
        }

        if (logLevel >= options.PassthroughLevel)
        {
            inner.Log(logLevel, eventId, state, exception, formatter);
            return;
        }

        if (logLevel < options.BufferLevel)
        {
            return;
        }

        var scope = LogBuffer.Current;
        if (scope is null || scope.IsDisposed)
        {
            // No active scope to buffer into: drop the record rather than emit unscoped noise.
            return;
        }

        if (options.SuspendBufferingAfterFlush && scope.IsFlushed)
        {
            // Fast path: already flushed once, so emit subsequent buffer-range records live without
            // allocating a snapshot. The authoritative, race-free decision is still made under the
            // scope lock in TryEnqueue below for the case where a flush happens concurrently.
            inner.Log(logLevel, eventId, state, exception, formatter);
            return;
        }

        var message = RenderMessage(state, exception, formatter);
        var entry = BufferedLogEntry.Create(inner, logLevel, eventId, state, message, exception);
        if (scope.TryEnqueue(entry, options.SuspendBufferingAfterFlush) == LogBufferEnqueueResult.AlreadyFlushed)
        {
            // A concurrent flush completed between the check above and the enqueue; emit live.
            inner.Log(logLevel, eventId, state, exception, formatter);
        }
    }

    private static string RenderMessage<TState>(
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        // Buffer-range records are rendered eagerly so the message reflects the values at log time
        // (the live logging pipeline would otherwise render lazily at write time). Guard the caller's
        // formatter so a throwing message — for example a structured value whose ToString throws —
        // cannot surface as a logging failure on the caller's hot path.
        try
        {
            return formatter(state, exception);
        }
        catch (Exception ex)
        {
            return $"[buffering logger: message formatter threw {ex.GetType().FullName}]";
        }
    }

    private void WarnIfDumpFiltered()
    {
        // The buffered band is [BufferLevel, PassthroughLevel). When it is empty nothing is ever
        // buffered, so there is no dump to filter.
        if (options.PassthroughLevel <= options.BufferLevel)
        {
            return;
        }

        // Log levels are monotonic for a given logger: if the highest level that can be buffered is
        // disabled, every lower buffered level is disabled too, so no dumped record can reach a
        // sink. If that top level is still enabled, at least part of the dump gets through and the
        // warning would be misleading.
        var highestBufferedLevel = (LogLevel)((int)options.PassthroughLevel - 1);
        if (inner.IsEnabled(highestBufferedLevel))
        {
            return;
        }

        // The inner logger filters out the entire buffered band, so the dump just replayed cannot
        // reach the sinks. Warn once per logger so the misconfiguration is visible without flooding
        // output. This only happens when ConfigureUnderlyingFilter is disabled or a category rule
        // blocks the buffered levels.
        if (Interlocked.Exchange(ref filterWarningEmitted, 1) != 0)
        {
            return;
        }

        var message =
            $"Buffering logger: buffered records below {options.PassthroughLevel} are filtered out by the underlying " +
            $"logging configuration, so the dumped context will not reach any sink. Add a filter rule at " +
            $"{options.BufferLevel} for the buffered categories (for example set Logging:LogLevel:Default to Trace) " +
            "or leave BufferingLoggerOptions.ConfigureUnderlyingFilter enabled.";

        // Emit at the flush level (which just triggered and is written live) when that is higher than
        // Warning, so the backstop warning cannot itself be filtered out by the same misconfiguration
        // it is reporting.
        var warnLevel = options.FlushLevel > LogLevel.Warning ? options.FlushLevel : LogLevel.Warning;
        inner.Log(warnLevel, default, message, null, static (s, _) => s);
    }
}
