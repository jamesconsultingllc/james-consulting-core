using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using JamesConsulting.Internal;

namespace JamesConsulting.Cryptography;

/// <summary>
/// Provides cryptographic and encoding related extension methods for <see cref="string" />.
/// </summary>
/// <remarks>
/// Base64 helpers are deprecated; prefer direct <see cref="Convert" /> and <see cref="Encoding" /> usage.
/// Default PBKDF2 iteration count has been raised to 100,000 for improved security.
/// </remarks>
public static class StringExtensions
{
    /// <summary>
    /// Decodes a Base64 string into its textual representation.
    /// </summary>
    /// <param name="encoded">The Base64 encoded input string.</param>
    /// <param name="encoding">The character encoding to use. Defaults to <see cref="Encoding.Default" /> when null.</param>
    /// <returns>The decoded textual value.</returns>
    /// <remarks>Deprecated: Use <c>Encoding.UTF8.GetString(Convert.FromBase64String(value))</c> directly.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="encoded" /> is <c>null</c>.</exception>
    /// <exception cref="FormatException">Input is not a valid Base64 string.</exception>
    /// <example>
    /// Round-trip encode/decode.
    /// <code>
    /// var encoded = "test".Base64Encode();
    /// var decoded = encoded.Base64Decode(); // "test"
    /// var empty = string.Empty.Base64Decode(); // ""
    /// </code>
    /// </example>
    [Obsolete("Use Encoding.UTF8.GetString(Convert.FromBase64String(value)) instead.")]
    [SuppressMessage("CodeQuality", "S1133:Deprecated code should be removed",
        Justification = "Kept for backward compatibility; will be removed in next major release.")]
    public static string Base64Decode(this string encoded, Encoding? encoding = null)
    {
        Guard.NotNull(encoded);
        if (string.IsNullOrEmpty(encoded)) return encoded;
        var bytes = Convert.FromBase64String(encoded);
        return (encoding ?? Encoding.Default).GetString(bytes);
    }

