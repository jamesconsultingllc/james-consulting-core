using System;
using JamesConsulting.Internal;

namespace JamesConsulting;

/// <summary>
/// Provides extension methods for working with <see cref="byte" /> arrays.
/// </summary>
public static class ByteArrayExtensions
{
    /// <summary>
    /// Converts a byte array (produced from a UTF-16/Unicode string using <see cref="Buffer.BlockCopy" />) back to a
    /// <see cref="string" />.
    /// </summary>
    /// <param name="bytes">The byte array to convert. Expected to represent UTF-16 characters; its length must be a multiple of <c>sizeof(char)</c> (2).</param>
    /// <returns>The reconstructed string or <see cref="string.Empty" /> if the array is empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bytes" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="bytes" /> has an odd length, which cannot be a valid UTF-16 byte
    /// representation (each <see cref="char" /> occupies exactly two bytes).
    /// </exception>
    /// <exception cref="OverflowException">
    /// The array is multidimensional and contains more than <see cref="int.MaxValue" />
    /// elements.
    /// </exception>
    /// <example>
    /// Convert bytes back to string.
    /// <code>
    /// var original = "Test";
    /// var bytes = original.GetBytes();
    /// var roundTrip = bytes.GetString(); // "Test"
    /// var emptyRoundTrip = Array.Empty&lt;byte&gt;().GetString(); // ""
    /// </code>
    /// </example>
    public static string GetString(this byte[] bytes)
    {
        Guard.NotNull(bytes);
        if (bytes.Length == 0) return string.Empty;
        if (bytes.Length % sizeof(char) != 0)
            throw new ArgumentException(
                $"Byte array length must be a multiple of sizeof(char) ({sizeof(char)}) to represent a UTF-16 string. Actual length: {bytes.Length}.",
                nameof(bytes));

        var chars = new char[bytes.Length / sizeof(char)];
        Buffer.BlockCopy(bytes, 0, chars, 0, bytes.Length);
        return new string(chars);
    }
}