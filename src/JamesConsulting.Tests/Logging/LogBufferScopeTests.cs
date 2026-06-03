using System.Linq;
using FluentAssertions;
using JamesConsulting.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace JamesConsulting.Tests.Logging;

/// <summary>
/// Unit tests for <see cref="LogBufferScope" /> lifecycle and <see cref="LogBuffer" /> ambient
/// scope management, driven through the public buffering API.
/// </summary>
public class LogBufferScopeTests
{
    private readonly RecordingLogger inner = new();
    private readonly BufferingLogger logger;

    public LogBufferScopeTests() => logger = new BufferingLogger(inner, new BufferingLoggerOptions());

    /// <summary>
    /// <see cref="LogBuffer.BeginScope()" /> uses the default capacity and becomes the current scope.
    /// </summary>
    [Fact]
    public void BeginScopeSetsCurrentWithDefaultCapacity()
    {
        using var scope = LogBuffer.BeginScope();

        LogBuffer.Current.Should().BeSameAs(scope);
        scope.Capacity.Should().Be(LogBufferScope.DefaultCapacity);
        scope.Count.Should().Be(0);
        scope.IsFlushed.Should().BeFalse();
        scope.IsDisposed.Should().BeFalse();
    }

    /// <summary>
    /// Disposing the scope clears the ambient slot and marks the scope disposed.
    /// </summary>
    [Fact]
    public void DisposeClearsCurrent()
    {
        var scope = LogBuffer.BeginScope();
        scope.Dispose();

        LogBuffer.Current.Should().BeNull();
        scope.IsDisposed.Should().BeTrue();
    }

    /// <summary>
    /// The ring buffer drops the oldest records once capacity is exceeded.
    /// </summary>
    [Fact]
    public void OverflowDropsOldest()
    {
        using (LogBuffer.BeginScope(2))
        {
            logger.LogDebug("one");
            logger.LogDebug("two");
            logger.LogDebug("three");
            logger.LogError("boom");
        }

        inner.Records.Where(r => r.Level == LogLevel.Debug).Select(r => r.Message)
            .Should().Equal("two", "three");
    }

    /// <summary>
    /// Disposing without flushing discards the buffered records.
    /// </summary>
    [Fact]
    public void DisposeWithoutFlushDiscardsBuffer()
    {
        using (LogBuffer.BeginScope())
        {
            logger.LogDebug("debug");
        }

        inner.Records.Should().BeEmpty();
    }

    /// <summary>
    /// A manual <see cref="LogBufferScope.Flush" /> dumps buffered records in order and marks the
    /// scope flushed.
    /// </summary>
    [Fact]
    public void ManualFlushDumpsInOrder()
    {
        using var scope = LogBuffer.BeginScope();
        logger.LogDebug("a");
        logger.LogDebug("b");

        scope.Flush();

        inner.Records.Select(r => r.Message).Should().Equal("a", "b");
        scope.IsFlushed.Should().BeTrue();
        scope.Count.Should().Be(0);
    }

    /// <summary>
    /// Flushing twice is safe and does not re-emit records.
    /// </summary>
    [Fact]
    public void FlushIsIdempotent()
    {
        using var scope = LogBuffer.BeginScope();
        logger.LogDebug("a");

        scope.Flush();
        scope.Flush();

        inner.Records.Should().ContainSingle();
    }

    /// <summary>
    /// A manual <see cref="LogBufferScope.Flush" /> on an empty buffer is a no-op and does not
    /// suspend buffering, so subsequent buffer-range records are still captured.
    /// </summary>
    [Fact]
    public void EmptyManualFlushDoesNotSuspendBuffering()
    {
        using var scope = LogBuffer.BeginScope();

        scope.Flush();

        scope.IsFlushed.Should().BeFalse();

        logger.LogDebug("after");

        scope.Count.Should().Be(1);
        inner.Records.Should().BeEmpty();
    }

    /// <summary>
    /// Nested scopes restore their parent on disposal.
    /// </summary>
    [Fact]
    public void NestedScopesRestoreParent()
    {
        using var outer = LogBuffer.BeginScope();
        using (var innerScope = LogBuffer.BeginScope())
        {
            LogBuffer.Current.Should().BeSameAs(innerScope);
        }

        LogBuffer.Current.Should().BeSameAs(outer);
    }

    /// <summary>
    /// Records buffered in a nested scope are independent of the parent scope.
    /// </summary>
    [Fact]
    public void NestedScopeBuffersIndependently()
    {
        using var outer = LogBuffer.BeginScope();
        logger.LogDebug("outer");

        using (LogBuffer.BeginScope())
        {
            logger.LogDebug("inner");
            outer.Count.Should().Be(1);
            LogBuffer.Current!.Count.Should().Be(1);
        }
    }

    /// <summary>
    /// A non-positive capacity is rejected.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BeginScopeRejectsNonPositiveCapacity(int capacity)
    {
        var act = () => LogBuffer.BeginScope(capacity);

        act.Should().Throw<System.ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Disposing a scope twice is safe.
    /// </summary>
    [Fact]
    public void DoubleDisposeIsSafe()
    {
        var scope = LogBuffer.BeginScope();

        scope.Dispose();
        var act = scope.Dispose;

        act.Should().NotThrow();
        scope.IsDisposed.Should().BeTrue();
    }

    /// <summary>
    /// Records logged after the scope is disposed are dropped rather than buffered.
    /// </summary>
    [Fact]
    public void LoggingAfterDisposeDropsRecords()
    {
        var scope = LogBuffer.BeginScope();
        scope.Dispose();

        logger.LogDebug("late");
        scope.Flush();

        inner.Records.Should().BeEmpty();
    }

    /// <summary>
    /// A non-structured buffered state is captured and replayed as its message.
    /// </summary>
    [Fact]
    public void NonStructuredStateIsReplayedAsMessage()
    {
        using var scope = LogBuffer.BeginScope();
        logger.Log(LogLevel.Debug, default, "plain-state", null, (s, _) => s);

        scope.Flush();

        inner.Records.Should().ContainSingle().Which.Message.Should().Be("plain-state");
    }
}
