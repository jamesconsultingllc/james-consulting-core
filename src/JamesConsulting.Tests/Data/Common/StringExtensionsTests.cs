using System;
using FluentAssertions;
using JamesConsulting.Data.Common;
using Xunit;

namespace JamesConsulting.Tests.Data.Common;

/// <summary>
/// The string extensions tests.
/// </summary>
public class StringExtensionsTests
{
    /// <summary>
    /// The string password from connection string strips password entry.
    /// </summary>
    /// <param name="passwordVariation">
    /// The password variation.
    /// </param>
    [Theory]
    [InlineData("Password")]
    [InlineData("password")]
    [InlineData("Pwd")]
    [InlineData("pwd")]
    public void StringPasswordFromConnectionStringStripsPasswordEntry(string passwordVariation)
    {
        var connectionString =
            $"Server=Test;Database=Test; User ID=Test;{passwordVariation}=Test;Trusted_Connection=false; Connection Timeout=60;";
        connectionString.StripPasswordFromConnectionString().Should().NotContain($"{passwordVariation}=Test");
    }

    /// <summary>
    /// The strip password from connection string with empty connection string returns empty string.
    /// </summary>
    [Fact]
    public void StripPasswordFromConnectionStringWithEmptyConnectionStringReturnsEmptyString()
    {
        string.Empty.StripPasswordFromConnectionString().Should().BeEmpty();
    }

    /// <summary>
    /// Strip password from connection string with null connection string throws argument null exception.
    /// </summary>
    [Fact]
    public void StripPasswordFromConnectionStringWithNullConnectionStringThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => default(string)!.StripPasswordFromConnectionString());
    }
}