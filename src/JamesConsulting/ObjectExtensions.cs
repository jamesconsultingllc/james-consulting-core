using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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

    // Mask round-trips an in-memory object, so it must use one case-sensitive contract (including public
    // fields) for serialization, metadata lookup, and deserialization. Case-insensitive matching is only
    // appropriate for external input, which uses SerializerOptions.
    private static readonly ConcurrentDictionary<JsonConverter, JsonSerializerOptions> ConverterSerializerOptions =
        new(ReferenceEqualityComparer.Instance);

    private static readonly JsonSerializerOptions MaskSerializerOptions = new()
    {
        IncludeFields = true,
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
    /// <exception cref="InvalidOperationException">
    /// A path explicitly names a read-only or computed member, which the masked copy cannot carry.
    /// Wildcards skip such members. Polymorphic type discriminators are never masked.
    /// </exception>
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
        // Serialize and deserialize with the runtime type so a derived instance passed through a base-typed
        // variable keeps its derived type and data in the masked copy.
        var runtimeType = data.GetType();
        var root = JsonSerializer.SerializeToNode(data, runtimeType, MaskSerializerOptions);
        if (root is null)
            throw new InvalidOperationException("The object serialized to JSON null and cannot be masked.");

        foreach (var path in propertiesToMask)
        {
            foreach (var selection in SelectTokens(root, runtimeType, path, nameof(propertiesToMask)))
                SetValue(selection);
        }

        return (T)root.Deserialize(runtimeType, MaskSerializerOptions)!;
    }

    private static List<NodeSelection> SelectTokens(
        JsonNode root, Type rootType, string path, string parameterName)
    {
        var nodes = new List<NodeSelection> { new(root, rootType) };
        var index = 0;
        if (path == "$" || (path.Length > 1 && path[0] == '$' && path[1] is '.' or '['))
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
                            AddPropertyMatch(nextNodes, selection, objectNode, property.Key, isWildcard: true);
                    }
                    else if (selection.Node is JsonObject parent)
                    {
                        AddPropertyMatch(nextNodes, selection, parent, propertyName, isWildcard: false);
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
                        AddPropertyMatch(matches, selection, objectNode, property.Key, isWildcard: true);
                }

                continue;
            }

            if (selector.Length >= 2 && (selector[0] is '\'' or '"') && selector[^1] == selector[0])
            {
                var propertyName = DecodeQuotedPropertyName(selector, parameterName);
                if (selection.Node is JsonObject parent)
                    AddPropertyMatch(matches, selection, parent, propertyName, isWildcard: false);
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

    private static void AddPropertyMatch(
        ICollection<NodeSelection> matches,
        NodeSelection selection,
        JsonObject parent,
        string propertyName,
        bool isWildcard)
    {
        if (!parent.TryGetPropertyValue(propertyName, out var propertyValue) || propertyValue is null)
            return;

        var member = GetMemberContract(selection.Type, parent, propertyName);

        // Type discriminators are serializer metadata, not data; masking them breaks deserialization.
        if (member.IsTypeDiscriminator)
            return;

        // A wildcard skips members the masked copy cannot carry (computed or read-only). They never hold
        // the source instance's state after deserialization, so there is nothing to mask.
        if (isWildcard && !member.IsWritable)
            return;

        matches.Add(new NodeSelection(
            propertyValue, member.Type, parent, propertyName,
            Converter: member.Converter, IsWritable: member.IsWritable));
    }

    private static MemberContract GetMemberContract(Type parentType, JsonObject parentNode, string propertyName)
    {
        parentType = Nullable.GetUnderlyingType(parentType) ?? parentType;
        var declaredTypeInfo = MaskSerializerOptions.GetTypeInfo(parentType);
        if (declaredTypeInfo.PolymorphismOptions?.TypeDiscriminatorPropertyName == propertyName)
            return new MemberContract(typeof(object), IsTypeDiscriminator: true);

        var typeInfo = ResolveDerivedTypeInfo(declaredTypeInfo, parentNode);
        if (typeInfo.Kind == JsonTypeInfoKind.Dictionary)
            return new MemberContract(typeInfo.ElementType ?? typeof(object));

        foreach (var property in typeInfo.Properties)
        {
            if (property.Name == propertyName)
                return new MemberContract(
                    property.PropertyType,
                    Converter: property.CustomConverter,
                    IsWritable: property.Set is not null || property.AssociatedParameter is not null);
        }

        return new MemberContract(typeof(object));
    }

    private static JsonTypeInfo ResolveDerivedTypeInfo(JsonTypeInfo typeInfo, JsonObject node)
    {
        var polymorphism = typeInfo.PolymorphismOptions;
        if (polymorphism is null ||
            !node.TryGetPropertyValue(polymorphism.TypeDiscriminatorPropertyName, out var discriminatorNode) ||
            discriminatorNode is not JsonValue discriminator)
            return typeInfo;

        foreach (var derivedType in polymorphism.DerivedTypes)
        {
            var matches = derivedType.TypeDiscriminator switch
            {
                string name => discriminator.TryGetValue<string>(out var value) && value == name,
                int id => discriminator.TryGetValue<int>(out var value) && value == id,
                _ => false
            };
            if (matches)
                return MaskSerializerOptions.GetTypeInfo(derivedType.DerivedType);
        }

        return typeInfo;
    }

    private static Type GetElementType(Type collectionType)
    {
        return MaskSerializerOptions.GetTypeInfo(collectionType).ElementType ?? typeof(object);
    }

    private static void SetValue(NodeSelection selection)
    {
        if (selection.Node is not JsonValue value)
            return;

        if (!selection.IsWritable)
            throw new InvalidOperationException(
                $"The member '{selection.PropertyName}' cannot be masked because it has no setter or " +
                "constructor parameter, so the masked copy would not reflect the masked value.");

        var replacement = CreateDefaultValue(selection.Type, selection.Converter, value);

        if (selection.Parent is JsonObject parentObject && selection.PropertyName is not null)
        {
            parentObject[selection.PropertyName] = replacement;
        }
        else if (selection.Parent is JsonArray parentArray && selection.ArrayIndex is int index)
        {
            parentArray[index] = replacement;
        }
    }

    private static JsonNode? CreateDefaultValue(Type type, JsonConverter? converter, JsonValue value)
    {
        // Unknown member type (for example object-valued dictionaries): infer a default from the JSON kind.
        if (type == typeof(object))
        {
            return value.GetValueKind() switch
            {
                JsonValueKind.Number => JsonValue.Create(0),
                JsonValueKind.True or JsonValueKind.False => JsonValue.Create(false),
                _ => null
            };
        }

        // default(T) for reference and nullable value types is null.
        if (!type.IsValueType || Nullable.GetUnderlyingType(type) is not null)
            return null;

        // Serialize default(T) through the member's own converter so property-level converters
        // (string enums, custom formats) produce a value they can read back.
        var options = converter is null
            ? MaskSerializerOptions
            : ConverterSerializerOptions.GetOrAdd(
                converter, static c => new JsonSerializerOptions(MaskSerializerOptions) { Converters = { c } });
        return JsonSerializer.SerializeToNode(Activator.CreateInstance(type), type, options);
    }

    private readonly record struct NodeSelection(
        JsonNode Node,
        Type Type,
        JsonNode? Parent = null,
        string? PropertyName = null,
        int? ArrayIndex = null,
        JsonConverter? Converter = null,
        bool IsWritable = true);

    private readonly record struct MemberContract(
        Type Type,
        JsonConverter? Converter = null,
        bool IsWritable = true,
        bool IsTypeDiscriminator = false);

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
