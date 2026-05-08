using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading;
using JamesConsulting.Internal;

namespace JamesConsulting;

/// <summary>
/// Provides general purpose <see cref="string" /> extension methods.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Converts a string to a UTF-16 byte array using <see cref="Buffer.BlockCopy" />.
    /// </summary>
    /// <param name="arg">The input string.</param>
    /// <returns>A byte array representing the UTF-16 encoded characters of the input.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="arg" /> is <c>null</c>.</exception>
    /// <exception cref="OverflowException">
    /// The array is multidimensional and contains more than <see cref="int.MaxValue" />
    /// elements.
    /// </exception>
    /// <example>
    /// Convert to bytes and back.
    /// <code>
    /// var bytes = "Test".GetBytes();
    /// var roundTrip = bytes.GetString(); // "Test"
    /// var empty = string.Empty.GetBytes(); // Array.Empty&lt;byte&gt;()
    /// </code>
    /// </example>
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly",
        Justification = "Reviewed.")]
    public static byte[] GetBytes(this string arg)
    {
        Guard.NotNull(arg);
        if (arg.Length == 0) return Array.Empty<byte>();
        var bytes = new byte[arg.Length * sizeof(char)];
        Buffer.BlockCopy(arg.ToCharArray(), 0, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>
    /// Converts the string to title case using either the provided <see cref="CultureInfo" /> or the current UI culture.
    /// </summary>
    /// <param name="arg">The input string to convert.</param>
    /// <param name="ci">Optional culture whose <see cref="TextInfo" /> is used for casing.</param>
    /// <returns>The title-cased string; or the original string if empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="arg" /> is <c>null</c>.</exception>
    /// <example>
    /// Title casing examples.
    /// <code>
    /// var tc = "rudy james".ToTitleCase(); // "Rudy James"
    /// var unchangedEmpty = string.Empty.ToTitleCase(); // ""
    /// </code>
    /// </example>
    public static string ToTitleCase(this string arg, CultureInfo? ci = null)
    {
        Guard.NotNull(arg);
        if (arg.Length == 0) return arg;
        return ci != null
            ? ci.TextInfo.ToTitleCase(arg)
            : Thread.CurrentThread.CurrentUICulture.TextInfo.ToTitleCase(arg);
    }

    /// <summary>
    /// Truncates a string to the specified length starting at index 0.
    /// </summary>
    /// <param name="argument">The input string.</param>
    /// <param name="length">The maximum number of characters to keep.</param>
    /// <returns>The truncated string, or <see cref="string.Empty" /> if the input is empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="argument" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length" /> is not positive.</exception>
    /// <example>
    /// Truncate examples.
    /// <code>
    /// var truncated = "testing".Truncate(4); // "test"
    /// var emptyResult = string.Empty.Truncate(100); // ""
    /// </code>
    /// </example>
    public static string Truncate(this string argument, int length)
    {
        Guard.NotNull(argument);
        Guard.StrictlyPositive(length);
        return argument.Length == 0 ? string.Empty : argument.Substring(0, length);
    }
}