using System;
using FluentAssertions;
using JamesConsulting.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace JamesConsulting.Tests.Logging;

/// <summary>
/// Unit tests for <see cref="BufferingLoggerOptions" /> validation.
/// </summary>
public class BufferingLoggerOptionsTests
{
    /// <summary>
    /// The default options satisfy the required invariant.
    /// </summary>
    [Fact]
    public void ValidateDefaultsDoesNotThrow()
    {
        var options = new BufferingLoggerOptions();

        var act = options.Validate;

        act.Should().NotThrow();
    }

    /// <summary>
    /// Defaults match the documented values.
    /// </summary>
    [Fact]
    public void DefaultsAreExpectedValues()
    {
        var options = new BufferingLoggerOptions();

        options.BufferLevel.Should().Be(LogLevel.Trace);
        options.FlushLevel.Should().Be(LogLevel.Error);
        options.SuspendBufferingAfterFlush.Should().BeTrue();
    }

    /// <summary>
    /// Either threshold set to <see cref="LogLevel.None" /> is rejected.
    /// </summary>
    /// <param name="which">Which threshold to set to None.</param>
    [Theory]
    [InlineData("buffer")]
    [InlineData("flush")]
    public void ValidateRejectsNone(string which)
    {
        var options = new BufferingLoggerOptions();
        switch (which)
        {
            case "buffer":
                options.BufferLevel = LogLevel.None;
                break;
            default:
                options.FlushLevel = LogLevel.None;
                break;
        }

        var act = options.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// A flush level below the buffer level violates the invariant.
    /// </summary>
    [Fact]
    public void ValidateRejectsFlushBelowBuffer()
    {
        var options = new BufferingLoggerOptions
        {
            BufferLevel = LogLevel.Warning,
            FlushLevel = LogLevel.Information,
        };

        var act = options.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be(nameof(BufferingLoggerOptions.FlushLevel));
    }

    /// <summary>
    /// Equal thresholds are allowed (the invariant uses &lt;=).
    /// </summary>
    [Fact]
    public void ValidateAllowsEqualThresholds()
    {
        var options = new BufferingLoggerOptions
        {
            BufferLevel = LogLevel.Information,
            FlushLevel = LogLevel.Information,
        };

        var act = options.Validate;

        act.Should().NotThrow();
    }
}
