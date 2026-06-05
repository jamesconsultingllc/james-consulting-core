using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using JamesConsulting.Internal;
using Microsoft.Extensions.Logging;

namespace JamesConsulting.Logging;

/// <summary>
/// An <see cref="ILoggerFactory" /> decorator that wraps every logger produced by an inner factory
/// in a <see cref="BufferingLogger" />, so the buffering ("dump-on-error") behaviour applies across
/// all categories without changing call sites.
/// </summary>
/// <remarks>
/// The factory also tracks the registered <see cref="ILoggerProvider" /> instances so that, on an
/// error-triggered flush, buffered records can be replayed directly to the providers — bypassing the
/// Microsoft.Extensions.Logging factory-level filters. The provider set is seeded from the ones
/// supplied at construction (resolved from DI) and extended by <see cref="AddProvider" />.
/// </remarks>
public sealed class BufferingLoggerFactory : ILoggerFactory
{
    private readonly ILoggerFactory inner;
    private readonly BufferingLoggerOptions options;
    private readonly object gate = new();
    private readonly List<ILoggerProvider> providers;
    private readonly ConcurrentDictionary<string, ILogger> loggers = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferingLoggerFactory" /> class.
    /// </summary>
    /// <param name="inner">The inner factory whose loggers are wrapped.</param>
    /// <param name="providers">
    /// The logging providers buffered records are replayed to on flush. Typically the same providers
    /// registered with the inner factory (resolved from DI). Providers added later via
    /// <see cref="AddProvider" /> are included too.
    /// </param>
    /// <param name="options">The buffering configuration applied to every produced logger. Validated by the constructor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner" />, <paramref name="providers" />, or <paramref name="options" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The options fail <see cref="BufferingLoggerOptions.Validate" />.</exception>
    public BufferingLoggerFactory(ILoggerFactory inner, IEnumerable<ILoggerProvider> providers, BufferingLoggerOptions options)
    {
        Guard.NotNull(inner);
        Guard.NotNull(providers);
        Guard.NotNull(options);
        options.Validate();
        this.inner = inner;
        this.options = options;
        this.providers = providers.Where(static p => p is not null).Distinct().ToList();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Loggers are cached per category name (ordinal), so repeated calls for the same category
    /// return the same instance, matching the Microsoft.Extensions.Logging factory contract.
    /// Caching is safe with respect to providers added later via <see cref="AddProvider" /> because
    /// each logger resolves the live provider set at write time through <see cref="SnapshotProviders" />.
    /// </remarks>
    public ILogger CreateLogger(string categoryName) =>
        loggers.GetOrAdd(
            categoryName,
            name => new BufferingLogger(
                inner.CreateLogger(name),
                new ProviderReplayLogger(name, SnapshotProviders),
                options));

    /// <inheritdoc />
    public void AddProvider(ILoggerProvider provider)
    {
        Guard.NotNull(provider);
        inner.AddProvider(provider);
        lock (gate)
        {
            if (!providers.Contains(provider))
            {
                providers.Add(provider);
            }
        }
    }

    /// <summary>
    /// Disposes the inner factory.
    /// </summary>
    public void Dispose() => inner.Dispose();

    private ILoggerProvider[] SnapshotProviders()
    {
        lock (gate)
        {
            return providers.ToArray();
        }
    }
}
