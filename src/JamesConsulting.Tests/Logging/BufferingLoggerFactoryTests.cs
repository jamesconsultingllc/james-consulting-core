using System;
using FluentAssertions;
using JamesConsulting.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace JamesConsulting.Tests.Logging;

/// <summary>
/// Unit tests for <see cref="BufferingLoggerFactory" />.
/// </summary>
public class BufferingLoggerFactoryTests
{
    /// <summary>
    /// A null inner factory is rejected.
    /// </summary>
    [Fact]
    public void ConstructorNullInnerThrows()
    {
        var act = () => new BufferingLoggerFactory(null!, new BufferingLoggerOptions());

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Null options are rejected.
    /// </summary>
    [Fact]
    public void ConstructorNullOptionsThrows()
    {
        var act = () => new BufferingLoggerFactory(Substitute.For<ILoggerFactory>(), null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Invalid options are rejected by the constructor.
    /// </summary>
    [Fact]
    public void ConstructorInvalidOptionsThrows()
    {
        var options = new BufferingLoggerOptions { BufferLevel = LogLevel.None };

        var act = () => new BufferingLoggerFactory(Substitute.For<ILoggerFactory>(), options);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// <see cref="BufferingLoggerFactory.CreateLogger" /> wraps the inner logger in a
    /// <see cref="BufferingLogger" />.
    /// </summary>
    [Fact]
    public void CreateLoggerReturnsBufferingLogger()
    {
        var innerFactory = Substitute.For<ILoggerFactory>();
        innerFactory.CreateLogger("cat").Returns(NullLogger.Instance);
        var factory = new BufferingLoggerFactory(innerFactory, new BufferingLoggerOptions());

        var logger = factory.CreateLogger("cat");

        logger.Should().BeOfType<BufferingLogger>();
        innerFactory.Received(1).CreateLogger("cat");
    }

    /// <summary>
    /// <see cref="BufferingLoggerFactory.AddProvider" /> is delegated to the inner factory.
    /// </summary>
    [Fact]
    public void AddProviderDelegatesToInner()
    {
        var innerFactory = Substitute.For<ILoggerFactory>();
        var factory = new BufferingLoggerFactory(innerFactory, new BufferingLoggerOptions());
        var provider = Substitute.For<ILoggerProvider>();

        factory.AddProvider(provider);

        innerFactory.Received(1).AddProvider(provider);
    }

    /// <summary>
    /// <see cref="BufferingLoggerFactory.Dispose" /> is delegated to the inner factory.
    /// </summary>
    [Fact]
    public void DisposeDelegatesToInner()
    {
        var innerFactory = Substitute.For<ILoggerFactory>();
        var factory = new BufferingLoggerFactory(innerFactory, new BufferingLoggerOptions());

        factory.Dispose();

        innerFactory.Received(1).Dispose();
    }
}
