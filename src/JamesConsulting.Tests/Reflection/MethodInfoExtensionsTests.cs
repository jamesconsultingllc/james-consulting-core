using System;
using System.Reflection;
using FluentAssertions;
using JamesConsulting.Reflection;
using Xunit;

namespace JamesConsulting.Tests.Reflection;

/// <summary>
/// The method info extensions tests.
/// </summary>
public class MethodInfoExtensionsTests
{
    [Fact]
    public void ToInvocationStringReadsCachedValue()
    {
        var methodInfo = typeof(string).GetMethod("Insert", new[] { typeof(int), typeof(string) });
        methodInfo?.ToInvocationString(3, "testing");
        var actualResult = methodInfo?.ToInvocationString(3, "testing");
        actualResult.Should()
            .Be("System.String.Insert(System.Int32 startIndex : 3, System.String value : \"testing\")");
    }

    /// <summary>
    /// The to invocation string succeeds.
    /// </summary>
    /// <exception cref="T:System.Reflection.AmbiguousMatchException">
    /// More than one method is found with the specified name and
    /// specified parameters.
    /// </exception>
    [Fact]
    public void ToInvocationStringSucceeds()
    {
        var methodInfo = typeof(string).GetMethod("Insert", new[] { typeof(int), typeof(string) });
        var actualResult = methodInfo?.ToInvocationString(3, "testing");
        actualResult.Should()
            .Be("System.String.Insert(System.Int32 startIndex : 3, System.String value : \"testing\")");
    }

    /// <summary>
    /// The to invocation throws argument null exception when method info is null.
    /// </summary>
    [Fact]
    public void ToInvocationThrowsArgumentNullExceptionWhenMethodInfoIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => default(MethodInfo)!.ToInvocationString(default!));
    }
}