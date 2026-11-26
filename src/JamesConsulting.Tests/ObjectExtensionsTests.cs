using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace JamesConsulting.Tests;

/// <summary>
/// The object extensions tests.
/// </summary>
public class ObjectExtensionsTests
{
    /// <summary>
    /// The mask as static method null object throws argument null exception.
    /// </summary>
    [Fact]
    public void MaskAsStaticMethodNullObjectThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => default(object)!.Mask());
    }


    /// <summary>
    /// Tests that throwing an exception when the ignore list is empty.
    /// </summary>
    [Fact]
    public void MaskEmptyIgnoreListThrowsArgumentException()
    {
        object test = new Test();
        var exception = Assert.Throws<ArgumentException>(() => test.Mask());
        exception.ParamName.Should().Be("propertiesToMask");
    }

    /// <summary>
    /// Tests that the mask method masks given values.
    /// </summary>
    [Fact]
    public void MaskMasksGivenValues()
    {
        var test = new Test
        {
            Value1 = "MyPassword",
            Value2 = 32,
            Value3 = DateTime.Now,
            Value4 = TimeSpan.FromMinutes(3),
            Value5 =
            [
                new Test2 { Value1 = "test", Value2 = 2 },
                new Test2 { Value1 = "test", Value2 = 2 },
                new Test2 { Value1 = "test", Value2 = 2 }
            ]
        };

        var maskedTest = test.Mask("Value2", "Value3", "Value4", "Value5[*].Value1");
        maskedTest.Value1.Should().Be("MyPassword");
        maskedTest.Value2.Should().Be(default);
        maskedTest.Value3.Should().Be(default);
        maskedTest.Value4.Should().Be(default);

        if (maskedTest.Value5 == null) return;

        foreach (var value5 in maskedTest.Value5) value5.Value1.Should().Be(default);
    }

    [Fact]
    public void MaskSupportsArrayIndexes()
    {
        var test = new Test
        {
            Value5 =
            [
                new Test2 { Value1 = "first", Value2 = 1 },
                new Test2 { Value1 = "second", Value2 = 2 }
            ]
        };

        var masked = test.Mask("Value5[1].Value2");

        masked.Value5![0].Value2.Should().Be(1);
        masked.Value5[1].Value2.Should().Be(0);
    }

    [Fact]
    public void MaskSupportsQuotedPropertyNames()
    {
        var data = new Dictionary<string, string>
        {
            ["Value.With.Dot"] = "private",
            ["Keep"] = "visible"
        };

        var masked = data.Mask("['Value.With.Dot']");

        masked["Value.With.Dot"].Should().BeNull();
        masked["Keep"].Should().Be("visible");
    }

    [Fact]
    public void MaskSupportsQuotedPropertyNamesContainingClosingBrackets()
    {
        var data = new Dictionary<string, string>
        {
            ["Value]With.Dot"] = "private",
            ["Keep"] = "visible"
        };

        var masked = data.Mask("['Value]With.Dot']");

        masked["Value]With.Dot"].Should().BeNull();
        masked["Keep"].Should().Be("visible");
    }

    [Fact]
    public void MaskDecodesEscapedQuotedPropertyNames()
    {
        var data = new Dictionary<string, string>
        {
            ["Card\\Number"] = "private",
            ["Card'Number"] = "also private"
        };

        var masked = data.Mask("['Card\\\\Number']", "['Card\\'Number']");

        masked["Card\\Number"].Should().BeNull();
        masked["Card'Number"].Should().BeNull();
    }

    [Fact]
    public void MaskRejectsUnsupportedQuotedPropertyNameEscapes()
    {
        var data = new Dictionary<string, string> { ["CardqNumber"] = "private" };

        var exception = Assert.Throws<ArgumentException>(() => data.Mask("['Card\\qNumber']"));

        exception.ParamName.Should().Be("propertiesToMask");
    }

    [Fact]
    public void MaskDefaultsStringEncodedValueTypes()
    {
        var test = new StringEncodedValueTypes
        {
            Id = Guid.NewGuid(),
            Date = new DateOnly(2024, 3, 14),
            Time = new TimeOnly(15, 9),
            Character = 'X'
        };

        var masked = test.Mask("Id", "Date", "Time", "Character");

        masked.Id.Should().Be(Guid.Empty);
        masked.Date.Should().Be(DateOnly.MinValue);
        masked.Time.Should().Be(TimeOnly.MinValue);
        masked.Character.Should().Be('\0');
    }

    [Fact]
    public void MaskResolvesPropertiesOnNullableStructs()
    {
        var test = new NullableStructProperty
        {
            Details = new StructDetails { Timestamp = DateTime.UtcNow }
        };

        var masked = test.Mask("Details.Timestamp");

        masked.Details.Should().NotBeNull();
        masked.Details!.Value.Timestamp.Should().Be(default);
    }

    [Fact]
    public void MaskWildcardsReplaceLargeArraysAndDictionaries()
    {
        var values = Enumerable.Range(0, 2048).ToArray();
        var array = values.Select(value => $"secret-{value}").ToArray();
        var dictionary = values.ToDictionary(value => $"key-{value}", value => $"secret-{value}");

        var maskedArray = array.Mask("$[*]");
        var maskedDictionary = dictionary.Mask("$[*]");

        maskedArray.Should().HaveCount(values.Length);
        maskedArray.Should().OnlyContain(value => value == null);
        maskedDictionary.Should().HaveCount(values.Length);
        maskedDictionary.Values.Should().OnlyContain(value => value == null);
    }

    [Fact]
    public void MaskMasksPublicFields()
    {
        var test = new PublicFieldModel { Name = "visible" };

        var masked = test.Mask("Token");

        masked.Token.Should().BeNull();
        masked.Name.Should().Be("visible");
    }

    [Fact]
    public void MaskTreatsDollarPrefixedNamesAsLiteralProperties()
    {
        var data = new Dictionary<string, string>
        {
            ["$secret"] = "sensitive",
            ["secret"] = "keep"
        };

        var masked = data.Mask("$secret");

        masked["$secret"].Should().BeNull();
        masked["secret"].Should().Be("keep");
    }

    [Fact]
    public void MaskSupportsPropertyNamesDifferingOnlyByCase()
    {
        var test = new CaseDistinctProperties { Id = "lower", ID = "upper" };

        var masked = test.Mask("ID");

        masked.Id.Should().Be("lower");
        masked.ID.Should().BeNull();
    }

    [Fact]
    public void MaskResolvesDerivedPropertiesOnPolymorphicMembers()
    {
        var test = new PolymorphicContainer
        {
            Item = new DerivedItem { Name = "derived", Timestamp = DateTime.UtcNow }
        };

        var masked = test.Mask("Item.Timestamp");

        var item = masked.Item.Should().BeOfType<DerivedItem>().Subject;
        item.Timestamp.Should().Be(default);
        item.Name.Should().Be("derived");
    }

    [Fact]
    public void MaskPreservesRuntimeTypeWhenCalledThroughPolymorphicBaseType()
    {
        var timestamp = DateTime.UtcNow;
        BaseItem test = new DerivedItem { Name = "secret", Timestamp = timestamp };

        var masked = test.Mask("Name");

        var item = masked.Should().BeOfType<DerivedItem>().Subject;
        item.Name.Should().BeNull();
        item.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void MaskPreservesRuntimeTypeWhenCalledThroughNonPolymorphicBaseType()
    {
        Test test = new DerivedTest { Value1 = "secret", Extra = "keep" };

        var masked = test.Mask("Value1");

        var item = masked.Should().BeOfType<DerivedTest>().Subject;
        item.Value1.Should().BeNull();
        item.Extra.Should().Be("keep");
    }

    [Fact]
    public void MaskWildcardDoesNotRemovePolymorphicTypeDiscriminator()
    {
        var test = new PolymorphicContainer
        {
            Item = new DerivedItem { Name = "secret", Timestamp = DateTime.UtcNow }
        };

        var masked = test.Mask("Item.*");

        var item = masked.Item.Should().BeOfType<DerivedItem>().Subject;
        item.Name.Should().BeNull();
        item.Timestamp.Should().Be(default);
    }

    [Fact]
    public void MaskIgnoresExplicitPolymorphicTypeDiscriminator()
    {
        var test = new PolymorphicContainer { Item = new DerivedItem { Name = "keep" } };

        var masked = test.Mask("Item['$type']");

        masked.Item.Should().BeOfType<DerivedItem>().Which.Name.Should().Be("keep");
    }

    [Fact]
    public void MaskPolymorphicItemsInCollections()
    {
        var test = new PolymorphicCollection
        {
            Items = [new DerivedItem { Name = "a", Timestamp = DateTime.UtcNow }, new BaseItem { Name = "b" }]
        };

        var masked = test.Mask("Items[*].Timestamp");

        masked.Items![0].Should().BeOfType<DerivedItem>().Which.Timestamp.Should().Be(default);
        masked.Items[1].Should().BeOfType<BaseItem>().Which.Name.Should().Be("b");
    }

    [Fact]
    public void MaskUsesPropertyLevelEnumConverters()
    {
        var test = new ConvertedMembers { Status = StrictStatus.Active, Other = StrictStatus.Active };

        var masked = test.Mask("Status", "Other");

        masked.Status.Should().Be(StrictStatus.None);
        masked.Other.Should().Be(StrictStatus.None);
    }

    [Fact]
    public void MaskUsesPropertyLevelCustomConverters()
    {
        var test = new ConvertedMembers { Day = new DateTime(2026, 11, 25, 0, 0, 0, DateTimeKind.Unspecified) };

        var masked = test.Mask("Day");

        masked.Day.Should().Be(default);
    }

    [Fact]
    public void MaskSetsBooleansToDefault()
    {
        var test = new BooleanModel { IsAdmin = true, MaybeAdmin = true };

        var masked = test.Mask("IsAdmin", "MaybeAdmin");

        masked.IsAdmin.Should().BeFalse();
        masked.MaybeAdmin.Should().BeNull();
    }

    [Fact]
    public void MaskMasksPublicFieldsOnStructsAndNestedTypes()
    {
        var test = new FieldContainer { Inner = new FieldStruct { Pin = 1234, Code = "secret" } };

        var masked = test.Mask("Inner.Pin", "Inner.Code");

        masked.Inner.Pin.Should().Be(0);
        masked.Inner.Code.Should().BeNull();
    }

    [Fact]
    public void MaskThrowsWhenTargetMemberCannotBeWritten()
    {
        var test = new ReadOnlyMembers();

        var exception = Assert.Throws<InvalidOperationException>(() => test.Mask("Token"));

        exception.Message.Should().Contain("Token");
    }

    [Fact]
    public void MaskRejectsRecursiveJsonPathSelectors()
    {
        var test = new Test { Value1 = "private" };

        var exception = Assert.Throws<ArgumentException>(() => test.Mask("$..Value1"));

        exception.ParamName.Should().Be("propertiesToMask");
    }

    /// <summary>
    /// Test when null is sent in place of the ignore list, an exception is thrown.
    /// </summary>
    [Fact]
    public void MaskNullIgnoreListThrowsArgumentNullException()
    {
        object test = new Test();
        var exception = Assert.Throws<ArgumentNullException>(() => test.Mask(null!));
        exception.ParamName.Should().Be("propertiesToMask");
    }

    /// <summary>
    /// The mask null object throws argument null exception.
    /// </summary>
    [Fact]
    public void MaskNullObjectThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => ((object)null!).Mask());
        exception.ParamName.Should().Be("data");
    }

    /// <summary>
    /// Tests that masking a null object throws <exception cref="ArgumentNullException"></exception>.
    /// </summary>
    [Fact]
    public void NullObjectToJsonThrowsArgumentNullException()
    {
        Test? test = null;
        var exception = Assert.Throws<ArgumentNullException>(() => test!.ToJson());
        exception.ParamName.Should().Be("obj");
    }

    [Fact]
    public void ObjectToJsonShouldNotBeNull()
    {
        object obj = new Test();
        obj.ToJson().Should().NotBeNull();
    }

    [Fact]
    public void ToJsonSerializesObjectWithCompactJson()
    {
        var json = new { Name = "Rudy", Value = 3 }.ToJson();
        json.Should().Be("{\"Name\":\"Rudy\",\"Value\":3}");
    }

    /// <summary>
    /// Tests serialize to json stream.
    /// </summary>
    [Fact]
    public void SerializeToJsonStream()
    {
        var test = new Test2 { Value1 = "test", Value2 = 2 };
        var stream = test.SerializeToJsonStream(new MemoryStream());
        stream.Should().NotBeNull();
        stream.Length.Should().BeGreaterThan(0);
        stream.Position.Should().Be(0);
        stream.CanRead.Should().BeTrue();
    }

    /// <summary>
    /// Tests serialize to json stream null object throws argument null exception.
    /// </summary>
    [Fact]
    public void SerializeToJsonStreamNullObjectThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => default(object)!.SerializeToJsonStream(default!));
    }

    /// <summary>
    /// Tests serialize to json stream null stream throws argument null exception.
    /// </summary>
    [Fact]
    public void SerializeToJsonStreamNullStreamThrowsArgumentNullException()
    {
        var test = new Test2 { Value1 = "test", Value2 = 2 };
        Assert.Throws<ArgumentNullException>(() => test.SerializeToJsonStream(default!));
    }

    /// <summary>
    /// Read-only streams cannot be serialized to. The method must reject them up front
    /// with a clear <see cref="ArgumentException"/> instead of letting
    /// <see cref="JsonSerializer"/> throw a less actionable downstream error.
    /// </summary>
    [Fact]
    public void SerializeToJsonStreamReadOnlyStreamThrowsArgumentException()
    {
        var test = new Test2 { Value1 = "test", Value2 = 2 };
        using var readOnly = new MemoryStream(new byte[16], writable: false);
        var ex = Assert.Throws<ArgumentException>(() => test.SerializeToJsonStream(readOnly));
        ex.ParamName.Should().Be("stream");
    }

    /// <summary>
    /// Non-seekable streams cannot have their position reset to 0 after serialization.
    /// The method must reject them up front instead of throwing the less actionable
    /// <see cref="NotSupportedException"/> from <c>Stream.set_Position</c>.
    /// </summary>
    [Fact]
    public void SerializeToJsonStreamNonSeekableStreamThrowsArgumentException()
    {
        var test = new Test2 { Value1 = "test", Value2 = 2 };
        using var nonSeekable = new NonSeekableWriteStream();
        var ex = Assert.Throws<ArgumentException>(() => test.SerializeToJsonStream(nonSeekable));
        ex.ParamName.Should().Be("stream");
    }

    [Fact]
    public void StringToJsonShouldReturnItself()
    {
        object obj = "test";
        obj.ToJson().Should().BeEquivalentTo(obj.ToString());
    }

    /// <summary>
    /// The test.
    /// </summary>
    public class Test
    {
        /// <summary>
        /// Gets or sets the value 1.
        /// </summary>
        public string? Value1 { get; set; }

        /// <summary>
        /// Gets or sets the value 2.
        /// </summary>
        public int Value2 { get; set; }

        public DateTime Value3 { get; set; }
        public TimeSpan Value4 { get; set; }

        public Test2[]? Value5 { get; set; }
    }

    /// <summary>
    /// The test.
    /// </summary>
    [Serializable]
    public class Test2
    {
        /// <summary>
        /// Gets or sets the value 1.
        /// </summary>
        public string? Value1 { get; set; }

        /// <summary>
        /// Gets or sets the value 2.
        /// </summary>
        public int Value2 { get; set; }

        /// <summary>
        /// Gets or sets the value 2.
        /// </summary>
        public int Value3 { get; set; }
    }

    private sealed class StringEncodedValueTypes
    {
        public Guid Id { get; set; }
        public DateOnly Date { get; set; }
        public TimeOnly Time { get; set; }
        public char Character { get; set; }
    }

    private sealed class NullableStructProperty
    {
        public StructDetails? Details { get; set; }
    }

    private struct StructDetails
    {
        public DateTime Timestamp { get; set; }
    }

    private sealed class PublicFieldModel
    {
#pragma warning disable S1104 // Public fields are the scenario under test.
        public string? Token = "secret";
#pragma warning restore S1104
        public string? Name { get; set; }
    }

    private sealed class CaseDistinctProperties
    {
        public string? Id { get; set; }
#pragma warning disable S100, IDE1006 // Case-distinct names are the scenario under test.
        public string? ID { get; set; }
#pragma warning restore S100, IDE1006
    }

    private sealed class PolymorphicContainer
    {
        public BaseItem? Item { get; set; }
    }

    [System.Text.Json.Serialization.JsonPolymorphic]
    [System.Text.Json.Serialization.JsonDerivedType(typeof(DerivedItem), "derived")]
    private class BaseItem
    {
        public string? Name { get; set; }
    }

    private sealed class DerivedItem : BaseItem
    {
        public DateTime Timestamp { get; set; }
    }

    private sealed class PolymorphicCollection
    {
        public List<BaseItem>? Items { get; set; }
    }

    private enum StrictStatus
    {
        None,
        Active
    }

    private sealed class ConvertedMembers
    {
        [System.Text.Json.Serialization.JsonConverter(typeof(StrictStatusConverter))]
        public StrictStatus Status { get; set; }

        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
        public StrictStatus Other { get; set; }

        [System.Text.Json.Serialization.JsonConverter(typeof(DayOnlyConverter))]
        public DateTime Day { get; set; }
    }

    private sealed class StrictStatusConverter()
        : System.Text.Json.Serialization.JsonStringEnumConverter<StrictStatus>(allowIntegerValues: false);

    private sealed class DayOnlyConverter : System.Text.Json.Serialization.JsonConverter<DateTime>
    {
        public override DateTime Read(
            ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
        {
            return DateTime.ParseExact(
                reader.GetString()!, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        }

        public override void Write(
            System.Text.Json.Utf8JsonWriter writer, DateTime value, System.Text.Json.JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    private sealed class BooleanModel
    {
        public bool IsAdmin { get; set; }
        public bool? MaybeAdmin { get; set; }
    }

    private sealed class FieldContainer
    {
#pragma warning disable S1104 // Public fields are the scenario under test.
        public FieldStruct Inner;
#pragma warning restore S1104
    }

    private struct FieldStruct
    {
#pragma warning disable S1104 // Public fields are the scenario under test.
        public int Pin;
        public string? Code;
#pragma warning restore S1104
    }

    private sealed class ReadOnlyMembers
    {
#pragma warning disable S1104, S2933 // Read-only public field is the scenario under test.
        public readonly string Token = "secret";
#pragma warning restore S1104, S2933
    }

    private sealed class DerivedTest : Test
    {
        public string? Extra { get; set; }
    }

    /// <summary>
    /// Writable, non-seekable stream used to assert
    /// <see cref="ObjectExtensions.SerializeToJsonStream"/> rejects streams whose
    /// position cannot be reset to 0.
    /// </summary>
    private sealed class NonSeekableWriteStream : Stream
    {
        private readonly MemoryStream _inner = new();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}