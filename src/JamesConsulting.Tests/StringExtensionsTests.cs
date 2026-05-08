using System;
using FluentAssertions;
using Xunit;

namespace JamesConsulting.Tests;

/// <summary>
/// The string extensions tests.
/// </summary>
public class StringExtensionsTests
{
    /// <summary>
    /// The to title case.
    /// </summary>
    /// <param name="stringToConvertToTitleCase">
    /// The string to convert to title case.
    /// </param>
    /// <param name="titleCasedString">
    /// The title cased string.
    /// </param>
    /// <param name="expectedResult">
    /// The expected result.
    /// </param>
    [Theory]
    [InlineData("rudy james", "Rudy James", true)]
    [InlineData("rudy james", "Rudy james", false)]
    [InlineData("", "", true)]
    public void ToTitleCase(string stringToConvertToTitleCase, string titleCasedString, bool expectedResult)
    {
        var result = string.Equals(stringToConvertToTitleCase.ToTitleCase(), titleCasedString,
            StringComparison.Ordinal);
        result.Should().Be(expectedResult);
    }

    /// <summary>
    /// The truncate invalid length throws argument out of range exception.
    /// </summary>
    /// <param name="length">
    /// The length.
    /// </param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TruncateInvalidLengthThrowsArgumentOutOfRangeException(int length)
    {
        const string arg = "testing";
        Assert.Throws<ArgumentOutOfRangeException>(() => arg.Truncate(length));
    }

    /// <summary>
    /// The get bytes empty string returns empty byte array.
    /// </summary>
    [Fact]
    public void GetBytesEmptyStringReturnsEmptyByteArray()
    {
        var arg = string.Empty;
        arg.GetBytes().Should().BeEmpty();
    }

    /// <summary>
    /// The get bytes null argument.
    /// </summary>
    [Fact]
    public void GetBytesNullArgument()
    {
        Assert.Throws<ArgumentNullException>(() => default(string)!.GetBytes());
    }

    [Fact]
    public void GetBytesReturnsByteArray()
    {
        var bytes = "Test".GetBytes();
        bytes.Should().NotBeNull();
        bytes.Should().NotBeEmpty();
    }

    /// <summary>
    /// The to title case null argument.
    /// </summary>
    [Fact]
    public void ToTitleCaseNullArgument()
    {
        Assert.Throws<ArgumentNullException>(() => default(string)!.ToTitleCase());
    }

    /// <summary>
    /// The truncate empty string returns empty string.
    /// </summary>
    [Fact]
    public void TruncateEmptyStringReturnsEmptyString()
    {
        string.Empty.Truncate(100).Should().BeEmpty();
    }

    /// <summary>
    /// The truncate null string throws argument null exception.
    /// </summary>
    [Fact]
    public void TruncateNullStringThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => default(string)!.Truncate(0));
    }

    /// <summary>
    /// When length exceeds the input's length, Truncate must return the input unchanged.
    /// Previously this surface threw <see cref="ArgumentOutOfRangeException"/> from
    /// <see cref="string.Substring(int,int)"/>, which contradicted the documented
    /// "maximum number of characters to keep" semantics.
    /// </summary>
    [Theory]
    [InlineData("abc", 5, "abc")]
    [InlineData("abc", 3, "abc")]
    [InlineData("abc", 2, "ab")]
    public void TruncateLengthGreaterThanOrEqualToInputReturnsInput(string input, int length, string expected)
    {
        input.Truncate(length).Should().Be(expected);
    }
}