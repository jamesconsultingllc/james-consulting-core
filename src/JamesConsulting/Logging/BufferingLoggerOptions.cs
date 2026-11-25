using System;
using Microsoft.Extensions.Logging;

namespace JamesConsulting.Logging;

/// <summary>
/// Configuration for the buffering ("dump-on-error") logger.
/// </summary>
/// <remarks>
/// <para>
/// The buffering logger leaves your host's live logging threshold (for example
/// <c>Logging:LogLevel:Default</c>) completely untouched and authoritative — that configuration
/// still decides which records are written live, exactly as it does without buffering. On top of
/// that, two thresholds control buffering, which must satisfy <c>BufferLevel &lt;= FlushLevel</c>:
/// </para>
/// <list type="bullet">
///     <item>
///         A record whose level your live configuration would already write is written live as
///         normal — buffering does not change it.
///     </item>
///     <item>
///         A record <em>below</em> the live threshold but at or above <see cref="BufferLevel" /> is
///         captured into the active <see cref="LogBufferScope" /> (and dropped if no scope is
///         active), so it is only emitted if a flush occurs.
///     </item>
///     <item>
///         A record below <see cref="BufferLevel" /> is dropped.
///     </item>
///     <item>
///         A record at or above <see cref="FlushLevel" /> that occurs inside an active scope flushes
///         that scope: every buffered record — and the triggering record — is replayed directly to
///         the registered logging providers so you get the diagnostic context leading up to the
///         failure. Outside a scope there is nothing to dump, so the record follows your normal live
///         configuration.
///     </item>
/// </list>
/// <para>
/// The replayed dump is written <em>directly</em> to the registered providers and therefore bypasses
/// the Microsoft.Extensions.Logging factory-level filters (category, provider, and
/// <c>Logging:LogLevel</c> rules). This is deliberate: the whole point of a dump-on-error buffer is
/// to surface low-level context that your live configuration suppresses, so no extra filter
/// configuration is required for the dump to reach your sinks. It also means an error replays the
/// buffered context to every registered provider, even one whose own configured level would normally
/// exclude those records.
/// </para>
/// <para>
/// The filter bypass extends to the triggering record itself. <em>Inside an active scope</em>, a
/// record at or above <see cref="FlushLevel" /> is force-routed to every registered provider together
/// with the dump, and <see cref="BufferingLogger.IsEnabled" /> reports <c>true</c> for flush-level
/// records whenever a scope is active. So if your configuration has silenced a category or provider,
/// an error logged in that category inside a scope <em>still emits</em> — the error and its buffered
/// context, to every provider — because the trigger must reach the same sinks that just received its
/// context. The same flush-level record logged <em>outside</em> a scope honors your live
/// configuration normally.
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
    /// Gets or sets the level at or above which a record flushes the active
    /// <see cref="LogBufferScope" />, replaying every buffered record and the triggering record
    /// directly to the registered providers. Defaults to <see cref="LogLevel.Error" />.
    /// </summary>
    public LogLevel FlushLevel { get; set; } = LogLevel.Error;

    /// <summary>
    /// Gets or sets a value indicating whether buffering is suspended for the remainder of a scope
    /// after it has been flushed. When <c>true</c> (the default), buffer-range records that occur
    /// after a flush honor the host's live configuration instead of being re-buffered (so a record
    /// below the live threshold is dropped), matching the behaviour of the built-in .NET log
    /// buffering. When <c>false</c>, the scope resumes buffering after a flush.
    /// </summary>
    /// <remarks>
    /// A consequence of the default <c>true</c> is that a scope dumps context at most once: after the
    /// first flush no further records are buffered, so a second error later in the same long-lived
    /// scope dumps nothing. Use one scope per logical operation (or set this to <c>false</c>) if you
    /// want every error to carry its own buffered context.
    /// </remarks>
    public bool SuspendBufferingAfterFlush { get; set; } = true;

    /// <summary>
    /// Validates that the configured thresholds satisfy the required invariant.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="BufferLevel" /> or <see cref="FlushLevel" /> is <see cref="LogLevel.None" />, or
    /// the invariant <c>BufferLevel &lt;= FlushLevel</c> is violated.
    /// </exception>
    public void Validate()
    {
        EnsureReal(BufferLevel, nameof(BufferLevel));
        EnsureReal(FlushLevel, nameof(FlushLevel));

        if (BufferLevel > FlushLevel)
        {
            throw new ArgumentOutOfRangeException(
                nameof(FlushLevel),
                FlushLevel,
                $"{nameof(FlushLevel)} ({FlushLevel}) must be greater than or equal to {nameof(BufferLevel)} ({BufferLevel}).");
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
