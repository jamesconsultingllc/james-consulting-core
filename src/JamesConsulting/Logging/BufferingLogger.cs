using System;
using JamesConsulting.Internal;
using Microsoft.Extensions.Logging;

namespace JamesConsulting.Logging;

/// <summary>
/// An <see cref="ILogger" /> decorator that implements the buffering ("dump-on-error") pattern.
/// Records your host's live logging configuration would already write are written live, unchanged.
/// Records below that live threshold but at or above the configured buffer level are captured into
/// the active <see cref="LogBufferScope" /> and only emitted if a record at or above the configured
/// flush level occurs inside that scope, at which point the entire buffer — and the triggering
/// record — is replayed directly to the registered providers so you get the diagnostic context
/// leading up to the failure.
/// </summary>
/// <remarks>
/// <para>
/// See <see cref="BufferingLoggerOptions" /> for the routing rules. The decorator forwards
/// <see cref="ILogger.BeginScope{TState}" /> to the inner logger; buffered records do not capture
/// logging-scope state.
/// </para>
/// <para>
/// The live boundary is the unmodified inner logger's own <see cref="ILogger.IsEnabled" />, so the
/// host's <c>Logging:LogLevel</c> configuration stays authoritative for live logging (including
/// per-category and per-provider rules and configuration reloads). The error dump, by contrast, is
/// written through a <em>replay target</em> that fans out directly to the registered providers and
/// bypasses those factory-level filters — that is what makes the suppressed low-level context
/// visible on error.
/// </para>
/// <para>
/// One deliberate consequence: <em>inside an active scope</em> the filter bypass applies to the
/// triggering flush-level record too, and <see cref="IsEnabled" /> reports <c>true</c> for
/// flush-level records whenever a scope is active. So an error logged in a category or provider the
/// host configuration has silenced still emits — along with the whole buffered dump — to every
/// registered provider, because the error must reach the same sinks that just received its context.
/// The same flush-level record logged <em>outside</em> a scope honors the host configuration
/// normally. This type is internal; it is always constructed by <see cref="BufferingLoggerFactory" />
/// with a provider-backed replay target.
/// </para>
/// </remarks>
internal sealed class BufferingLogger : ILogger
{
    private readonly ILogger inner;
    private readonly ILogger replayTarget;
    private readonly BufferingLoggerOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferingLogger" /> class.
    /// </summary>
    /// <param name="inner">The inner logger used for live writes and for the authoritative live-level decision.</param>
    /// <param name="replayTarget">
    /// The logger buffered records and the error trigger are replayed to during a flush. Writes
    /// directly to the registered providers, bypassing the Microsoft.Extensions.Logging factory
    /// filters.
    /// </param>
    /// <param name="options">The buffering configuration. Validated by the constructor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner" />, <paramref name="replayTarget" />, or <paramref name="options" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The options fail <see cref="BufferingLoggerOptions.Validate" />.</exception>
    public BufferingLogger(ILogger inner, ILogger replayTarget, BufferingLoggerOptions options)
    {
        Guard.NotNull(inner);
        Guard.NotNull(replayTarget);
        Guard.NotNull(options);
        options.Validate();
        this.inner = inner;
        this.replayTarget = replayTarget;
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

        if (logLevel >= options.FlushLevel)
        {
            // Flush-level records are written live when the host configuration allows it, and are
            // also worth producing whenever there is an active scope to flush — even if the live
            // configuration would suppress them — because they trigger the dump.
            return inner.IsEnabled(logLevel) || HasActiveScope();
        }

        if (inner.IsEnabled(logLevel))
        {
            // The host configuration writes this level live; nothing for the buffer to decide.
            return true;
        }

        if (logLevel >= options.BufferLevel)
        {
            // Buffer-range records are only worth producing when there is somewhere to buffer them.
            var scope = LogBuffer.Current;
            if (scope is null || scope.IsDisposed)
            {
                return false;
            }

            // After a flush with suspend-after-flush enabled, further buffer-range records are
            // written live, so they are only enabled if the host configuration would write them
            // (which it would not here, since inner.IsEnabled returned false above).
            return !(options.SuspendBufferingAfterFlush && scope.IsFlushed);
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
            var scope = LogBuffer.Current;
            var hasScope = scope is not null && !scope.IsDisposed;

            if (hasScope)
            {
                // Dump the accrued context first (replayed directly to every provider), then write
                // the triggering record the same way so every sink that just received the buffered
                // context also receives the error that explains it. Flush is best-effort and never
                // throws, so the triggering record is always written.
                scope!.FlushForError();
                if (ReplayHasSinks())
                {
                    replayTarget.Log(logLevel, eventId, state, exception, formatter);
                }
                else
                {
                    // The replay target reaches no providers — for example a custom inner factory
                    // whose providers were not supplied to this logger. The buffered context cannot
                    // be surfaced (the inner filter would suppress it), but the triggering record
                    // must not be lost, so fall back to the inner logger for it.
                    inner.Log(logLevel, eventId, state, exception, formatter);
                }

                return;
            }

            // No scope to dump: there is no buffered context, so the record follows the host's live
            // configuration exactly as it would without buffering. The host LogLevel stays
            // authoritative — a suppressed flush-level record stays suppressed.
            inner.Log(logLevel, eventId, state, exception, formatter);
            return;
        }

        if (inner.IsEnabled(logLevel))
        {
            // The host configuration writes this level live.
            inner.Log(logLevel, eventId, state, exception, formatter);
            return;
        }

        if (logLevel < options.BufferLevel)
        {
            return;
        }

        var bufferScope = LogBuffer.Current;
        if (bufferScope is null || bufferScope.IsDisposed)
        {
            // No active scope to buffer into: drop the record rather than emit unscoped noise.
            return;
        }

        if (options.SuspendBufferingAfterFlush && bufferScope.IsFlushed)
        {
            // Fast path: already flushed once. Buffering is suspended, but the host configuration
            // does not write this level live (inner.IsEnabled was false), so there is nothing to do.
            return;
        }

        var message = RenderMessage(state, exception, formatter);
        var entry = BufferedLogEntry.Create(replayTarget, logLevel, eventId, state, message, exception);
        bufferScope.TryEnqueue(entry, options.SuspendBufferingAfterFlush);
    }

    private static bool HasActiveScope()
    {
        var scope = LogBuffer.Current;
        return scope is not null && !scope.IsDisposed;
    }

    // The replay target is the direct-to-providers logger except in tests; treat any non-replay
    // logger as always having a sink so it is used unchanged. A ProviderReplayLogger with no
    // providers has no sink, so the triggering record falls back to the inner logger.
    private bool ReplayHasSinks() => replayTarget is not ProviderReplayLogger replay || replay.HasSinks;

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
}
