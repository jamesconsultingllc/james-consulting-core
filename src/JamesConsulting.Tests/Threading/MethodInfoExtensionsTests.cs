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
    public void CreateTaskResultNonGenericTaskReturnTypeThrowsArgumentException()
    {
        // TestAsync returns System.Threading.Tasks.Task (non-generic) — no T to bind a result to.
        var ex = Assert.Throws<ArgumentException>(() =>
            InstanceType.GetMethod("TestAsync")!.CreateTaskResult(default!));
        ex.ParamName.Should().Be("methodInfo");
        ex.Message.Should().Contain("Task<T>");
    }

    [Fact]
    public void CreateTaskResultThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => default(MethodInfo)!.CreateTaskResult(default!));
    }

    [Fact]
    public void CreateTaskResultOpenGenericReturnTypeThrowsArgumentException()
    {
        // Open generic method definition: returns Task<T> where T is unbound.
        var open = typeof(GenericMethodHost)
            .GetMethod(nameof(GenericMethodHost.GenericAsync))!;
        open.ContainsGenericParameters.Should().BeTrue();

        var ex = Assert.Throws<ArgumentException>(() => open.CreateTaskResult(new MyClass { X = 1 }));
        ex.ParamName.Should().Be("methodInfo");
        ex.Message.Should().Contain("unbound generic parameters");
    }

    private sealed class GenericMethodHost
    {
        public Task<T> GenericAsync<T>() => Task.FromResult(default(T)!);
    }
}