using System;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using JamesConsulting.Threading;
using Xunit;

namespace JamesConsulting.Tests.Threading;

public class MethodInfoExtensionsTests
{
    private static readonly Type InstanceType = typeof(MyInterface);

    [Fact]
    public async Task CreateTaskResultReturnsTaskResult()
    {
        var taskResult = InstanceType.GetMethod("GetClassById")!.CreateTaskResult(new MyClass { X = 1 });
        taskResult.Should().BeOfType<Task<MyClass>>();
        var result = await (taskResult as Task<MyClass>)!;
        result.X.Should().Be(1);
    }

    [Fact]
    public void CreateTaskResultReturnTypeNullThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => InstanceType.GetMethod("Test")!.CreateTaskResult(default!));
    }

    [Fact]
    public void CreateTaskResultThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => default(MethodInfo)!.CreateTaskResult(default!));
    }
}