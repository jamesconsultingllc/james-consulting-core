using System;
using JamesConsulting.Internal;
using Microsoft.Extensions.Logging;

namespace JamesConsulting.Logging;

/// <summary>
/// <see cref="ILoggingBuilder" /> helpers that enable buffering ("dump-on-error") logging.
/// </summary>
public static class BufferingLoggingBuilderExtensions
{
    /// <summary>
    /// Enables buffering ("dump-on-error") logging by decorating the registered
    /// <see cref="ILoggerFactory" />. Your host's live logging configuration stays authoritative;
    /// records below the live threshold are buffered and, on an error inside a
    /// <see cref="LogBufferScope" />, replayed directly to the registered providers so no extra
    /// filter configuration is needed for the dump to reach your sinks.
    /// </summary>
    /// <param name="builder">The logging builder.</param>
    /// <param name="configure">An optional callback to configure <see cref="BufferingLoggerOptions" />.</param>
    /// <returns>The same <paramref name="builder" /> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <c>null</c>.</exception>
    /// <example>
    /// <code>
    /// services.AddLogging(logging =>
    /// {
    ///     logging.AddConsole();
    ///     // The host's Logging:LogLevel still controls live logging. BufferLevel sets how deep the
    ///     // buffer captures; FlushLevel triggers the dump. On error the buffered context is replayed
    ///     // directly to the providers, bypassing the MEL factory filters.
    ///     logging.AddBufferingLogging(o =>
    ///     {
    ///         o.BufferLevel = LogLevel.Trace;
    ///         o.FlushLevel = LogLevel.Error;
    ///     });
    /// });
    /// </code>
    /// </example>
    public static ILoggingBuilder AddBufferingLogging(
        this ILoggingBuilder builder,
        Action<BufferingLoggerOptions>? configure = null)
    {
        Guard.NotNull(builder);
        builder.Services.AddBufferingLogging(configure);
        return builder;
    }
}
