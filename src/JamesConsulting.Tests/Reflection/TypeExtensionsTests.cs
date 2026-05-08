using System;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using JamesConsulting.Reflection;
using Xunit;

namespace JamesConsulting.Tests.Reflection;

/// <summary>
/// The type extensions tests.
/// </summary>
public class TypeExtensionsTests
{
    /// <summary>
    /// The instance type.
    /// </summary>
    private static readonly Type InstanceType = typeof(MyInterface);

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void GetMethodInfoFromStringEmptyOrWhitespaceMethodThrowsArgumentException(string method)
    {
        Assert.Throws<ArgumentException>(() => typeof(string).GetMethodInfoFromString(method));
    }

    [Fact]
    public void GetMethodInfoFromStringEmptyOrWhitespaceMethodThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => typeof(string).GetMethodInfoFromString(default!));
    }

    /// <summary>
    /// The to method info throws argument exception invalid string.
    /// </summary>
    /// <param name="test">
    /// The test.
    /// </param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ToMethodInfoThrowsArgumentExceptionInvalidString(string test)
    {
        Assert.Throws<ArgumentException>(() => InstanceType.GetMethodInfoFromString(test));
    }

    [Theory]
    [InlineData("Test")]
    [InlineData("TestAsync")]
    public void IsAsyncWithResultReturnsFalseWhenMethodIsNotAsync(string method)
    {
        InstanceType.GetMethod(method)!.IsAsyncWithResult().Should().BeFalse();
    }

    /// <summary>
    /// The get method info from string null type throws argument null exception.
    /// </summary>
    [Fact]
    public void GetMethodInfoFromStringNullTypeThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => default(Type)!.GetMethodInfoFromString(""));
    }

    [Fact]
    public void GetMethodInfoFromStringReturnsCachedMethodOnSubsequentRequests()
    {
        var signature = InstanceType.GetMethods().First(x => x.Name == "GetClassById").ToString()!;
        var result1 = InstanceType.GetMethodInfoFromString(signature);
        var result2 = InstanceType.GetMethodInfoFromString(signature);
        result1.Should().NotBeNull();
        result2.Should().BeSameAs(result1);
    }

    [Fact]
    public void GetMethodInfoFromStringReturnsMethodInfo()
    {
        var expected = InstanceType.GetMethods().First(x => x.Name == "GetClassById");
        InstanceType.GetMethodInfoFromString(expected.ToString()!).Should().BeSameAs(expected);
    }

    [Fact]
    public void GetMethodInfoFromStringReturnsNullIfMethodIsNotPartOfClass()
    {
        GetType().GetMethodInfoFromString("Rudy").Should().BeNull();
    }

    [Fact]
    public void HasReturnValueReturnsFalseWhenMethodHasReturnTypeTask()
    {
        InstanceType.GetMethod("TestAsync")!.HasReturnValue().Should().BeFalse();
    }

    [Fact]
    public void HasReturnValueReturnsFalseWhenMethodHasReturnTypeVoid()
    {
        InstanceType.GetMethod("Test")!.HasReturnValue().Should().BeFalse();
    }

    [Fact]
    public void HasReturnValueReturnsTrueWhenMethodHasReturnValue()
    {
        var expected = InstanceType.GetMethods().First(x => x.Name == "GetClassById");
        InstanceType.GetMethod(expected.Name)!.HasReturnValue().Should().BeTrue();
    }

    [Fact]
    public void HasReturnValueThrowsArgumentNullExceptionWhenMethodInfoIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => default(MethodInfo)!.HasReturnValue());
    }

    [Fact]
    public void IsAsyncReturnsFalseWhenMethodIsNotAsync()
    {
        InstanceType.GetMethod("Test")!.IsAsync().Should().BeFalse();
    }

    [Fact]
    public void IsAsyncReturnsTrueWhenMethodIsAsync()
    {
        InstanceType.GetMethod("TestAsync")!.IsAsync().Should().BeTrue();
    }

    [Fact]
    public void IsAsyncThrowsArgumentNullExceptionWhenMethodInfoIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => default(MethodInfo)!.IsAsync());
    }

    [Fact]
    public void IsAsyncWithResultReturnsTrueWhenMethodIsAsync()
    {
        InstanceType.GetMethod("GetClassById")!.IsAsyncWithResult().Should().BeTrue();
    }

    [Fact]
    public void IsAsyncWithResultThrowsArgumentNullExceptionWhenMethodInfoIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => default(MethodInfo)!.IsAsyncWithResult());
    }

    [Fact]
    public void IsConcreteClassAbstractClassTypeReturnsFalse()
    {
        typeof(DbConnection).IsConcreteClass().Should().BeFalse();
    }

    [Fact]
    public void IsConcreteClassConcreteClassTypeReturnsTrue()
    {
        GetType().IsConcreteClass().Should().BeTrue();
    }

    [Fact]
    public void IsConcreteClassInterfaceTypeReturnsFalse()
    {
        typeof(ICloneable).IsConcreteClass().Should().BeFalse();
    }

    [Fact]
    public void IsConcreteClassThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => default(Type)!.IsConcreteClass());
    }

    /// <summary>
    /// The to method info returns method info from method name.
    /// </summary>
    [Fact]
    public void ToMethodInfoReturnsMethodInfoFromMethodName()
    {
        var expected = InstanceType.GetMethods().First(x => x.Name == "GetClassById");
        var actual = InstanceType.GetMethodInfoFromString(expected.ToString()!);
        actual.Should().BeSameAs(expected);
    }

    /// <summary>
    /// The to method info throws argument null exception null string.
    /// </summary>
    [Fact]
    public void ToMethodInfoThrowsArgumentNullExceptionNullString()
    {
        Assert.Throws<ArgumentNullException>(() => InstanceType.GetMethodInfoFromString(default!));
    }
}