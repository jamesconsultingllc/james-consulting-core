using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using JamesConsulting.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace JamesConsulting.Tests.Logging;

/// <summary>
/// Unit tests for <see cref="BufferingLogger" /> routing behaviour, including dump-on-error.
/// </summary>
public class BufferingLoggerTests
{
    private readonly RecordingLogger inner = new();

    private BufferingLogger CreateLogger(Action<BufferingLoggerOptions>? configure = null)
    {
        var options = new BufferingLoggerOptions();
        configure?.Invoke(options);
        return new BufferingLogger(inner, options);
    }

    /// <summary>
    /// A null inner logger is rejected.
    /// </summary>
    [Fact]
    public void ConstructorNullInnerThrows()
    {
        var act = () => new BufferingLogger(null!, new BufferingLoggerOptions());

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Null options are rejected.
    /// </summary>
    [Fact]
    public void ConstructorNullOptionsThrows()
    {
        var act = () => new BufferingLogger(inner, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Invalid options are rejected by the constructor.
    /// </summary>
    [Fact]
    public void ConstructorInvalidOptionsThrows()
    {
        var options = new BufferingLoggerOptions { FlushLevel = LogLevel.None };

        var act = () => new BufferingLogger(inner, options);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Passthrough-level records are written through immediately, even without a scope.
    /// </summary>
    [Fact]
    public void InformationIsWrittenLive()
    {
        var logger = CreateLogger();

        logger.LogInformation("hello");

        inner.Records.Should().ContainSingle().Which.Message.Should().Be("hello");
    }

    /// <summary>
    /// Buffer-range records are dropped when no scope is active.
    /// </summary>
    [Fact]
    public void DebugWithoutScopeIsDropped()
    {
        var logger = CreateLogger();

        logger.LogDebug("debug");

        inner.Records.Should().BeEmpty();
    }

    /// <summary>
    /// Buffer-range records are held until a flush occurs.
    /// </summary>
    [Fact]
    public void DebugWithinScopeIsBufferedNotWritten()
    {
        var logger = CreateLogger();

        using var scope = LogBuffer.BeginScope();
        logger.LogDebug("debug");

        inner.Records.Should().BeEmpty();
        scope.Count.Should().Be(1);
    }

    /// <summary>
    /// An error dumps the buffered context first, in order, then writes the triggering record.
    /// </summary>
    [Fact]
    public void ErrorDumpsBufferedContextThenError()
    {
        var logger = CreateLogger();

        using (LogBuffer.BeginScope())
        {
            logger.LogDebug("first");
            logger.LogDebug("second");
            logger.LogInformation("info");
            logger.LogError("boom");
        }

        inner.Records.Select(r => r.Message).Should()
            .ContainInOrder("info", "first", "second", "boom");
        inner.Records.Select(r => r.Level).Should()
            .ContainInOrder(LogLevel.Information, LogLevel.Debug, LogLevel.Debug, LogLevel.Error);
    }

    /// <summary>
    /// Records below the buffer level are always dropped, even inside a scope.
    /// </summary>
    [Fact]
    public void BelowBufferLevelIsDropped()
    {
        var logger = CreateLogger(o => o.BufferLevel = LogLevel.Debug);

        using var scope = LogBuffer.BeginScope();
        logger.LogTrace("trace");
        logger.LogError("boom");

        inner.Records.Select(r => r.Message).Should().Equal("boom");
    }

    /// <summary>
    /// After a flush, buffer-range records are written live when suspend-after-flush is enabled.
    /// </summary>
    [Fact]
    public void SuspendAfterFlushWritesSubsequentDebugLive()
    {
        var logger = CreateLogger(o => o.SuspendBufferingAfterFlush = true);

        using (LogBuffer.BeginScope())
        {
            logger.LogError("boom");
            logger.LogDebug("after");
        }

        inner.Records.Select(r => r.Message).Should().Equal("boom", "after");
    }

    /// <summary>
    /// When suspend-after-flush is disabled, the scope resumes buffering and only dumps on the next error.
    /// </summary>
    [Fact]
    public void NoSuspendAfterFlushResumesBuffering()
    {
        var logger = CreateLogger(o => o.SuspendBufferingAfterFlush = false);

        using (LogBuffer.BeginScope())
        {
            logger.LogError("boom1");
            logger.LogDebug("after");

            inner.Records.Select(r => r.Message).Should().Equal("boom1");

            logger.LogError("boom2");
        }

        inner.Records.Select(r => r.Message).Should().Equal("boom1", "after", "boom2");
    }

    /// <summary>
    /// <see cref="LogLevel.None" /> is never written.
    /// </summary>
    [Fact]
    public void NoneIsIgnored()
    {
        var logger = CreateLogger();

        logger.Log(LogLevel.None, default, "x", null, (s, _) => s);

        inner.Records.Should().BeEmpty();
    }

    /// <summary>
    /// A null formatter is rejected.
    /// </summary>
    [Fact]
    public void LogNullFormatterThrows()
    {
        var logger = CreateLogger();

        var act = () => logger.Log<string>(LogLevel.Information, default, "x", null, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Buffered structured state is preserved on replay.
    /// </summary>
    [Fact]
    public void BufferedStructuredStateIsPreservedOnReplay()
    {
        var logger = CreateLogger();

        using (LogBuffer.BeginScope())
        {
            logger.LogDebug("order {OrderId}", 42);
            logger.LogError("boom");
        }

        var replayed = inner.Records.First(r => r.Level == LogLevel.Debug);
        replayed.State.Should().NotBeNull();
        replayed.State!.Should().Contain(kv => kv.Key == "OrderId" && Equals(kv.Value, 42));
    }

    /// <summary>
    /// <see cref="ILogger.IsEnabled" /> returns false for buffer-range levels when no scope is active.
    /// </summary>
    [Fact]
    public void IsEnabledDebugFalseWithoutScope()
    {
        var logger = CreateLogger();

        logger.IsEnabled(LogLevel.Debug).Should().BeFalse();
    }

    /// <summary>
    /// <see cref="ILogger.IsEnabled" /> returns true for buffer-range levels inside an active scope.
    /// </summary>
    [Fact]
    public void IsEnabledDebugTrueWithinScope()
    {
        var logger = CreateLogger();

        using var scope = LogBuffer.BeginScope();

        logger.IsEnabled(LogLevel.Debug).Should().BeTrue();
    }

    /// <summary>
    /// <see cref="ILogger.IsEnabled" /> is false for <see cref="LogLevel.None" />.
    /// </summary>
    [Fact]
    public void IsEnabledNoneFalse()
    {
        var logger = CreateLogger();

        logger.IsEnabled(LogLevel.None).Should().BeFalse();
    }

    /// <summary>
    /// Flush-level records are always enabled so the dump can be triggered, even if the inner logger
    /// would filter them.
    /// </summary>
    [Fact]
    public void IsEnabledErrorTrueEvenWhenInnerDisabled()
    {
        // An inner logger whose threshold is None is never enabled for any real level.
        var quietInner = new RecordingLogger(LogLevel.None);
        var logger = new BufferingLogger(quietInner, new BufferingLoggerOptions());

        quietInner.IsEnabled(LogLevel.Error).Should().BeFalse();
        logger.IsEnabled(LogLevel.Error).Should().BeTrue();
    }

    /// <summary>
    /// <see cref="ILogger.BeginScope{TState}" /> is delegated to the inner logger.
    /// </summary>
    [Fact]
    public void BeginScopeDelegatesToInner()
    {
        var logger = CreateLogger();

        using var scope = logger.BeginScope("state");

        scope.Should().NotBeNull();
    }

    /// <summary>
    /// Concurrent buffer-range logging that shares a single ambient scope is thread-safe and never
    /// throws, and the triggering error is always written.
    /// </summary>
    [Fact]
    public void ConcurrentLoggingIsThreadSafe()
    {
        var logger = CreateLogger();

        using (LogBuffer.BeginScope(256))
        {
            System.Threading.Tasks.Parallel.For(0, 1000, i => logger.LogDebug("n {N}", i));
            logger.LogError("boom");
        }

        inner.Records.Should().Contain(r => r.Level == LogLevel.Error && r.Message == "boom");
        // The ring buffer is bounded, so at most capacity debug records are dumped.
        inner.Records.Count(r => r.Level == LogLevel.Debug).Should().BeLessThanOrEqualTo(256);
    }

    /// <summary>
    /// An error with an empty buffer still suspends buffering for the rest of the scope, so a
    /// subsequent buffer-range record is written live rather than re-buffered.
    /// </summary>
    [Fact]
    public void EmptyErrorFlushSuspendsBuffering()
    {
        var logger = CreateLogger();

        using var scope = LogBuffer.BeginScope();
        logger.LogError("boom");
        logger.LogDebug("after");

        scope.IsFlushed.Should().BeTrue();
        inner.Records.Select(r => r.Message).Should().Equal("boom", "after");
    }

    /// <summary>
    /// A nested error dumps the whole live scope chain in chronological order (ancestors first), but
    /// only suspends the innermost scope; ancestors are dumped for context yet keep buffering
    /// afterward (they are not marked flushed), so records logged into the ancestor after the nested
    /// error are still captured.
    /// </summary>
    [Fact]
    public void NestedErrorDumpsChronologicallyAndLeavesAncestorBuffering()
    {
        var logger = CreateLogger();

        using (var outer = LogBuffer.BeginScope())
        {
            logger.LogDebug("a1");

            using (LogBuffer.BeginScope())
            {
                logger.LogDebug("b1");
                logger.LogError("boom");
            }

            // The ancestor was dumped for context but not suspended.
            outer.IsFlushed.Should().BeFalse();

            logger.LogDebug("a2");
            outer.Count.Should().Be(1);
        }

        // a2 is discarded when the outer scope disposes without its own error.
        inner.Records.Select(r => r.Message).Should().Equal("a1", "b1", "boom");
    }

    /// <summary>
    /// Object-valued structured properties are frozen to their log-time text, so mutating the source
    /// object before the dump does not change what the replayed record reports.
    /// </summary>
    [Fact]
    public void ObjectValuedStateIsFrozenAtLogTime()
    {
        var logger = CreateLogger();
        var holder = new MutableValue { Text = "before" };

        using (LogBuffer.BeginScope())
        {
            logger.LogDebug("state {Holder}", holder);
            holder.Text = "after";
            logger.LogError("boom");
        }

        var replayed = inner.Records.First(r => r.Level == LogLevel.Debug);
        replayed.State!.Should().Contain(kv => kv.Key == "Holder" && Equals(kv.Value, "before"));
    }

    /// <summary>
    /// A throwing <see cref="object.ToString" /> on a buffered value does not propagate into the
    /// caller's logging path; the value is frozen to a fallback instead.
    /// </summary>
    [Fact]
    public void ThrowingToStringOnBufferedValueDoesNotPropagate()
    {
        var logger = CreateLogger();

        using var scope = LogBuffer.BeginScope();

        var act = () => logger.LogDebug("bad {X}", new ThrowingToString());

        act.Should().NotThrow();
    }

    /// <summary>
    /// A faulty sink that throws while replaying one buffered record does not break the dump: the
    /// remaining buffered records and the triggering error are still written.
    /// </summary>
    [Fact]
    public void FaultyReplaySinkDoesNotBreakDump()
    {
        inner.ThrowOn = (level, message) => level == LogLevel.Debug && message == "a";
        var logger = CreateLogger();

        using (LogBuffer.BeginScope())
        {
            logger.LogDebug("a");
            logger.LogDebug("b");
            logger.LogError("boom");
        }

        inner.Records.Select(r => r.Message).Should().Equal("b", "boom");
    }

    /// <summary>
    /// A structured state whose enumerator throws is snapshotted defensively: the buffer-path render
    /// does not propagate, and the record is replayed with its rendered message.
    /// </summary>
    [Fact]
    public void ThrowingStructuredStateDoesNotPropagate()
    {
        var logger = CreateLogger();

        using (LogBuffer.BeginScope())
        {
            var act = () => logger.Log(
                LogLevel.Debug,
                default,
                new ThrowingEnumerableState(),
                null,
                static (s, _) => s.ToString());

            act.Should().NotThrow();

            logger.LogError("boom");
        }

        inner.Records.Select(r => r.Message).Should().Equal("rendered", "boom");
    }

    private sealed class ThrowingEnumerableState : IReadOnlyList<KeyValuePair<string, object?>>
    {
        public int Count => throw new InvalidOperationException("nope");

        public KeyValuePair<string, object?> this[int index] => throw new InvalidOperationException("nope");

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() =>
            throw new InvalidOperationException("nope");

        IEnumerator IEnumerable.GetEnumerator() => throw new InvalidOperationException("nope");

        public override string ToString() => "rendered";
    }

    private sealed class MutableValue
    {
        public string Text { get; set; } = string.Empty;

        public override string ToString() => Text;
    }

    private sealed class ThrowingToString
    {
        public override string ToString() => throw new InvalidOperationException("nope");
    }
}
