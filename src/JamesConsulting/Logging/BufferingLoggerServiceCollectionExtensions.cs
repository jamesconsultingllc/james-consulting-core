using System;
using System.Linq;
using JamesConsulting.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JamesConsulting.Logging;

/// <summary>
/// Dependency-injection helpers that enable buffering ("dump-on-error") logging by decorating the
/// registered <see cref="ILoggerFactory" />.
/// </summary>
/// <remarks>
/// <para>
/// Buffering works by holding back low-level records and only emitting them when an error triggers
/// a flush. For flushed records to reach your sinks, a filter <em>rule</em> at your
/// <see cref="BufferingLoggerOptions.BufferLevel" /> must apply to the buffered categories. A plain
/// <c>SetMinimumLevel(LogLevel.Trace)</c> is not enough, because a configuration-provided rule such
/// as <c>Logging:LogLevel:Default</c> takes precedence over the minimum level. By default
/// <see cref="BufferingLoggerOptions.ConfigureUnderlyingFilter" /> is enabled, so this method
/// appends a winning no-category filter rule at <see cref="BufferingLoggerOptions.BufferLevel" /> for
/// you. Note this only beats the no-category <c>Default</c> rule, not more specific category- or
/// provider-scoped rules, and (being level-based) it also surfaces passthrough-band records that a
/// higher configured default would have hidden — see
/// <see cref="BufferingLoggerOptions.ConfigureUnderlyingFilter" />. The buffering logger still
/// suppresses live emission of records below
/// <see cref="BufferingLoggerOptions.PassthroughLevel" />, so your sinks stay quiet until a flush
/// occurs.
/// </para>
/// </remarks>
public static class BufferingLoggerServiceCollectionExtensions
{
    /// <summary>
    /// Decorates the registered <see cref="ILoggerFactory" /> so that every logger buffers low-level
    /// records and dumps them on error. Call after logging has been registered (for example after
    /// <c>AddLogging</c>). This call is idempotent: if buffering has already been added, subsequent
    /// calls are ignored and the first registration's options are kept.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional callback to configure <see cref="BufferingLoggerOptions" />.</param>
    /// <returns>The same <paramref name="services" /> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services" /> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">No <see cref="ILoggerFactory" /> has been registered yet.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The configured options fail validation.</exception>
    public static IServiceCollection AddBufferingLogging(
        this IServiceCollection services,
        Action<BufferingLoggerOptions>? configure = null)
    {
        Guard.NotNull(services);

        var options = new BufferingLoggerOptions();
        configure?.Invoke(options);
        options.Validate();

        if (services.Any(d => d.ServiceType == typeof(BufferingLoggingMarker)))
        {
            // Idempotent: a previous AddBufferingLogging call already decorated the factory. Decorating
            // again would nest BufferingLoggers and replay buffered records through a second buffer.
            // The first registration wins.
            return services;
        }

        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(ILoggerFactory));
        if (descriptor is null)
        {
            throw new InvalidOperationException(
                "AddBufferingLogging requires logging to be registered first. Call AddLogging (or services.AddLogging) before AddBufferingLogging.");
        }

        services.Remove(descriptor);
        services.Add(new ServiceDescriptor(
            typeof(ILoggerFactory),
            provider => new BufferingLoggerFactory(ResolveInner(provider, descriptor), options),
            descriptor.Lifetime));
        services.Add(new ServiceDescriptor(typeof(BufferingLoggingMarker), new BufferingLoggingMarker()));

        if (options.ConfigureUnderlyingFilter)
        {
            var bufferLevel = options.BufferLevel;

            // Lower the underlying logging filter so flushed buffer-range records can reach sinks.
            // PostConfigure runs after every Configure callback (including configuration binding of
            // Logging:LogLevel sections), so our catch-all rule is appended last and therefore wins
            // the "take the last matching rule" tie-break. SetMinimumLevel/MinLevel alone is bypassed
            // whenever a matching rule (e.g. a bound Default rule) exists.
            services.PostConfigure<LoggerFilterOptions>(filterOptions =>
            {
                if (filterOptions.MinLevel > bufferLevel)
                {
                    filterOptions.MinLevel = bufferLevel;
                }

                filterOptions.Rules.Add(new LoggerFilterRule(
                    providerName: null,
                    categoryName: null,
                    logLevel: bufferLevel,
                    filter: null));
            });
        }

        return services;
    }

    private static ILoggerFactory ResolveInner(IServiceProvider provider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is ILoggerFactory instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return (ILoggerFactory)descriptor.ImplementationFactory(provider);
        }

        if (descriptor.ImplementationType is not null)
        {
            return (ILoggerFactory)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType);
        }

        throw new InvalidOperationException("Unable to resolve the inner ILoggerFactory to decorate.");
    }
}

/// <summary>
/// A marker registered in the service collection to make <c>AddBufferingLogging</c> idempotent, so
/// repeated calls do not nest buffering decorators.
/// </summary>
internal sealed class BufferingLoggingMarker
{
}
