using System;
using System.Collections.Concurrent;
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
    private static readonly ILoggerProvider[] NoProviders = Array.Empty<ILoggerProvider>();

    /// <summary>
    /// A null inner factory is rejected.
    /// </summary>
    [Fact]
    public void ConstructorNullInnerThrows()
    {
        var act = () => new BufferingLoggerFactory(null!, NoProviders, new BufferingLoggerOptions());

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// A null providers collection is rejected.
    /// </summary>
    [Fact]
    public void ConstructorNullProvidersThrows()
    {
        var act = () => new BufferingLoggerFactory(Substitute.For<ILoggerFactory>(), null!, new BufferingLoggerOptions());

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Null options are rejected.
    /// </summary>
    [Fact]
    public void ConstructorNullOptionsThrows()
    {
        var act = () => new BufferingLoggerFactory(Substitute.For<ILoggerFactory>(), NoProviders, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Invalid options are rejected by the constructor.
    /// </summary>
    [Fact]
    public void ConstructorInvalidOptionsThrows()
    {
        var options = new BufferingLoggerOptions { BufferLevel = LogLevel.None };

        var act = () => new BufferingLoggerFactory(Substitute.For<ILoggerFactory>(), NoProviders, options);

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
        var factory = new BufferingLoggerFactory(innerFactory, NoProviders, new BufferingLoggerOptions());

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
        var factory = new BufferingLoggerFactory(innerFactory, NoProviders, new BufferingLoggerOptions());
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
        var factory = new BufferingLoggerFactory(innerFactory, NoProviders, new BufferingLoggerOptions());

        factory.Dispose();

        innerFactory.Received(1).Dispose();
    }

    /// <summary>
    /// A provider supplied at construction receives the error dump (buffered context and the
    /// triggering record) directly, even though the inner logger would suppress the buffered level.
    /// </summary>
    [Fact]
    public void SeededProviderReceivesDumpOnError()
    {
        var recorder = new RecordingProvider();
        var innerFactory = Substitute.For<ILoggerFactory>();
        innerFactory.CreateLogger("cat").Returns(new RecordingLogger(LogLevel.Information));
        var factory = new BufferingLoggerFactory(innerFactory, new ILoggerProvider[] { recorder }, new BufferingLoggerOptions());

        var logger = factory.CreateLogger("cat");

        using (LogBuffer.BeginScope())
        {
            logger.LogDebug("ctx");
            logger.LogError("boom");
        }

        recorder.Messages.Should().ContainInOrder("ctx", "boom");
    }

    /// <summary>
    /// A provider added after construction via <see cref="BufferingLoggerFactory.AddProvider" /> also
    /// participates in the error dump, because the replay target snapshots the live provider set.
    /// </summary>
    [Fact]
    public void LateAddedProviderReceivesDumpOnError()
    {
        var recorder = new RecordingProvider();
        var innerFactory = Substitute.For<ILoggerFactory>();
        innerFactory.CreateLogger("cat").Returns(new RecordingLogger(LogLevel.Information));
        var factory = new BufferingLoggerFactory(innerFactory, NoProviders, new BufferingLoggerOptions());

        var logger = factory.CreateLogger("cat");
        factory.AddProvider(recorder);

        using (LogBuffer.BeginScope())
        {
            logger.LogDebug("ctx");
            logger.LogError("boom");
        }

        recorder.Messages.Should().ContainInOrder("ctx", "boom");
    }

    /// <summary>
    /// When the replay target reaches no providers (for example a custom inner factory whose
    /// providers were not supplied to the buffering factory), a scoped error must not be lost: the
    /// triggering record falls back to the inner logger. The buffered context cannot be surfaced,
    /// because the inner filter that hid it live still applies.
    /// </summary>
    [Fact]
    public void ScopedErrorFallsBackToInnerWhenNoReplayProviders()
    {
        var innerLogger = new RecordingLogger(LogLevel.Information);
        var innerFactory = Substitute.For<ILoggerFactory>();
        innerFactory.CreateLogger("cat").Returns(innerLogger);
        var factory = new BufferingLoggerFactory(innerFactory, NoProviders, new BufferingLoggerOptions());

        var logger = factory.CreateLogger("cat");

        using (LogBuffer.BeginScope())
        {
            logger.LogDebug("ctx");
            logger.LogError("boom");
        }

        innerLogger.Records.Should().ContainSingle(r => r.Level == LogLevel.Error && r.Message == "boom");
        innerLogger.Records.Should().NotContain(r => r.Level == LogLevel.Debug);
    }

    private sealed class RecordingProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new ProviderLogger(Messages);

        public void Dispose()
        {
        }

        private sealed class ProviderLogger : ILogger
        {
            private readonly ConcurrentQueue<string> messages;

            public ProviderLogger(ConcurrentQueue<string> messages) => this.messages = messages;

            public IDisposable BeginScope<TState>(TState state)
                where TState : notnull => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                messages.Enqueue(formatter(state, exception));

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();

                public void Dispose()
                {
                }
            }
        }
    }
}
