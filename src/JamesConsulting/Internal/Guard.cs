using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace JamesConsulting.Internal
{
    /// <summary>
    /// Internal argument-validation helpers. Mirrors the semantics of the
    /// contract attributes previously supplied by Metalama.Patterns.Contracts:
    /// <list type="bullet">
    ///     <item><c>NotNull</c>            → <see cref="ArgumentNullException" /> when value is <c>null</c>.</item>
    ///     <item><c>NotEmpty</c>           → <see cref="ArgumentException" /> when length is zero.</item>
    ///     <item><c>NotNullOrEmpty</c>     → both of the above, in order.</item>
    ///     <item><c>Required</c>           → null + whitespace checks (matches Metalama [Required]).</item>
    ///     <item><c>StrictlyPositive</c>   → <see cref="ArgumentOutOfRangeException" /> when value &lt;= 0.</item>
    /// </list>
    /// </summary>
    internal static class Guard
    {
        private const string ValueCannotBeEmpty = "Value cannot be empty.";

        /// <summary>Throws <see cref="ArgumentNullException" /> when <paramref name="value" /> is <c>null</c>.</summary>
        public static void NotNull<T>(
            [NotNull] T? value,
            [CallerArgumentExpression("value")] string? name = null)
            where T : class
        {
#if NET6_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(value, name);
#else
            if (value is null) throw new ArgumentNullException(name);
#endif
        }

        /// <summary>
        /// Throws <see cref="ArgumentException" /> when <paramref name="value" /> has zero length. Caller is expected to
        /// pre-validate non-null.
        /// </summary>
        public static void NotEmpty(
            string value,
            [CallerArgumentExpression("value")] string? name = null)
        {
            if (value.Length == 0) throw new ArgumentException(ValueCannotBeEmpty, name);
        }

        /// <summary>
        /// Throws <see cref="ArgumentException" /> when array <paramref name="value" /> has zero length. Caller is
        /// expected to pre-validate non-null.
        /// </summary>
        public static void NotEmpty<T>(
            T[] value,
            [CallerArgumentExpression("value")] string? name = null)
        {
            if (value.Length == 0) throw new ArgumentException(ValueCannotBeEmpty, name);
        }

        /// <summary>Throws <see cref="ArgumentNullException" /> for <c>null</c>, <see cref="ArgumentException" /> for empty.</summary>
        public static void NotNullOrEmpty(
            [NotNull] string? value,
            [CallerArgumentExpression("value")] string? name = null)
        {
            NotNull(value, name);
            if (value.Length == 0) throw new ArgumentException(ValueCannotBeEmpty, name);
        }

        /// <summary>Throws <see cref="ArgumentNullException" /> for <c>null</c>, <see cref="ArgumentException" /> for empty array.</summary>
        public static void NotNullOrEmpty<T>(
            [NotNull] T[]? value,
            [CallerArgumentExpression("value")] string? name = null)
        {
            NotNull(value, name);
            if (value.Length == 0) throw new ArgumentException(ValueCannotBeEmpty, name);
        }

        /// <summary>Matches Metalama <c>[Required]</c>: throws on null or whitespace.</summary>
        public static void Required(
            [NotNull] string? value,
            [CallerArgumentExpression("value")] string? name = null)
        {
            NotNull(value, name);
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Value cannot be empty or whitespace.", name);
        }

        /// <summary>Throws <see cref="ArgumentOutOfRangeException" /> when <paramref name="value" /> &lt;= 0.</summary>
        public static void StrictlyPositive(
            int value,
            [CallerArgumentExpression("value")] string? name = null)
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(name, value, "Value must be greater than zero.");
        }
    }
}

#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter)]
    internal sealed class CallerArgumentExpressionAttribute : Attribute
    {
        public CallerArgumentExpressionAttribute(string parameterName)
        {
            ParameterName = parameterName;
        }

        public string ParameterName { get; }
    }
}
#endif

#if NETSTANDARD2_0
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property |
                    AttributeTargets.ReturnValue)]
    internal sealed class NotNullAttribute : Attribute
    {
    }
}
#endif