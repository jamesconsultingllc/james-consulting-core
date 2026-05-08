using System;
using System.Reflection;
using JamesConsulting.Internal;

namespace JamesConsulting.Threading;

/// <summary>
/// Provides extension methods for creating <see cref="System.Threading.Tasks.Task" /> instances from reflection results.
/// </summary>
public static class MethodInfoExtensions
{
    private const string SetResult = "SetResult";
    private const string Task = "Task";

    /// <summary>
    /// Creates a completed <see cref="System.Threading.Tasks.Task{TResult}" /> wrapping
    /// <paramref name="results" /> for a reflected method whose return type is a constructed
    /// <see cref="System.Threading.Tasks.Task{TResult}" />.
    /// </summary>
    /// <remarks>
    /// Only constructed <see cref="System.Threading.Tasks.Task{TResult}" /> return types are
    /// supported. <see cref="System.Threading.Tasks.Task" /> (non-generic) and <c>void</c> are
    /// rejected with <see cref="ArgumentException" /> because there is no result type to bind.
    /// </remarks>
    /// <param name="methodInfo">The reflected method whose return type must be <c>Task&lt;T&gt;</c>.</param>
    /// <param name="results">The result value to set on the created <c>TaskCompletionSource&lt;T&gt;</c>.</param>
    /// <returns>The completed <c>Task&lt;T&gt;</c> instance carrying <paramref name="results" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="methodInfo" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// The method's return type is not a constructed <see cref="System.Threading.Tasks.Task{TResult}" />
    /// (e.g. <c>void</c> or non-generic <see cref="System.Threading.Tasks.Task" />).
    /// </exception>
    /// <example>
    /// Create a task result via reflection.
    /// <code>
    /// var mi = typeof(MyInterface).GetMethod("GetClassById");
    /// var taskObj = mi!.CreateTaskResult(new MyClass { X = 1 });
    /// var typedTask = (Task&lt;MyClass&gt;)taskObj!;
    /// var result = await typedTask; // result.X == 1
    /// </code>
    /// </example>
    public static object? CreateTaskResult(this MethodInfo methodInfo, dynamic results)
    {
        Guard.NotNull(methodInfo);
        var returnType = methodInfo.ReturnType;
        if (returnType == Constants.VoidType
            || !returnType.IsGenericType
            || returnType.GetGenericTypeDefinition() != typeof(System.Threading.Tasks.Task<>))
        {
            throw new ArgumentException(
                $"{methodInfo} must return Task<T>; got '{returnType}'.", nameof(methodInfo));
        }

        var resultType =
            Constants.TaskCompletionSourceType.MakeGenericType(returnType.GetGenericArguments());
        var taskSource = Activator.CreateInstance(resultType);
        var taskType = taskSource.GetObjectType();
        taskType.InvokeMember(SetResult, BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod, null,
            taskSource, new[] { results });
        return taskType.InvokeMember(Task, BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty, null,
            taskSource, null);
    }
}