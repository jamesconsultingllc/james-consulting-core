using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using JamesConsulting.Internal;

namespace JamesConsulting;

/// <summary>
/// Provides extension methods for generic object manipulation including JSON masking and serialization.
/// </summary>
public static class ObjectExtensions
{
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

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
    /// <param name="propertiesToMask">
    /// Dot-delimited property paths with optional quoted property names, array indices, or array wildcards
    /// (e.g. <c>Customer.Address</c> or <c>Orders[*].CardNumber</c>). JSONPath filters and recursive descent are not supported.
    /// </param>
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
        var root = JsonSerializer.SerializeToNode(data, data.GetType());
        if (root is null)
            throw new InvalidOperationException("The object serialized to JSON null and cannot be masked.");

        foreach (var path in propertiesToMask)
        {
            foreach (var selection in SelectTokens(root, data.GetType(), path, nameof(propertiesToMask)))
                SetValue(selection);
        }

        return root.Deserialize<T>(SerializerOptions)!;
    }

    private static List<NodeSelection> SelectTokens(
        JsonNode root, Type rootType, string path, string parameterName)
    {
        var nodes = new List<NodeSelection> { new(root, rootType) };
        var index = 0;
        if (path.StartsWith('$'))
            index++;

        while (index < path.Length)
        {
            if (path[index] == '.')
            {
                if (index + 1 < path.Length && path[index + 1] == '.')
                    throw new ArgumentException(
                        "Recursive descent is not supported in property paths.", parameterName);
                index++;
                continue;
            }

            var nextNodes = new List<NodeSelection>();
            if (path[index] == '[')
            {
                var closeIndex = FindClosingBracket(path, index);
                if (closeIndex < 0)
                    throw new ArgumentException("The property path contains an unmatched '['.", parameterName);

                var selector = path[(index + 1)..closeIndex];
                index = closeIndex + 1;
                AddBracketMatches(nodes, nextNodes, selector, parameterName);
            }
            else
            {
                var startIndex = index;
                while (index < path.Length && path[index] is not '.' and not '[')
                    index++;
                var propertyName = path[startIndex..index];
                if (propertyName.Length == 0)
                    throw new ArgumentException("The property path contains an empty segment.", parameterName);

                foreach (var selection in nodes)
                {
                    if (propertyName == "*" && selection.Node is JsonObject objectNode)
                    {
                        foreach (var property in objectNode)
                        {
                            if (property.Value is not null)
                                nextNodes.Add(new NodeSelection(
                                    property.Value, GetPropertyType(selection.Type, property.Key),
                                    objectNode, property.Key));
                        }
                    }
                    else if (selection.Node is JsonObject parent &&
                             parent.TryGetPropertyValue(propertyName, out var propertyValue) &&
                             propertyValue is not null)
                    {
                        nextNodes.Add(new NodeSelection(
                            propertyValue, GetPropertyType(selection.Type, propertyName),
                            parent, propertyName));
                    }
                }
            }

            nodes = nextNodes;
            if (nodes.Count == 0)
                break;
        }

        return nodes;
    }

    private static int FindClosingBracket(string path, int openIndex)
    {
        var quote = '\0';
        var escaped = false;
        for (var index = openIndex + 1; index < path.Length; index++)
        {
            var current = path[index];
            if (quote != '\0')
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (current == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (current == quote)
                    quote = '\0';
                continue;
            }

            if (current is '\'' or '"')
                quote = current;
            else if (current == ']')
                return index;
        }

        return -1;
    }

    private static void AddBracketMatches(
        IEnumerable<NodeSelection> nodes,
        ICollection<NodeSelection> matches,
        string selector,
        string parameterName)
    {
        foreach (var selection in nodes)
        {
            if (selector == "*")
            {
                if (selection.Node is JsonArray array)
                {
                    for (var index = 0; index < array.Count; index++)
                    {
                        if (array[index] is { } item)
                            matches.Add(new NodeSelection(
                                item, GetElementType(selection.Type), array, ArrayIndex: index));
                    }
                }
                else if (selection.Node is JsonObject objectNode)
                {
                    foreach (var property in objectNode)
                    {
                        if (property.Value is not null)
                            matches.Add(new NodeSelection(
                                property.Value, GetPropertyType(selection.Type, property.Key),
                                objectNode, property.Key));
                    }
                }

                continue;
            }

            if (selector.Length >= 2 && (selector[0] is '\'' or '"') && selector[^1] == selector[0])
            {
                var propertyName = DecodeQuotedPropertyName(selector, parameterName);
                if (selection.Node is JsonObject parent &&
                    parent.TryGetPropertyValue(propertyName, out var propertyValue) &&
                    propertyValue is not null)
                {
                    matches.Add(new NodeSelection(
                        propertyValue, GetPropertyType(selection.Type, propertyName),
                        parent, propertyName));
                }
            }
            else if (int.TryParse(selector, NumberStyles.None, CultureInfo.InvariantCulture, out var index) &&
                     selection.Node is JsonArray array &&
                     index < array.Count &&
                     array[index] is { } item)
            {
                matches.Add(new NodeSelection(item, GetElementType(selection.Type), array, ArrayIndex: index));
            }
            else if (!int.TryParse(selector, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                throw new ArgumentException(
                    "Only quoted property names, array indices, and wildcards are supported in brackets.",
                    parameterName);
            }
        }
    }

    private static string DecodeQuotedPropertyName(string selector, string parameterName)
    {
        var quote = selector[0];
        var propertyName = new StringBuilder(selector.Length - 2);
        for (var index = 1; index < selector.Length - 1; index++)
        {
            var current = selector[index];
            if (current != '\\')
            {
                propertyName.Append(current);
                continue;
            }

            if (++index >= selector.Length - 1 || selector[index] is not '\\' && selector[index] != quote)
                throw new ArgumentException(
                    "Quoted property names only support escaped quotes and backslashes.", parameterName);

            propertyName.Append(selector[index]);
        }

        return propertyName.ToString();
    }

    private static Type GetPropertyType(Type parentType, string propertyName)
    {
        parentType = Nullable.GetUnderlyingType(parentType) ?? parentType;
        var typeInfo = SerializerOptions.GetTypeInfo(parentType);
        if (typeInfo.Kind == JsonTypeInfoKind.Dictionary)
            return typeInfo.ElementType ?? typeof(object);

        foreach (var property in typeInfo.Properties)
        {
            if (property.Name == propertyName)
                return property.PropertyType;
        }

        return typeof(object);
    }

    private static Type GetElementType(Type collectionType)
    {
        return SerializerOptions.GetTypeInfo(collectionType).ElementType ?? typeof(object);
    }

    private static void SetValue(NodeSelection selection)
    {
        var property = selection.Node;
        if (property is not JsonValue value)
            return;

        var underlyingType = Nullable.GetUnderlyingType(selection.Type) ?? selection.Type;
        JsonNode? replacement;
        if (value.GetValueKind() == JsonValueKind.String && underlyingType.IsValueType)
            replacement = JsonSerializer.SerializeToNode(Activator.CreateInstance(underlyingType), underlyingType);
        else
        {
            replacement = value.GetValueKind() switch
            {
                JsonValueKind.String => null,
                JsonValueKind.Number => JsonValue.Create(0),
                _ => property.DeepClone()
            };
        }

        if (selection.Parent is JsonObject parentObject && selection.PropertyName is not null)
        {
            parentObject[selection.PropertyName] = replacement;
        }
        else if (selection.Parent is JsonArray parentArray && selection.ArrayIndex is int index)
        {
            parentArray[index] = replacement;
        }
    }

    private readonly record struct NodeSelection(
        JsonNode Node, Type Type, JsonNode? Parent = null, string? PropertyName = null, int? ArrayIndex = null);

    /// <summary>
    /// Serializes an object to JSON and writes it to the provided stream, resetting the position to 0 after writing.
    /// </summary>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="stream">The writable, seekable target stream.</param>
    /// <returns>The same stream instance, positioned at the start.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="obj" /> or <paramref name="stream" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="stream" /> is not writable (<see cref="Stream.CanWrite" /> is <c>false</c>) or not seekable
    /// (<see cref="Stream.CanSeek" /> is <c>false</c>). Both capabilities are required: the method writes the JSON
    /// payload and then resets <see cref="Stream.Position" /> to 0 so callers can immediately read the result back.
    /// </exception>
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
        if (!stream.CanWrite)
            throw new ArgumentException("Stream must be writable.", nameof(stream));
        if (!stream.CanSeek)
            throw new ArgumentException("Stream must be seekable so the position can be reset to 0.", nameof(stream));
        JsonSerializer.Serialize(stream, obj, obj.GetType());
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
            _ => JsonSerializer.Serialize(obj, obj.GetType())
        };
    }
}
