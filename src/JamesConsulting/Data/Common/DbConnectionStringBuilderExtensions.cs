using System;
using System.Data.Common;
using JamesConsulting.Internal;

namespace JamesConsulting.Data.Common;

/// <summary>
/// Provides extension methods for <see cref="DbConnectionStringBuilder" /> manipulation.
/// </summary>
public static class DbConnectionStringBuilderExtensions
{
    /// <summary>
    /// Removes the specified keys if present in the connection string builder.
    /// </summary>
    /// <param name="connectionStringBuilder">The builder to operate on.</param>
    /// <param name="keys">The keys to remove.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="connectionStringBuilder" /> or <paramref name="keys" /> is
    /// <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="keys" /> is an empty collection.</exception>
    /// <example>
    /// Remove keys from connection string.
    /// <code>
    /// var builder = new DbConnectionStringBuilder { ConnectionString = "Server=Test;Password=Secret;User ID=Me;" };
    /// builder.RemoveKeys("Password", "Pwd");
    /// var result = builder.ToString(); // "Server=Test;User ID=Me;"
    /// </code>
    /// </example>
    public static void RemoveKeys(
        this DbConnectionStringBuilder connectionStringBuilder,
        params string[] keys)
    {
        Guard.NotNull(connectionStringBuilder);
        Guard.NotNullOrEmpty(keys);
        Array.ForEach(
            keys,
            key =>
            {
                if (connectionStringBuilder.ContainsKey(key)) connectionStringBuilder.Remove(key);
            });
    }
}