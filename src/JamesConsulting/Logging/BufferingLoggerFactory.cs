using System;
using JamesConsulting.Internal;
using Microsoft.Extensions.Logging;

namespace JamesConsulting.Logging;

/// <summary>
/// An <see cref="ILoggerFactory" /> decorator that wraps every logger produced by an inner factory
/// in a <see cref="BufferingLogger" />, so the buffering ("dump-on-error") behaviour applies across
/// all categories without changing call sites.
/// </summary>
public sealed class BufferingLoggerFactory : ILoggerFactory
{
    private readonly ILoggerFactory inner;
    private readonly BufferingLoggerOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferingLoggerFactory" /> class.
    /// </summary>
    /// <param name="inner">The inner factory whose loggers are wrapped.</param>
    /// <param name="options">The buffering configuration applied to every produced logger. Validated by the constructor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner" /> or <paramref name="options" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The options fail <see cref="BufferingLoggerOptions.Validate" />.</exception>
    public BufferingLoggerFactory(ILoggerFactory inner, BufferingLoggerOptions options)
    {
        Guard.NotNull(inner);
        Guard.NotNull(options);
        options.Validate();
        this.inner = inner;
        this.options = options;
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) =>
        new BufferingLogger(inner.CreateLogger(categoryName), options);

    /// <inheritdoc />
    public void AddProvider(ILoggerProvider provider) => inner.AddProvider(provider);

    /// <summary>
    /// Disposes the inner factory.
    /// </summary>
    public void Dispose() => inner.Dispose();
}
