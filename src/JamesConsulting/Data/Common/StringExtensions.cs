using System;
using System.Data.Common;
using JamesConsulting.Internal;

namespace JamesConsulting.Data.Common;

/// <summary>
/// Provides extension methods for connection string manipulation.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Removes password related keys from a database connection string for logging or display purposes.
    /// </summary>
    /// <param name="connectionString">The original connection string.</param>
    /// <returns>The sanitized connection string with password keys removed; original value if null or empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connectionString" /> is <c>null</c>.</exception>
    /// <example>
    /// Strip password keys.
    /// <code>
    /// var original = "Server=Test;Database=Test;User ID=Test;Password=Secret;";
    /// var sanitized = original.StripPasswordFromConnectionString(); // "Server=Test;Database=Test;User ID=Test;"
    /// var empty = string.Empty.StripPasswordFromConnectionString(); // ""
    /// </code>
    /// </example>
    public static string StripPasswordFromConnectionString(this string connectionString)
    {
        Guard.NotNull(connectionString);
        if (string.IsNullOrEmpty(connectionString)) return connectionString;
        var db = new DbConnectionStringBuilder { ConnectionString = connectionString };
        db.RemoveKeys("Password", "password", "Pwd", "pwd");
        return db.ToString();
    }
}