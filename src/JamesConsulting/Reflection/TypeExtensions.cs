using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using JamesConsulting.Internal;

namespace JamesConsulting.Reflection
{
    /// <summary>
    /// Provides reflection convenience methods for <see cref="Type"/> metadata queries.
    /// </summary>
    public static class TypeExtensions
    {
        private static readonly ConcurrentDictionary<string, MethodInfo> Methods = new();

        /// <summary>
        /// Resolves a cached <see cref="MethodInfo"/> from its <c>MemberInfo.ToString()</c> string representation.
        /// </summary>
        /// <param name="type">The declaring type of the method.</param>
        /// <param name="method">The string form of the method signature.</param>
        /// <returns>The matching <see cref="MethodInfo"/> or <c>null</c> if not found.</returns>
        /// <example>
        /// Resolve method from signature string.
        /// <code>
        /// var mi = typeof(string).GetMethod("Insert", new[]{ typeof(int), typeof(string) });
        /// var signature = mi!.ToString();
        /// var resolved = typeof(string).GetMethodInfoFromString(signature);
        /// var missing = typeof(string).GetMethodInfoFromString("NonExistent"); // null
        /// </code>
        /// </example>
        public static MethodInfo? GetMethodInfoFromString(this Type type, string method)
        {
            Guard.NotNull(type);
            Guard.Required(method);
            if (Methods.TryGetValue(method, out var s)) return s;
            MethodInfo[] methods;
            if (Constants.TypeMethods.TryGetValue(type, out var typeMethod)) methods = typeMethod;
            else
            {
                methods = type.GetMethods();
                Constants.TypeMethods[type] = methods;
            }
            var result = methods.FirstOrDefault(x => x.ToString()!.Equals(method));
            if (result != null) Methods[method] = result;
            return result;
        }

        /// <summary>
        /// Determines whether a method returns a value (not <c>void</c> and not a non-generic <see cref="System.Threading.Tasks.Task"/>).
        /// </summary>
        /// <param name="methodInfo">The method to inspect.</param>
        /// <returns><c>true</c> if the method has a non-void, non-Task return type; otherwise <c>false</c>.</returns>
        /// <example>
        /// Detect return value presence.
        /// <code>
        /// var hasResult = typeof(MyInterface).GetMethod("GetClassById")!.HasReturnValue();
        /// var noResult1 = typeof(MyInterface).GetMethod("Test")!.HasReturnValue();
        /// var noResult2 = typeof(MyInterface).GetMethod("TestAsync")!.HasReturnValue();
        /// </code>
        /// </example>
        public static bool HasReturnValue(this MethodInfo methodInfo)
        {
            Guard.NotNull(methodInfo);
            return methodInfo.ReturnType != Constants.VoidType && methodInfo.ReturnType != Constants.TaskType;
        }

        /// <summary>
        /// Determines whether a method is asynchronous (returns <see cref="System.Threading.Tasks.Task"/> or a derived type).
        /// </summary>
        /// <param name="methodInfo">The method to inspect.</param>
        /// <returns><c>true</c> if the method returns a Task type; otherwise <c>false</c>.</returns>
        /// <example>
        /// Identify async methods.
        /// <code>
        /// var isAsync = typeof(MyInterface).GetMethod("TestAsync")!.IsAsync();
        /// var notAsync = typeof(MyInterface).GetMethod("Test")!.IsAsync();
        /// </code>
        /// </example>
        public static bool IsAsync(this MethodInfo methodInfo)
        {
            Guard.NotNull(methodInfo);
            return Constants.TaskType.IsAssignableFrom(methodInfo.ReturnType);
        }

        /// <summary>
        /// Determines whether a method returns a generic task with a result (<see cref="System.Threading.Tasks.Task{TResult}"/>).
        /// </summary>
        /// <param name="methodInfo">The method to inspect.</param>
        /// <returns><c>true</c> if the method returns a generic Task result; otherwise <c>false</c>.</returns>
        /// <example>
        /// Detect async result methods.
        /// <code>
        /// var asyncWithResult = typeof(MyInterface).GetMethod("GetClassById")!.IsAsyncWithResult();
        /// var asyncNoResult = typeof(MyInterface).GetMethod("TestAsync")!.IsAsyncWithResult();
        /// var syncMethod = typeof(MyInterface).GetMethod("Test")!.IsAsyncWithResult();
        /// </code>
        /// </example>
        public static bool IsAsyncWithResult(this MethodInfo methodInfo)
        {
            Guard.NotNull(methodInfo);
            return methodInfo.ReturnType != Constants.TaskType && Constants.TaskType.IsAssignableFrom(methodInfo.ReturnType);
        }

        /// <summary>
        /// Indicates whether the type represents a concrete (instantiable) class.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns><c>true</c> if the type is a non-abstract class; otherwise <c>false</c>.</returns>
        /// <example>
        /// Check concrete class.
        /// <code>
        /// var concrete = typeof(string).IsConcreteClass();
        /// var abstractClass = typeof(System.Data.Common.DbConnection).IsConcreteClass();
        /// var interfaceType = typeof(IDisposable).IsConcreteClass();
        /// </code>
        /// </example>
        public static bool IsConcreteClass(this Type type)
        {
            Guard.NotNull(type);
            return !type.IsAbstract && !type.IsInterface;
        }
    }
}