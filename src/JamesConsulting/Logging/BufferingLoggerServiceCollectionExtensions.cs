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
/// Buffering leaves your host's live logging configuration (for example <c>Logging:LogLevel</c>)
/// untouched and authoritative: records that configuration would write are written live exactly as
/// before. Low-level records below the live threshold are held back and only emitted when an error
/// triggers a flush, at which point the buffered context — and the triggering record — are replayed
/// <em>directly</em> to the registered logging providers, bypassing the
/// Microsoft.Extensions.Logging factory filters. That direct replay is why no extra filter
/// configuration is required for flushed records to reach your sinks.
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

        if (services.Any(d => d.ServiceType == typeof(BufferingLoggingMarker)))
        {
            // Idempotent: a previous AddBufferingLogging call already decorated the factory. Decorating
            // again would nest BufferingLoggers and replay buffered records through a second buffer.
            // The first registration wins, so subsequent calls are ignored before any options are
            // configured or validated.
            return services;
        }

        var options = new BufferingLoggerOptions();
        configure?.Invoke(options);
        options.Validate();

        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(ILoggerFactory));
        if (descriptor is null)
        {
            throw new InvalidOperationException(
                "AddBufferingLogging requires logging to be registered first. Call AddLogging (or services.AddLogging) before AddBufferingLogging.");
        }

        services.Remove(descriptor);
        services.Add(new ServiceDescriptor(
            typeof(ILoggerFactory),
            provider => new BufferingLoggerFactory(
                ResolveInner(provider, descriptor),
                provider.GetServices<ILoggerProvider>(),
                options),
            descriptor.Lifetime));
        services.Add(new ServiceDescriptor(typeof(BufferingLoggingMarker), new BufferingLoggingMarker()));

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
