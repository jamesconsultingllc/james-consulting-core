using System;
using FluentAssertions;
using Xunit;

namespace JamesConsulting.Tests;

/// <summary>
/// The byte array extensions tests.
/// </summary>
public class ByteArrayExtensionsTests
{
    /// <summary>
    /// The get string empty array returns empty string.
    /// </summary>
    [Fact]
    public void GetStringEmptyArrayReturnsEmptyString()
    {
        var bytes = Array.Empty<byte>();
        bytes.GetString().Should().BeEmpty();
    }

    /// <summary>
    /// The get string null array throws argument null exception.
    /// </summary>
    [Fact]
    public void GetStringNullArrayThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => default(byte[])!.GetString());
    }

    [Fact]
    public void GetStringReturnsStringFromBytes()
    {
        "Test".GetBytes().GetString().Should().Be("Test");
    }

    /// <summary>
    /// UTF-16 requires exactly two bytes per <see cref="char"/>. Odd-length byte arrays
    /// cannot be valid UTF-16 representations and must be rejected with a clear
    /// <see cref="ArgumentException"/> instead of a low-level
    /// <see cref="ArgumentException"/> from <see cref="Buffer.BlockCopy"/>.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    public void GetStringOddLengthArrayThrowsArgumentException(int length)
    {
        var bytes = new byte[length];
        var ex = Assert.Throws<ArgumentException>(() => bytes.GetString());
        ex.ParamName.Should().Be("bytes");
    }
}