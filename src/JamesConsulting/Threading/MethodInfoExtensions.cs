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
    /// Creates a <see cref="System.Threading.Tasks.Task" /> (or <see cref="System.Threading.Tasks.Task{TResult}" />)
    /// representing the supplied result for the reflected method's generic return type.
    /// </summary>
    /// <param name="methodInfo">The reflected method with a generic Task return type.</param>
    /// <param name="results">The dynamic results to set on the created task source.</param>
    /// <returns>The created task instance containing the result.</returns>
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