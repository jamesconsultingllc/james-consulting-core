using System.Security;
using FluentAssertions;
using JamesConsulting.Security;
using Xunit;

namespace JamesConsulting.Tests.Security;

/// <summary>
/// The secure string extensions tests.
/// </summary>
public class SecureStringExtensionsTests
{
    /// <summary>
    /// The to string test.
    /// </summary>
    [Fact]
    public void ToStringTest()
    {
        SecureString secureString = new();
        secureString.AppendChar('t');
        secureString.AppendChar('e');
        secureString.AppendChar('s');
        secureString.AppendChar('t');

        secureString.ConvertToString().Should().Be("test");
    }
}