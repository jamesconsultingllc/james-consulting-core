using System;
using System.Net;
using FluentAssertions;
using JamesConsulting.Net;
using Xunit;

namespace JamesConsulting.Tests.Net;

public class ConnectToSharedFolderTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_ThrowsArgumentException_WhenNetworkCredentialsUsername_IsInvalid(string? userName)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ConnectToSharedFolder("Test", new NetworkCredential { UserName = userName }));
        exception.ParamName.Should().Be("credentials");
        exception.Message.Should().StartWith("UserName specified cannot be null or whitespace.");
    }

    [Fact]
    public void Constructor_Succeeds()
    {
        var instance = new ConnectToSharedFolder("Test", new NetworkCredential { UserName = "Test" });
        instance.Should().NotBeNull();
    }
}