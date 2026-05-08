using System;
using System.IO;
using System.Text;
using FluentAssertions;
using JamesConsulting.IO;
using Utf8Json;
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
#if NETSTANDARD2_0
                var hashcode = 35203352;
                var offset = -1521134295;
                hashcode *= offset + Property1.GetHashCode();
                hashcode *= offset + Property2.GetHashCode();
                return hashcode;
#else
            return HashCode.Combine(Property1, Property2);
#endif
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