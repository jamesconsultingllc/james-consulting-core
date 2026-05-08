using System;
using System.IO;
using System.Linq;
using JamesConsulting.Internal;
using Newtonsoft.Json.Linq;
using Utf8Json;

namespace JamesConsulting;

/// <summary>
/// Provides extension methods for generic object manipulation including JSON masking and serialization.
/// </summary>
public static class ObjectExtensions
{
    private static readonly JTokenType[] NumericTokenTypes = { JTokenType.Float, JTokenType.Integer };

    /// <summary>
    /// Gets the runtime type of an object or returns the generic type parameter when the instance is <c>null</c>.
    /// </summary>
    /// <typeparam name="T">The static compile-time type.</typeparam>
    /// <param name="obj">The object instance.</param>
    /// <returns>The runtime <see cref="Type" /> of <paramref name="obj" /> or the generic type parameter when <c>null</c>.</returns>
    /// <example>
    /// Determine object type.
    /// <code>
    /// object o = "text";
    /// var type1 = o.GetObjectType(); // System.String
    /// string? nullable = null;
    /// var type2 = nullable.GetObjectType(); // System.String
    /// </code>
    /// </example>
    public static Type GetObjectType<T>(this T obj)
    {
        return obj?.GetType() ?? typeof(T);
    }

    /// <summary>
    /// Produces a deep copy of an object with specified JSON properties masked (set to default values).
    /// </summary>
    /// <typeparam name="T">The type of the object being masked.</typeparam>
    /// <param name="data">The source object.</param>
    /// <param name="propertiesToMask">JsonPath-like property names to mask at the root (e.g. <c>Customer.Address</c>).</param>
    /// <returns>A copy of <paramref name="data" /> with targeted properties masked.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="data" /> or <paramref name="propertiesToMask" /> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="propertiesToMask" /> is empty.</exception>
    /// <example>
    /// Mask properties.
    /// <code>
    /// var obj = new { Value1 = "MyPassword", Value2 = 32 };
    /// var masked = obj.Mask("Value2");
    /// // masked.Value2 == 0
    /// </code>
    /// </example>
    public static T Mask<T>(this T data, params string[] propertiesToMask)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        Guard.NotNullOrEmpty(propertiesToMask);
        var jo = JObject.FromObject(data!);
        foreach (var propertyToMask in propertiesToMask)
        {
            var properties = jo.SelectTokens($"$.{propertyToMask}");
            foreach (var property in properties) SetValue(jo, property);
        }

        return jo.ToObject<T>()!;
    }

    private static void SetValue(JToken jo, JToken property)
    {
        if (jo.SelectToken(property.Path) is not JValue key) return;
        if (key.Type == JTokenType.String) key.Value = default(string);
        else if (NumericTokenTypes.Contains(key.Type)) key.Value = default(int);
        else
            key.Value = key.Type switch
            {
                JTokenType.Date => default(DateTime),
                JTokenType.TimeSpan => default(TimeSpan),
                JTokenType.Array => null,
                JTokenType.Object => null,
                _ => key.Value
            };
    }

    /// <summary>
    /// Serializes an object to JSON and writes it to the provided stream, resetting the position to 0 after writing.
    /// </summary>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="stream">The writable target stream.</param>
    /// <returns>The same stream instance, positioned at the start.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="obj" /> or <paramref name="stream" /> is <c>null</c>.</exception>
    /// <example>
    /// Serialize to stream.
    /// <code>
    /// var test = new { Name = "Rudy", Value = 3 };
    /// using var ms = new MemoryStream();
    /// var jsonStream = test.SerializeToJsonStream(ms);
    /// var length = jsonStream.Length; // > 0
    /// </code>
    /// </example>
    public static Stream SerializeToJsonStream(this object obj, Stream stream)
    {
        Guard.NotNull(obj);
        Guard.NotNull(stream);
        JsonSerializer.Serialize(stream, obj);
        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// Converts an object to a JSON string. Strings are returned unchanged.
    /// </summary>
    /// <param name="obj">The object to serialize.</param>
    /// <returns>A JSON string representation of <paramref name="obj" />.</returns>
    /// <example>
    /// Serialize to JSON string.
    /// <code>
    /// var json = new { X = 1 }.ToJson();
    /// var str = ((object)"test").ToJson(); // "test"
    /// </code>
    /// </example>
    public static string ToJson(this object obj)
    {
        Guard.NotNull(obj);
        return obj switch
        {
            string objString => objString,
            _ => JsonSerializer.ToJsonString(obj)
        };
    }
}