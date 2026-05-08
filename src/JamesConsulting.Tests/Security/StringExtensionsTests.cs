using FluentAssertions;
using JamesConsulting.Security;
using Xunit;

namespace JamesConsulting.Tests.Security;

/// <summary>
/// The string extensions tests.
/// </summary>
public class StringExtensionsTests
{
    /// <summary>
    /// The to secure string test.
    /// </summary>
    [Fact]
    public void ToSecureStringTest()
    {
        var secureString = "test".ToSecureString();
        secureString.Length.Should().Be(4);
        secureString.ConvertToString().Should().Be("test");
    }

    [Theory]
    [InlineData(default)]
    [InlineData("")]
    public void ToSecureString(string? value)
    {
        var secureString = value!.ToSecureString();
        secureString.Length.Should().Be(0);
        secureString.ConvertToString().Should().Be("");
    }
}