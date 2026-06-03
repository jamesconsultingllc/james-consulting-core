using System;
using Microsoft.Extensions.Logging;

namespace JamesConsulting.Logging;

/// <summary>
/// Configuration for the buffering ("dump-on-error") logger.
/// </summary>
/// <remarks>
/// <para>
/// The buffering logger routes each record using three thresholds, which must satisfy the
/// invariant <c>BufferLevel &lt;= PassthroughLevel &lt;= FlushLevel</c>:
/// </para>
/// <list type="bullet">
///     <item>
///         Records below <see cref="BufferLevel" /> are dropped.
///     </item>
///     <item>
///         Records in the range <c>[BufferLevel, PassthroughLevel)</c> are captured into the
///         active <see cref="LogBufferScope" /> and only emitted if a flush occurs.
///     </item>
///     <item>
///         Records in the range <c>[PassthroughLevel, FlushLevel)</c> are written through to the
///         inner logger immediately (normal logging).
///     </item>
///     <item>
///         Records at or above <see cref="FlushLevel" /> flush the active scope (emitting every
///         buffered record) and are then written through immediately.
///     </item>
/// </list>
/// <para>
/// Because the buffering logger decorates the inner <see cref="ILogger" />, replayed records still
/// pass through the inner logger's own level filtering. A plain
/// <c>SetMinimumLevel(LogLevel.Trace)</c> is <em>not</em> sufficient: when the host binds a
/// configuration section such as <c>Logging:LogLevel:Default</c>, the resulting catch-all filter
/// rule takes precedence over the minimum level, so flushed Debug/Trace records are silently
/// discarded. Instead, ensure a filter <em>rule</em> at <see cref="BufferLevel" /> applies to the
/// buffered categories — for example set <c>Logging:LogLevel:Default</c> to <c>Trace</c>, or call
/// <c>AddFilter(null, LogLevel.Trace)</c>. By default <see cref="ConfigureUnderlyingFilter" /> wires
/// this up for you. During normal operation nothing below <see cref="PassthroughLevel" /> is
/// forwarded to the inner logger, so your sinks stay quiet until a flush is triggered.
/// </para>
/// </remarks>
public sealed class BufferingLoggerOptions
{
    /// <summary>
    /// Gets or sets the lowest level captured into the buffer. Records below this level are dropped
    /// entirely. Defaults to <see cref="LogLevel.Trace" />.
    /// </summary>
    public LogLevel BufferLevel { get; set; } = LogLevel.Trace;

    /// <summary>
    /// Gets or sets the level at or above which records are always written through to the inner
    /// logger immediately (normal logging). Defaults to <see cref="LogLevel.Information" />.
    /// </summary>
    public LogLevel PassthroughLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets the level at or above which a record flushes the active
    /// <see cref="LogBufferScope" />, emitting every buffered record before the triggering record is
    /// written. Defaults to <see cref="LogLevel.Error" />.
    /// </summary>
    public LogLevel FlushLevel { get; set; } = LogLevel.Error;

    /// <summary>
    /// Gets or sets a value indicating whether buffering is suspended for the remainder of a scope
    /// after it has been flushed. When <c>true</c> (the default), buffer-range records that occur
    /// after a flush are written through live instead of re-buffered, matching the behaviour of the
    /// built-in .NET log buffering. When <c>false</c>, the scope resumes buffering after a flush.
    /// </summary>
    public bool SuspendBufferingAfterFlush { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether <c>AddBufferingLogging</c> automatically lowers the
    /// underlying logging filter so that flushed buffer-range records can reach your sinks. When
    /// <c>true</c> (the default), a catch-all filter rule at <see cref="BufferLevel" /> is appended
    /// as the last (and therefore winning) <em>no-category</em> rule, and the minimum level is
    /// lowered to <see cref="BufferLevel" /> if it was higher.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This wins over a configuration-bound <c>Logging:LogLevel:Default</c> rule, but it does
    /// <em>not</em> override more specific category- or provider-scoped rules (for example
    /// <c>Logging:LogLevel:MyApp</c> or <c>Logging:Console:LogLevel:Default</c>); dumps for those
    /// categories remain filtered, and the buffering logger emits a one-time warning when it detects
    /// this.
    /// </para>
    /// <para>
    /// Because the rule is level-based, lowering the effective default filter also makes
    /// passthrough-band records (at or above <see cref="PassthroughLevel" />) visible live where a
    /// higher configured default level would have suppressed them. Set this to <c>false</c> if you
    /// need to control the underlying filter yourself; in that case you must ensure a filter rule at
    /// <see cref="BufferLevel" /> applies to the buffered categories, otherwise dumped context is
    /// discarded by the inner logger's filtering.
    /// </para>
    /// </remarks>
    public bool ConfigureUnderlyingFilter { get; set; } = true;

    /// <summary>
    /// Validates that the configured thresholds satisfy the required invariant.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Any threshold is <see cref="LogLevel.None" />, or the invariant
    /// <c>BufferLevel &lt;= PassthroughLevel &lt;= FlushLevel</c> is violated.
    /// </exception>
    public void Validate()
    {
        EnsureReal(BufferLevel, nameof(BufferLevel));
        EnsureReal(PassthroughLevel, nameof(PassthroughLevel));
        EnsureReal(FlushLevel, nameof(FlushLevel));

        if (BufferLevel > PassthroughLevel)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PassthroughLevel),
                PassthroughLevel,
                $"{nameof(PassthroughLevel)} ({PassthroughLevel}) must be greater than or equal to {nameof(BufferLevel)} ({BufferLevel}).");
        }

        if (PassthroughLevel > FlushLevel)
        {
            throw new ArgumentOutOfRangeException(
                nameof(FlushLevel),
                FlushLevel,
                $"{nameof(FlushLevel)} ({FlushLevel}) must be greater than or equal to {nameof(PassthroughLevel)} ({PassthroughLevel}).");
        }
    }

    private static void EnsureReal(LogLevel level, string name)
    {
        if (level == LogLevel.None)
        {
            throw new ArgumentOutOfRangeException(name, level, $"{name} cannot be {nameof(LogLevel.None)}.");
        }
    }
}
