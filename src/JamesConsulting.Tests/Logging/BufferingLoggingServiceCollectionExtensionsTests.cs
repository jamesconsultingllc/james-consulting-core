using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using JamesConsulting.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace JamesConsulting.Tests.Logging;

/// <summary>
/// Integration tests for the <c>AddBufferingLogging</c> DI extensions, verifying that the registered
/// <see cref="ILoggerFactory" /> is decorated and that <see cref="ILogger{T}" /> resolves to a
/// buffering logger end to end.
/// </summary>
public class BufferingLoggingServiceCollectionExtensionsTests
{
    /// <summary>
    /// A null service collection is rejected.
    /// </summary>
    [Fact]
    public void AddBufferingLoggingNullServicesThrows()
    {
        IServiceCollection services = null!;

        var act = () => services.AddBufferingLogging();

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// A null logging builder is rejected.
    /// </summary>
    [Fact]
    public void AddBufferingLoggingNullBuilderThrows()
    {
        ILoggingBuilder builder = null!;

        var act = () => builder.AddBufferingLogging();

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// The builder overload returns the same builder for chaining.
    /// </summary>
    [Fact]
    public void AddBufferingLoggingBuilderReturnsSameBuilder()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            var result = builder.AddBufferingLogging();
            result.Should().BeSameAs(builder);
        });

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ILoggerFactory>().Should().BeOfType<BufferingLoggerFactory>();
    }

    /// <summary>
    /// Calling the extension before logging is registered throws a helpful error.
    /// </summary>
    [Fact]
    public void AddBufferingLoggingWithoutLoggingThrows()
    {
        var services = new ServiceCollection();

        var act = () => services.AddBufferingLogging();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AddLogging*");
    }

    /// <summary>
    /// Invalid options surface as an argument-out-of-range during registration.
    /// </summary>
    [Fact]
    public void AddBufferingLoggingInvalidOptionsThrows()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var act = () => services.AddBufferingLogging(o => o.FlushLevel = LogLevel.None);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// The resolved <see cref="ILoggerFactory" /> is the buffering decorator.
    /// </summary>
    [Fact]
    public void AddBufferingLoggingDecoratesFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBufferingLogging();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ILoggerFactory>();

        factory.Should().BeOfType<BufferingLoggerFactory>();
    }

    /// <summary>
    /// End to end: a resolved <see cref="ILogger{T}" /> buffers Debug logs (which the host's
    /// Information live threshold suppresses) and dumps them directly to the provider when an error is
    /// logged within a scope.
    /// </summary>
    [Fact]
    public void ResolvedLoggerDumpsOnErrorWithinScope()
    {
        var recorder = new RecordingProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(recorder);
            builder.AddBufferingLogging();
        });

        using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<BufferingLoggingServiceCollectionExtensionsTests>>();

        using (LogBuffer.BeginScope())
        {
            logger.LogDebug("step one");
            logger.LogDebug("step two");
            logger.LogError("failure");
        }

        recorder.Messages.Should().ContainInOrder("step one", "step two", "failure");
    }

    /// <summary>
    /// End to end: Debug logs are suppressed when no error occurs.
    /// </summary>
    [Fact]
    public void ResolvedLoggerSuppressesDebugWithoutError()
    {
        var recorder = new RecordingProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(recorder);
            builder.AddBufferingLogging();
        });

        using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<BufferingLoggingServiceCollectionExtensionsTests>>();

        using (LogBuffer.BeginScope())
        {
            logger.LogDebug("hidden");
            logger.LogInformation("shown");
        }

        recorder.Messages.Should().Contain("shown");
        recorder.Messages.Should().NotContain("hidden");
    }

    /// <summary>
    /// Calling the extension twice is idempotent: it does not nest decorators or duplicate the dump.
    /// </summary>
    [Fact]
    public void AddBufferingLoggingIsIdempotent()
    {
        var recorder = new RecordingProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(recorder);
            builder.AddBufferingLogging();
            builder.AddBufferingLogging();
        });

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ILoggerFactory>().Should().BeOfType<BufferingLoggerFactory>();

        var logger = provider.GetRequiredService<ILogger<BufferingLoggingServiceCollectionExtensionsTests>>();
        using (LogBuffer.BeginScope())
        {
            logger.LogDebug("ctx");
            logger.LogError("boom");
        }

        recorder.Messages.Count(m => m == "ctx").Should().Be(1);
        recorder.Messages.Count(m => m == "boom").Should().Be(1);
    }

    /// <summary>
    /// Idempotency is decided before options are configured or validated, so a second call with
    /// invalid options is ignored (the first registration wins) rather than throwing.
    /// </summary>
    [Fact]
    public void AddBufferingLoggingSecondInvalidCallIsIgnored()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBufferingLogging();

        var act = () => services.AddBufferingLogging(o => o.FlushLevel = LogLevel.None);

        act.Should().NotThrow();
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ILoggerFactory>().Should().BeOfType<BufferingLoggerFactory>();
    }

    /// <summary>
    /// The error dump is written directly to the registered providers, so it bypasses the
    /// Microsoft.Extensions.Logging factory-level filters: a configuration-bound <c>Default</c> rule
    /// at Information suppresses Debug live, yet the buffered Debug context still surfaces on error —
    /// exactly once (the dump), with no live duplicate, and without any extra filter configuration.
    /// </summary>
    [Fact]
    public void DumpBypassesConfiguredFactoryFilter()
    {
        var recorder = new RecordingProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddProvider(recorder);
            builder.AddBufferingLogging();
        });

        services.Configure<LoggerFilterOptions>(o =>
            o.Rules.Add(new LoggerFilterRule(null, null, LogLevel.Information, null)));

        using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<BufferingLoggingServiceCollectionExtensionsTests>>();

        using (LogBuffer.BeginScope())
        {
            logger.LogDebug("ctx");
            logger.LogError("boom");
        }

        recorder.Messages.Should().ContainInOrder("ctx", "boom");
        recorder.Messages.Count(m => m == "ctx").Should().Be(1);
        recorder.Messages.Count(m => m == "boom").Should().Be(1);
    }

    /// <summary>
    /// The host's live <c>LogLevel</c> stays authoritative (no override): a flush-level record with no
    /// active scope follows the configured filter exactly, so an error below a configured <c>Default</c>
    /// rule is suppressed. Inside a scope, the same error dumps the buffered context directly to the
    /// provider, bypassing that filter.
    /// </summary>
    [Fact]
    public void NoScopeErrorHonorsHostFilterButScopedErrorDumps()
    {
        var recorder = new RecordingProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddProvider(recorder);
            builder.AddBufferingLogging();
        });

        // A configured Default rule at Critical suppresses Error live.
        services.Configure<LoggerFilterOptions>(o =>
            o.Rules.Add(new LoggerFilterRule(null, null, LogLevel.Critical, null)));

        using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<BufferingLoggingServiceCollectionExtensionsTests>>();

        // No scope: nothing to dump, so the host filter is authoritative and the error is suppressed.
        logger.LogError("boom-no-scope");
        recorder.Messages.Should().NotContain("boom-no-scope");

        // Active scope: the buffered context and the triggering error are replayed directly to the
        // provider, bypassing the configured filter.
        using (LogBuffer.BeginScope())
        {
            logger.LogDebug("ctx");
            logger.LogError("boom");
        }

        recorder.Messages.Should().ContainInOrder("ctx", "boom");
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
