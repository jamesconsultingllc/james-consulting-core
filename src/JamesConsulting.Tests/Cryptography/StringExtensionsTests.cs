using System;
using System.Security.Cryptography;
using FluentAssertions;
using JamesConsulting.Cryptography;
using Xunit;

namespace JamesConsulting.Tests.Cryptography;

/// <summary>
/// The string extensions tests.
/// </summary>
public class StringExtensionsTests
{
    [Fact]
    public void HashInvalidTargetThrowsArgumentNullException()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() => JamesConsulting.Cryptography.StringExtensions.Hash(default!));
        exception.ParamName.Should().Be("target");
    }

    [Fact]
    public void HashEmptyTargetThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => string.Empty.Hash());
    }

    [Fact]
    public void HashInvalidNumberOfRoundsThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => "test".Hash(new byte[3], -100));
        exception.ParamName.Should().Be("numberOfRounds");
    }

    [Fact]
    public void HashReturnHashedStringWithSalt()
    {
        var (hashedString, salt) = "test".Hash();
        salt.Should().NotBeEmpty();
        hashedString.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void HashWithNullSalt()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => "test".Hash(default(byte[])!));
        exception.ParamName.Should().Be("salt");
    }

    [Fact]
    public void StringsHashedWithSameSaltShouldBeEqual()
    {
        const string test = "Rudy James";
        var salt = JamesConsulting.Cryptography.StringExtensions.GenerateSalt();
        var result = test.Hash(salt);
        var result2 = test.Hash(salt);
        result.Should().Be(result2);
    }

    [Fact]
    public void HashWithExplicitAlgorithmMatchesDefaultSha256()
    {
        var salt = JamesConsulting.Cryptography.StringExtensions.GenerateSalt();
        var defaultHash = "test".Hash(salt); // default SHA256
        var explicitHash = "test".Hash(salt, 100_000, HashAlgorithmName.SHA256);
        defaultHash.Should().Be(explicitHash);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    public void GenerateSaltWithSizeReturnsCorrectLength(int size)
    {
        var salt = JamesConsulting.Cryptography.StringExtensions.GenerateSalt(size);
        salt.Length.Should().Be(size);
    }

#pragma warning disable CS0618 // obsolete Base64 helpers intentionally exercised for migration coverage
    [Fact]
    public void Base64DecodeEmptyStringReturnsEmptyString()
    {
        string.Empty.Base64Decode().Should().BeEmpty();
    }

    [Fact]
    public void Base64DecodeNullThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => default(string)!.Base64Decode());
    }

    [Fact]
    public void Base64DecodeValidStringReturnsDecodedString()
    {
        "test".Base64Encode().Base64Decode().Should().Be("test");
    }

    [Fact]
    public void Base64EncodeEmptyStringReturnsEmptyString()
    {
        string.Empty.Base64Encode().Should().BeEmpty();
    }

    [Fact]
    public void Base64EncodeNullThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => default(string)!.Base64Encode());
    }

    [Fact]
    public void Base64EncodeValidStringReturnsEncodedString()
    {
        "test".Base64Encode().Should().NotBeNullOrWhiteSpace();
    }
#pragma warning restore CS0618
}