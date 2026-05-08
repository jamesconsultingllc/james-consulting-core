using System;
using System.Data.Common;
using FluentAssertions;
using JamesConsulting.Data.Common;
using Xunit;

namespace JamesConsulting.Tests.Data.Common;

/// <summary>
/// Unit tests for the <see cref="DbConnectionStringBuilderExtensions" /> class.
/// </summary>
public class DbConnectionStringBuilderExtensionTests
{
    /// <summary>
    /// Removes the specified keys from the connection string.
    /// </summary>
    [Fact]
    public void RemoveKeysFromConnectionString()
    {
        var dbConnectionStringBuilder = new DbConnectionStringBuilder
        {
            ConnectionString = "Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;"
        };

        dbConnectionStringBuilder.ConnectionString.Contains("myPassword").Should().BeTrue();

        dbConnectionStringBuilder.RemoveKeys("Password");

        dbConnectionStringBuilder.ConnectionString.Contains("myPassword").Should().BeFalse();
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException" /> when the keys parameter is empty.
    /// </summary>
    [Fact]
    public void RemoveKeysThrowsArgumentExceptionWhenKeysIsEmpty()
    {
        var exception = Assert.Throws<ArgumentException>(() => new DbConnectionStringBuilder().RemoveKeys());
        exception.ParamName.Should().Be("keys");
    }

    /// <summary>
    /// Throws an <see cref="ArgumentNullException" /> when the <see cref="DbConnectionStringBuilder" /> is null.
    /// </summary>
    [Fact]
    public void RemoveKeysThrowsArgumentNullExceptionWhenDbConnectionStringBuilderIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => default(DbConnectionStringBuilder)!.RemoveKeys());
        exception.ParamName.Should().Be("connectionStringBuilder");
    }

    /// <summary>
    /// Throws an <see cref="ArgumentNullException" /> when the keys parameter is null.
    /// </summary>
    [Fact]
    public void RemoveKeysThrowsArgumentNullExceptionWhenKeysIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new DbConnectionStringBuilder().RemoveKeys(null!));
        exception.ParamName.Should().Be("keys");
    }
}