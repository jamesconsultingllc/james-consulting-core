using System;
using System.IO;
using System.Text;
using FluentAssertions;
using JamesConsulting.IO;
using System.Text.Json;
using Xunit;

namespace JamesConsulting.Tests.IO;

public class StreamExtensionsTests
{
    [Fact]
    public void DeserializeStreamRecreatesObject()
    {
        var test = new MyClass("Test", 3);
        var ms = test.SerializeToJsonStream(new MemoryStream());
        var newTest = JsonSerializer.Deserialize<MyClass>(ms);
        newTest.Should().NotBeNull();
        newTest.Should().Be(test);
    }

    /// <summary>
    /// <see cref="StreamExtensions.Deserialize{T}"/> uses an internal
    /// <see cref="StreamReader"/>; the default StreamReader behavior is to dispose the
    /// underlying stream when itself disposed. This regression test pins the
    /// leaveOpen=true contract so callers can keep using their stream after the call.
    /// </summary>
    [Fact]
    public void DeserializeLeavesCallerStreamOpen()
    {
        var test = new MyClass("Test", 3);
        using var ms = (MemoryStream)test.SerializeToJsonStream(new MemoryStream());
        _ = ms.Deserialize<MyClass>();
        // CanRead would be false on a disposed MemoryStream.
        ms.CanRead.Should().BeTrue("the caller-provided stream must remain open after Deserialize returns");
    }

    [Fact]
    public void DeserializeMatchesPropertyNamesWithoutCaseSensitivity()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            "{\"property1\":\"Test\",\"property2\":3}"));

        var result = stream.Deserialize<MyClass>();

        result.Should().Be(new MyClass("Test", 3));
        stream.CanRead.Should().BeTrue();
    }

    [Fact]
    public void DeserializeSupportsUtf16BigEndianBom()
    {
        using var stream = new MemoryStream();
        stream.Write(Encoding.BigEndianUnicode.GetPreamble());
        stream.Write(Encoding.BigEndianUnicode.GetBytes("{\"Property1\":\"Test\",\"Property2\":3}"));
        stream.Position = 0;

        var result = stream.Deserialize<MyClass>();

        result.Should().Be(new MyClass("Test", 3));
    }

    [Fact]
    public void DeserializeSupportsUtf32Bom()
    {
        var encoding = new UTF32Encoding(bigEndian: false, byteOrderMark: true);
        using var stream = new MemoryStream();
        stream.Write(encoding.GetPreamble());
        stream.Write(encoding.GetBytes("{\"Property1\":\"Test\",\"Property2\":3}"));
        stream.Position = 0;

        var result = stream.Deserialize<MyClass>();

        result.Should().Be(new MyClass("Test", 3));
    }

    [Fact]
    public void DeserializeSupportsUtf32BigEndianBom()
    {
        var encoding = new UTF32Encoding(bigEndian: true, byteOrderMark: true);
        using var stream = new MemoryStream();
        stream.Write(encoding.GetPreamble());
        stream.Write(encoding.GetBytes("{\"Property1\":\"Test\",\"Property2\":3}"));
        stream.Position = 0;

        var result = stream.Deserialize<MyClass>();

        result.Should().Be(new MyClass("Test", 3));
    }

    [Fact]
    public void DeserializeSupportsNonSeekableStream()
    {
        using var stream = new NonSeekableReadStream(
            Encoding.UTF8.GetBytes("{\"Property1\":\"Test\",\"Property2\":3}"));

        var result = stream.Deserialize<MyClass>();

        result.Should().Be(new MyClass("Test", 3));
        stream.CanRead.Should().BeTrue();
    }

    [Fact]
    public void DeserializeSupportsUtf8Bom()
    {
        using var stream = new MemoryStream();
        stream.Write(Encoding.UTF8.GetPreamble());
        stream.Write(Encoding.UTF8.GetBytes("{\"Property1\":\"Test\",\"Property2\":3}"));
        stream.Position = 0;

        var result = stream.Deserialize<MyClass>();

        result.Should().Be(new MyClass("Test", 3));
    }

    [Fact]
    public void DeserializeSupportsUtf16Bom()
    {
        using var stream = new MemoryStream();
        stream.Write(Encoding.Unicode.GetPreamble());
        stream.Write(Encoding.Unicode.GetBytes("{\"Property1\":\"Test\",\"Property2\":3}"));
        stream.Position = 0;

        var result = stream.Deserialize<MyClass>();

        result.Should().Be(new MyClass("Test", 3));
    }

    [Fact]
    public void IsExecutableExeStream()
    {
        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream, Encoding.UTF8);
        writer.Write('M');
        writer.Write('Z');
        writer.Write("<Z1234239075032850jfddfjsldfjsdf");
        writer.Flush();
        stream.IsExecutable().Should().BeTrue();
    }

    [Fact]
    public void IsExecutableNonExeStream()
    {
        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream, Encoding.UTF8);
        writer.Write('Z');
        writer.Write("<Z1234239075032850jfddfjsldfjsdf");
        writer.Flush();
        stream.IsExecutable().Should().BeFalse();
    }

    [Fact]
    public void IsExecutableThrowsArgumentNullExceptionWhenStreamIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => default(Stream)!.IsExecutable());
    }

    [Fact]
    public void IsExecutableThrowsArgumentExceptionWhenStreamIsNotReadable()
    {
        using var writeOnly = new WriteOnlyStream();
        var ex = Assert.Throws<ArgumentException>(() => writeOnly.IsExecutable());
        ex.ParamName.Should().Be("stream");
        ex.Message.Should().Contain("readable");
    }

    [Fact]
    public void IsExecutableThrowsArgumentExceptionWhenStreamIsNotSeekable()
    {
        using var nonSeekable = new NonSeekableReadStream(new byte[] { (byte)'M', (byte)'Z' });
        var ex = Assert.Throws<ArgumentException>(() => nonSeekable.IsExecutable());
        ex.ParamName.Should().Be("stream");
        ex.Message.Should().Contain("seek");
    }

    [Fact]
    public void DeserializeThrowsArgumentNullExceptionWhenStreamIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => default(Stream)!.Deserialize<object>());
    }

    [Fact]
    public void Deserialize()
    {
        var test = new MyClass("Test", 3);
        var ms = test.SerializeToJsonStream(new MemoryStream());
        var newTest = ms.Deserialize<MyClass>();
        newTest.Should().NotBeNull();
        newTest.Should().Be(test);
    }

    [Serializable]
    public class MyClass
    {
        public MyClass(string property1, int property2)
        {
            Property1 = property1;
            Property2 = property2;
        }

        public string Property1 { get; }
        public int Property2 { get; }

        public override bool Equals(object? obj)
        {
            if (obj is not MyClass myClass)
                return false;

            return myClass.Property1 == Property1 && myClass.Property2 == Property2;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Property1, Property2);
        }
    }

    private sealed class WriteOnlyStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get; set; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => 0;
        public override void SetLength(long value) { }
        public override void Write(byte[] buffer, int offset, int count) { }
    }

    private sealed class NonSeekableReadStream : Stream
    {
        private readonly MemoryStream _inner;
        public NonSeekableReadStream(byte[] data) { _inner = new MemoryStream(data); }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
    }
}