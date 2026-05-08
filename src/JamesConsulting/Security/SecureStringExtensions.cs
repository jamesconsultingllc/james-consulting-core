using System;
using System.Runtime.InteropServices;
using System.Security;

namespace JamesConsulting.Security;

/// <summary>
/// Provides extension methods for <see cref="SecureString" /> manipulation.
/// </summary>
public static class SecureStringExtensions
{
    /// <summary>
    /// Converts a <see cref="SecureString" /> to its plaintext <see cref="string" /> representation.
    /// </summary>
    /// <param name="secureString">The secure string to convert.</param>
    /// <returns>The decrypted string, or <see cref="string.Empty" /> if the secure string length is zero.</returns>
    /// <remarks>Caller is responsible for handling sensitive data lifecycle after conversion.</remarks>
    /// <example>
    /// Convert secure string to plain text.
    /// <code>
    /// var ss = "test".ToSecureString();
    /// var plain = ss.ConvertToString(); // "test"
    /// var empty = string.Empty.ToSecureString().ConvertToString(); // ""
    /// </code>
    /// </example>
    public static string ConvertToString(this SecureString secureString)
    {
        if (secureString.Length == 0) return string.Empty;
        var ptr = IntPtr.Zero;
        var result = string.Empty;
        try
        {
            ptr = Marshal.SecureStringToGlobalAllocUnicode(secureString);
            result = Marshal.PtrToStringUni(ptr)!;
        }
        finally
        {
            if (ptr != IntPtr.Zero) Marshal.ZeroFreeGlobalAllocUnicode(ptr);
        }

        return result;
    }
}