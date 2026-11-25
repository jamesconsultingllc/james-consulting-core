using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace JamesConsulting.Logging;

/// <summary>
/// An <see cref="ILogger" /> used as the replay target for a buffering dump. It fans a record out to
/// every registered <see cref="ILoggerProvider" />'s logger for a single category, writing
/// <em>directly</em> to the provider loggers so the dump bypasses the Microsoft.Extensions.Logging
/// factory-level filters (category, provider, and <c>Logging:LogLevel</c> rules). This is what lets
/// flushed buffer-range records reach the sinks even though the live configuration suppressed them.
/// </summary>
/// <remarks>
/// <para>
/// The provider set is read from a live snapshot on every write, so providers added to the factory
/// after this logger was created are still included in later dumps. Per-provider loggers are cached
/// by provider instance; under concurrent first-time dumps a provider's
/// <see cref="ILoggerProvider.CreateLogger" /> may run more than once and the extra result is
/// discarded, which is harmless for the in-box providers but worth noting if a provider's
/// <c>CreateLogger</c> is expensive or side-effecting. The provider instances are owned by the
/// underlying <see cref="ILoggerFactory" />/DI container, so this type never disposes them.
/// </para>
/// <para>
/// Records are written outside any buffering decision, so a single faulty provider cannot break the
/// rest of the dump: each provider write is guarded and its exceptions are swallowed.
/// </para>
/// </remarks>
internal sealed class ProviderReplayLogger : ILogger
{
    private readonly string category;
    private readonly Func<ILoggerProvider[]> providersSnapshot;
    private readonly ConcurrentDictionary<ILoggerProvider, ILogger> loggers = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderReplayLogger" /> class.
    /// </summary>
    /// <param name="category">The category name the replayed records belong to.</param>
    /// <param name="providersSnapshot">
    /// A callback returning the current set of providers to fan out to. Invoked on every write so
    /// late-added providers are picked up.
    /// </param>
    public ProviderReplayLogger(string category, Func<ILoggerProvider[]> providersSnapshot)
    {
        this.category = category;
        this.providersSnapshot = providersSnapshot;
    }

    /// <summary>
    /// Gets a value indicating whether the replay target currently has at least one provider to fan
    /// out to. When <c>false</c> a dump would reach no sink, so the caller can fall back to the inner
    /// logger for the triggering record rather than lose it.
    /// </summary>
    public bool HasSinks => providersSnapshot().Length > 0;

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        foreach (var provider in providersSnapshot())
        {
            ILogger logger;
            try
            {
                logger = loggers.GetOrAdd(provider, p => p.CreateLogger(category));
            }
            catch
            {
                // A provider whose CreateLogger throws cannot participate in the dump; skip it
                // rather than abort the whole replay.
                continue;
            }

            try
            {
                logger.Log(logLevel, eventId, state, exception, formatter);
            }
            catch
            {
                // Best-effort: one faulty sink must not break the dump or suppress the triggering
                // error. The buffering logger is diagnostic infrastructure and never throws back
                // into the caller's logging path.
            }
        }
    }
}
