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
    /// <see cref="ILoggerFactory" />. By default the underlying logging filter is lowered for you
    /// (see <see cref="BufferingLoggerOptions.ConfigureUnderlyingFilter" />) so that flushed records
    /// reach your sinks.
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
    ///     // AddBufferingLogging lowers the underlying filter to BufferLevel by default, so a
    ///     // separate SetMinimumLevel/filter rule is not required for the dump to reach sinks.
    ///     logging.AddBufferingLogging(o =>
    ///     {
    ///         o.BufferLevel = LogLevel.Trace;
    ///         o.PassthroughLevel = LogLevel.Information;
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
