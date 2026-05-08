using System;
using System.IO;
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