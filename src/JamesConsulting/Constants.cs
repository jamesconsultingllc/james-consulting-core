using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace JamesConsulting;

/// <summary>
/// Provides cached reflection metadata and commonly used CLR <see cref="Type" /> instances for internal utilities.
/// </summary>
public static class Constants
{
    /// <summary>
    /// Gets the generic <see cref="ConfiguredTaskAwaitable{TResult}" /> type definition.
    /// </summary>
    public static readonly Type GenericConfiguredTaskAwaitable = typeof(ConfiguredTaskAwaitable<>);

    /// <summary>
    /// Gets the generic <see cref="Task{TResult}" /> type definition.
    /// </summary>
    public static readonly Type GenericTaskType = typeof(Task<>);

    /// <summary>
    /// Gets the generic <see cref="Func{TResult}" /> delegate type definition.
    /// </summary>
    public static readonly Type OutputOnlyFunctionType = typeof(Func<>);

    /// <summary>
    /// Gets the generic <see cref="TaskCompletionSource{TResult}" /> type definition.
    /// </summary>
    public static readonly Type TaskCompletionSourceType = typeof(TaskCompletionSource<>);

    /// <summary>
    /// Gets the non-generic <see cref="Task" /> type.
    /// </summary>
    public static readonly Type TaskType = typeof(Task);

    /// <summary>
    /// Gets the CLR <see cref="void" /> type representation.
    /// </summary>
    public static readonly Type VoidType = typeof(void);

    /// <summary>
    /// Cache mapping <see cref="Type" /> instances to their discovered public methods.
    /// </summary>
    public static ConcurrentDictionary<Type, MethodInfo[]> TypeMethods { get; private set; } = new();
}