using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace JamesConsulting.Tests.Logging;

/// <summary>
/// A minimal in-memory <see cref="ILogger" /> used by the buffering logger tests to capture the
/// records that reach the inner logger. Records the level, event id, rendered message, exception,
/// and any structured state so tests can assert on dump ordering and fidelity.
/// </summary>
internal sealed class RecordingLogger : ILogger
{
    private readonly LogLevel enabledFrom;
    private readonly List<RecordedLog> records;

    public RecordingLogger(LogLevel enabledFrom = LogLevel.Trace, List<RecordedLog>? sharedSink = null)
    {
        this.enabledFrom = enabledFrom;
        records = sharedSink ?? new List<RecordedLog>();
    }

    public List<RecordedLog> Records => records;

    /// <summary>
    /// Optional hook that, when it returns <c>true</c> for a given level and rendered message, makes
    /// the logger throw instead of recording — used to simulate a faulty sink during replay.
    /// </summary>
    public Func<LogLevel, string, bool>? ThrowOn { get; set; }

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= enabledFrom;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        // Model a real logger's own level filtering so the inner-logger path faithfully drops
        // records the host configuration would suppress. The replay target is constructed enabled
        // from Trace, so it records every dumped record unconditionally (mirroring the direct
        // provider replay, which bypasses the factory filters).
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);

        if (ThrowOn?.Invoke(logLevel, message) == true)
        {
            throw new InvalidOperationException("simulated sink failure");
        }

        IReadOnlyList<KeyValuePair<string, object?>>? structured =
            state as IReadOnlyList<KeyValuePair<string, object?>>;

        records.Add(new RecordedLog(logLevel, eventId, message, exception, structured));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}

/// <summary>
/// A captured log record from <see cref="RecordingLogger" />.
/// </summary>
internal sealed record RecordedLog(
    LogLevel Level,
    EventId EventId,
    string Message,
    Exception? Exception,
    IReadOnlyList<KeyValuePair<string, object?>>? State);