    /// <summary>
    /// Encodes a string value to its Base64 representation.
    /// </summary>
    /// <param name="decoded">The plain text input string.</param>
    /// <param name="encoding">The character encoding to use. Defaults to <see cref="Encoding.Default" /> when null.</param>
    /// <returns>The Base64 representation of the input; an empty string when input is empty.</returns>
    /// <remarks>Deprecated: Use <c>Convert.ToBase64String(Encoding.UTF8.GetBytes(value))</c> directly.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="decoded" /> is <c>null</c>.</exception>
    /// <example>
    /// Encoding examples.
    /// <code>
    /// var b64 = "test".Base64Encode();
    /// var empty = string.Empty.Base64Encode(); // ""
    /// </code>
    /// </example>
    [Obsolete("Use Convert.ToBase64String(Encoding.UTF8.GetBytes(value)) instead.")]
    [SuppressMessage("CodeQuality", "S1133:Deprecated code should be removed",
        Justification = "Kept for backward compatibility; will be removed in next major release.")]
    public static string Base64Encode(this string decoded, Encoding? encoding = null)
    {
        Guard.NotNull(decoded);
        if (string.IsNullOrEmpty(decoded)) return decoded;
        var bytes = (encoding ?? Encoding.Default).GetBytes(decoded);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Generates a cryptographically secure random salt.
    /// </summary>
    /// <param name="size">The length of the salt (in bytes). Default is 32.</param>
    /// <returns>A byte array containing random data suitable for use as a salt.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size" /> is less than or equal to 0.</exception>
    /// <example>
    /// Salt generation.
    /// <code>
    /// var salt16 = StringExtensions.GenerateSalt(16);
    /// var salt32 = StringExtensions.GenerateSalt();
    /// </code>
    /// </example>
    public static byte[] GenerateSalt(int size = 32)
    {
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
#if NET6_0_OR_GREATER
            return RandomNumberGenerator.GetBytes(size);
#else
        var randomNumber = new byte[size];
        using var randomNumberGenerator = RandomNumberGenerator.Create();
        randomNumberGenerator.GetBytes(randomNumber);
        return randomNumber;
#endif
    }

    /// <summary>
    /// Hashes a string using PBKDF2, generating a random salt internally.
    /// </summary>
    /// <param name="target">The input string to hash.</param>
    /// <param name="numberOfRounds">The PBKDF2 iteration count. Defaults to 100,000.</param>
    /// <param name="algorithm">The hash algorithm (defaults to <see cref="HashAlgorithmName.SHA256" />).</param>
    /// <returns>A tuple containing the Base64 encoded hash and the generated salt.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target" /> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="target" /> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="numberOfRounds" /> is not positive.</exception>
    /// <remarks>
    /// Uses PBKDF2 with a 32-byte derived key. For deterministic hashing with an existing salt, use the overload accepting a
    /// salt parameter.
    /// </remarks>
    /// <example>
    /// Hash with internally generated salt.
    /// <code>
    /// var (hash, salt) = "test".Hash();
    /// var repeat = "test".Hash(salt); // deterministic
    /// </code>
    /// </example>
    public static (string hashedString, byte[] salt) Hash(
        this string target,
        int numberOfRounds = 100_000,
        HashAlgorithmName? algorithm = null)
    {
        Guard.NotNullOrEmpty(target);
        Guard.StrictlyPositive(numberOfRounds);
        var salt = GenerateSalt();
        var hashedString = target.Hash(salt, numberOfRounds, algorithm);
        return (hashedString, salt);
    }

    /// <summary>
    /// Hashes a string using PBKDF2 with the specified salt.
    /// </summary>
    /// <param name="target">The input string.</param>
    /// <param name="salt">The cryptographic salt (must not be null or empty).</param>
    /// <param name="numberOfRounds">The PBKDF2 iteration count. Defaults to 100,000.</param>
    /// <param name="algorithm">The hash algorithm (defaults to <see cref="HashAlgorithmName.SHA256" />).</param>
    /// <returns>The Base64 encoded PBKDF2 derived key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target" /> or <paramref name="salt" /> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="target" /> or <paramref name="salt" /> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="numberOfRounds" /> is not positive.</exception>
    /// <remarks>On .NET 8 or greater uses the static PBKDF2 API on <see cref="Rfc2898DeriveBytes" /> for reduced allocations.</remarks>
    /// <example>
    /// Hash with explicit salt.
    /// <code>
    /// var salt = StringExtensions.GenerateSalt();
    /// var hash1 = "Rudy James".Hash(salt);
    /// var hash2 = "Rudy James".Hash(salt); // same
    /// var explicitAlg = "test".Hash(salt, 100_000, HashAlgorithmName.SHA256);
    /// </code>
    /// </example>
    [SuppressMessage("Security", "S5344:Use at least 100,000 iterations and a state-of-the-art digest algorithm here.",
        Justification = "Minimum 100,000 enforced; SHA256 selected by default.")]
    public static string Hash(
        this string target,
        byte[] salt,
        int numberOfRounds = 100_000,
        HashAlgorithmName? algorithm = null)
    {
        Guard.NotNullOrEmpty(target);
        Guard.NotNullOrEmpty(salt);
        Guard.StrictlyPositive(numberOfRounds);
        if (numberOfRounds < 100_000)
            throw new ArgumentOutOfRangeException(nameof(numberOfRounds), "Iteration count must be >= 100,000.");

        var effectiveAlgorithm = algorithm ?? HashAlgorithmName.SHA256;
#if NET8_0_OR_GREATER
            // Static PBKDF2 helper for newer frameworks.
            var bytes = Encoding.UTF8.GetBytes(target);
            Span<byte> derived = stackalloc byte[32];
            Rfc2898DeriveBytes.Pbkdf2(bytes, salt, derived, numberOfRounds, effectiveAlgorithm);
            return Convert.ToBase64String(derived);
#elif NET462 || NETSTANDARD2_0
        using var rfc2898DeriveBytes = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(target), salt, numberOfRounds);
        var hash = rfc2898DeriveBytes.GetBytes(32);
        return Convert.ToBase64String(hash);
#else
            using var rfc2898DeriveBytes =
 new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(target), salt, numberOfRounds, effectiveAlgorithm);
            var hash = rfc2898DeriveBytes.GetBytes(32);
            return Convert.ToBase64String(hash);
#endif
    }
}