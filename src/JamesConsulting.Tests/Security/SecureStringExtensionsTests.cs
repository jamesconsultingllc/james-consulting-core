using System;
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

    /// <summary>
    /// 2.0 breaking change: <see cref="SecureStringExtensions.ConvertToString"/> now
    /// throws <see cref="ArgumentNullException"/> for a null receiver via
    /// <c>Guard.NotNull</c>, instead of the prior <see cref="NullReferenceException"/>
    /// raised when the runtime dereferenced <c>secureString.Length</c>. This regression
    /// test pins the contract so the guard cannot be silently removed.
    /// </summary>
    [Fact]
    public void ConvertToString_NullReceiver_ThrowsArgumentNullException()
    {
        SecureString? secureString = null;
        Action act = () => secureString!.ConvertToString();
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("secureString");
    }
}