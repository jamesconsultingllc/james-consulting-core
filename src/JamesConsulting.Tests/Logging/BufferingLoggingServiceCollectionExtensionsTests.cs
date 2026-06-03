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
    /// End to end: a resolved <see cref="ILogger{T}" /> buffers Debug logs and dumps them when an
    /// error is logged within a scope.
    /// </summary>
    [Fact]
    public void ResolvedLoggerDumpsOnErrorWithinScope()
    {
        var recorder = new RecordingProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
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
            builder.SetMinimumLevel(LogLevel.Trace);
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
            builder.SetMinimumLevel(LogLevel.Trace);
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
    /// With <see cref="BufferingLoggerOptions.ConfigureUnderlyingFilter" /> enabled (the default), the
    /// auto-appended buffer-level rule wins over a configuration-bound <c>Default</c> rule, so a
    /// dumped Debug record still reaches the provider without any manual filter setup.
    /// </summary>
    [Fact]
    public void ConfigureUnderlyingFilterLetsDumpBeatConfiguredDefault()
    {
        var recorder = new RecordingProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddProvider(recorder);
            builder.AddBufferingLogging();
        });

        // Simulate a configuration-bound Logging:LogLevel:Default = Information rule. A Configure
        // callback runs before the extension's PostConfigure, so our buffer-level rule is appended
        // last and wins the tie-break.
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
    }

    /// <summary>
    /// With <see cref="BufferingLoggerOptions.ConfigureUnderlyingFilter" /> disabled and an inner
    /// filter that excludes the buffer level, the dump is filtered out but the triggering error is
    /// still written and the logger warns exactly once about the misconfiguration.
    /// </summary>
    [Fact]
    public void DisablingConfigureUnderlyingFilterFiltersDumpAndWarnsOnce()
    {
        var recorder = new RecordingProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(recorder);
            builder.AddBufferingLogging(o => o.ConfigureUnderlyingFilter = false);
        });

        using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<BufferingLoggingServiceCollectionExtensionsTests>>();

        using (LogBuffer.BeginScope())
        {
            logger.LogDebug("ctx1");
            logger.LogError("boom1");
        }

        using (LogBuffer.BeginScope())
        {
            logger.LogDebug("ctx2");
            logger.LogError("boom2");
        }

        recorder.Messages.Should().NotContain("ctx1");
        recorder.Messages.Should().NotContain("ctx2");
        recorder.Messages.Should().Contain("boom1");
        recorder.Messages.Should().Contain("boom2");
        recorder.Messages.Count(m => m.Contains("are filtered out by the underlying logging configuration")).Should().Be(1);
    }

    /// <summary>
    /// The dump-filtered backstop warning only fires when a buffering scope is active. A bare
    /// flush-level record outside any scope dumps nothing, so it must not raise the warning or burn
    /// the once-only guard that a later real dump relies on.
    /// </summary>
    [Fact]
    public void DumpFilteredWarningDoesNotFireWithoutActiveScope()
    {
        var recorder = new RecordingProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(recorder);
            builder.AddBufferingLogging(o => o.ConfigureUnderlyingFilter = false);
        });

        using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<BufferingLoggingServiceCollectionExtensionsTests>>();

        // No LogBuffer scope: nothing is buffered, so the error dumps nothing.
        logger.LogError("boom-no-scope");

        recorder.Messages.Should().Contain("boom-no-scope");
        recorder.Messages.Should().NotContain(m => m.Contains("are filtered out by the underlying logging configuration"));

        // The guard was not consumed, so a subsequent real scoped dump still warns.
        using (LogBuffer.BeginScope())
        {
            logger.LogDebug("ctx");
            logger.LogError("boom");
        }

        recorder.Messages.Count(m => m.Contains("are filtered out by the underlying logging configuration")).Should().Be(1);
    }

    /// <summary>
    /// The auto-appended no-category rule does not override a more specific category rule, so a
    /// category that is filtered above the buffer level still loses its dump — and the logger warns
    /// once about it. This locks in the documented limitation.
    /// </summary>
    [Fact]
    public void ConfigureUnderlyingFilterDoesNotBeatCategoryRuleAndWarnsOnce()
    {
        var recorder = new RecordingProvider();
        var services = new ServiceCollection();
        var category = typeof(BufferingLoggingServiceCollectionExtensionsTests).FullName!;
        services.AddLogging(builder =>
        {
            builder.AddProvider(recorder);
            builder.AddBufferingLogging();
        });

        // A category-specific rule at Information is more specific than our null/null buffer rule.
        services.Configure<LoggerFilterOptions>(o =>
            o.Rules.Add(new LoggerFilterRule(null, category, LogLevel.Information, null)));

        using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<BufferingLoggingServiceCollectionExtensionsTests>>();

        using (LogBuffer.BeginScope())
        {
            logger.LogDebug("ctx1");
            logger.LogError("boom1");
        }

        using (LogBuffer.BeginScope())
        {
            logger.LogDebug("ctx2");
            logger.LogError("boom2");
        }

        recorder.Messages.Should().NotContain("ctx1");
        recorder.Messages.Should().NotContain("ctx2");
        recorder.Messages.Should().Contain("boom1");
        recorder.Messages.Count(m => m.Contains("are filtered out by the underlying logging configuration")).Should().Be(1);
    }

    /// <summary>
    /// When the underlying filter is higher than Warning, the backstop "dump filtered" warning is
    /// still visible because it is emitted at the flush level (which is written live).
    /// </summary>
    [Fact]
    public void DumpFilteredWarningIsVisibleAboveWarningThreshold()
    {
        var recorder = new RecordingProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Error);
            builder.AddProvider(recorder);
            builder.AddBufferingLogging(o => o.ConfigureUnderlyingFilter = false);
        });

        using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<BufferingLoggingServiceCollectionExtensionsTests>>();

        using (LogBuffer.BeginScope())
        {
            logger.LogDebug("ctx");
            logger.LogError("boom");
        }

        recorder.Messages.Should().NotContain("ctx");
        recorder.Messages.Should().Contain("boom");
        recorder.Messages.Count(m => m.Contains("are filtered out by the underlying logging configuration")).Should().Be(1);
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
