using System.Security;

namespace JamesConsulting.Security;

/// <summary>
/// Provides a <see cref="string" /> extension to convert to <see cref="SecureString" />.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Creates a <see cref="SecureString" /> containing the characters of the input string.
    /// </summary>
    /// <param name="str">The source string. May be <see langword="null"/>.</param>
    /// <returns>A new <see cref="SecureString" /> instance; empty if the input is null or empty.</returns>
    /// <example>
    /// Create a secure string.
    /// <code>
    /// var secure = "test".ToSecureString();
    /// secure.Length; // 4
    /// var back = secure.ConvertToString(); // "test"
    /// var empty = string.Empty.ToSecureString(); // Length == 0
    /// </code>
    /// </example>
    public static unsafe SecureString ToSecureString(this string? str)
    {
        if (string.IsNullOrEmpty(str)) return new SecureString();

        fixed (char* ptr = str)
        {
            return new SecureString(ptr, str!.Length);
        }
    }
}