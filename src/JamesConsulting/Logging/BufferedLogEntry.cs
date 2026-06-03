using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace JamesConsulting.Logging;

/// <summary>
/// An immutable snapshot of a single buffered log record. Captured eagerly when a record is
/// buffered so that replaying it later is safe even if the original state is mutated, pooled, or
/// disposed after the originating <see cref="ILogger.Log{TState}" /> call returns. Structured state
/// is copied pair-by-pair; scalar values (strings and value types such as numbers, enums,
/// <see cref="DateTime" />, and <see cref="Guid" />) are preserved with their original type, while
/// any other (potentially mutable) reference value is frozen to its log-time text via
/// <see cref="object.ToString" />. This means an object-valued structured property
/// (for example <c>logger.LogDebug("processing {Order}", order)</c>) is replayed as a string, so a
/// destructuring sink receives the frozen text rather than the live object's properties — the safe
/// trade-off for a dump buffer, since the object may be mutated or disposed before replay.
/// </summary>
internal sealed class BufferedLogEntry
{
    private static readonly Func<object?, Exception?, string> ReplayFormatter =
        static (state, _) => state?.ToString() ?? string.Empty;

    private readonly ILogger inner;
    private readonly LogLevel level;
    private readonly EventId eventId;
    private readonly Exception? exception;
    private readonly object? state;

    private BufferedLogEntry(ILogger inner, LogLevel level, EventId eventId, object? state, Exception? exception)
    {
        this.inner = inner;
        this.level = level;
        this.eventId = eventId;
        this.state = state;
        this.exception = exception;
    }

    /// <summary>
    /// Creates a snapshot of a log record, eagerly rendering its message and copying any structured
    /// state so the entry can be replayed safely later.
    /// </summary>
    /// <typeparam name="TState">The type of the original log state.</typeparam>
    /// <param name="inner">The inner logger that the entry replays to (the originating category).</param>
    /// <param name="level">The original log level.</param>
    /// <param name="eventId">The original event id.</param>
    /// <param name="state">The original log state.</param>
    /// <param name="message">The pre-rendered message text.</param>
    /// <param name="exception">The original exception, if any.</param>
    /// <returns>An immutable <see cref="BufferedLogEntry" />.</returns>
    public static BufferedLogEntry Create<TState>(
        ILogger inner,
        LogLevel level,
        EventId eventId,
        TState state,
        string message,
        Exception? exception)
    {
        object? snapshot;
        try
        {
            snapshot = state is IEnumerable<KeyValuePair<string, object?>> structured
                ? new SnapshotState(Freeze(structured), message)
                : (object?)message;
        }
        catch
        {
            // Snapshotting runs on the caller's logging path. A hostile or buggy structured state
            // (for example an enumerable whose enumerator throws) must not surface as a logging
            // failure; fall back to the already-rendered message text.
            snapshot = message;
        }

        return new BufferedLogEntry(inner, level, eventId, snapshot, exception);
    }

    private static KeyValuePair<string, object?>[] Freeze(IEnumerable<KeyValuePair<string, object?>> structured)
    {
        var values = structured.ToArray();
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = new KeyValuePair<string, object?>(values[i].Key, FreezeValue(values[i].Value));
        }

        return values;
    }

    // Preserve scalars (immutable) by reference to keep structured typing; freeze any other reference
    // value to its log-time text so a later mutation cannot change what a replayed record reports.
    private static object? FreezeValue(object? value)
    {
        switch (value)
        {
            case null:
            case string:
            case ValueType:
                return value;
            default:
                try
                {
                    return value.ToString();
                }
                catch
                {
                    // Freezing runs on the caller's logging path; a throwing ToString must not
                    // surface as a logging failure. Fall back to the type name.
                    return value.GetType().FullName;
                }
        }
    }

    /// <summary>
    /// Replays the buffered record to its originating inner logger, preserving the original level,
    /// event id, message, structured state, and exception.
    /// </summary>
    public void Replay() => inner.Log(level, eventId, state, exception, ReplayFormatter);

    /// <summary>
    /// A defensive copy of a structured log state that preserves the original key/value pairs and
    /// the rendered message (via <see cref="ToString" />), so sinks that read structured state see
    /// the same data on replay.
    /// </summary>
    private sealed class SnapshotState : IReadOnlyList<KeyValuePair<string, object?>>
    {
        private readonly KeyValuePair<string, object?>[] values;
        private readonly string message;

        public SnapshotState(KeyValuePair<string, object?>[] values, string message)
        {
            this.values = values;
            this.message = message;
        }

        public int Count => values.Length;

        public KeyValuePair<string, object?> this[int index] => values[index];

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() =>
            ((IEnumerable<KeyValuePair<string, object?>>)values).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => values.GetEnumerator();

        public override string ToString() => message;
    }
}
