using System;
using System.IO;
using System.Text;
using System.Text.Json;
using JamesConsulting.Internal;

namespace JamesConsulting.IO;

/// <summary>
/// Provides extension methods for common <see cref="Stream" /> operations.
/// </summary>
public static class StreamExtensions
{
    /// <summary>
    /// Determines whether a stream represents a Windows PE/EXE file by checking the first two bytes for the "MZ" signature.
    /// </summary>
    /// <param name="stream">The stream to inspect. Must be readable and seekable.</param>
    /// <returns><c>true</c> if the first two bytes match "MZ"; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="stream" /> is not readable (<see cref="Stream.CanRead" /> is <c>false</c>) or
    /// not seekable (<see cref="Stream.CanSeek" /> is <c>false</c>). The MZ signature lives at offset 0,
    /// so the implementation must seek to <c>0</c> and restore the original <see cref="Stream.Position" />.
    /// </exception>
    /// <remarks>
    /// The original <see cref="Stream.Position" /> is restored on return. Non-seekable streams
    /// (e.g. <c>NetworkStream</c>) are rejected up front to avoid raising an opaque
    /// <see cref="NotSupportedException" /> from inside the read/restore sequence.
    /// </remarks>
    /// <example>
    /// Detect executable signature.
    /// <code>
    /// using var ms = new MemoryStream();
    /// using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen:true);
    /// writer.Write('M');
    /// writer.Write('Z');
    /// writer.Write("&lt;payload&gt;");
    /// writer.Flush();
    /// var isExe = ms.IsExecutable(); // true
    /// ms.Position = 0;
    /// using var nonExe = new MemoryStream();
    /// var isExe2 = nonExe.IsExecutable(); // false
    /// </code>
    /// </example>
    public static bool IsExecutable(this Stream stream)
    {
        Guard.NotNull(stream);
        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(stream));
        if (!stream.CanSeek)
            throw new ArgumentException(
                "Stream must support seeking so the original Position can be restored.", nameof(stream));

        var originalPosition = stream.Position;
        try
        {
            stream.Position = 0;
            Span<byte> buffer = stackalloc byte[2];
            var total = 0;
            while (total < 2)
            {
                var read = stream.Read(buffer.Slice(total));
                if (read == 0) return false;
                total += read;
            }

            return buffer[0] == (byte)'M' && buffer[1] == (byte)'Z';
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    /// <summary>
    /// Deserializes JSON from a stream to a CLR object.
    /// </summary>
    /// <typeparam name="T">Target CLR type for the JSON payload.</typeparam>
    /// <param name="stream">The source stream positioned at the beginning of the JSON document.</param>
    /// <returns>An instance of <typeparamref name="T" /> or <c>null</c> if the JSON represents <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream" /> is <c>null</c>.</exception>
    /// <exception cref="JsonException">The JSON is invalid or cannot be converted to <typeparamref name="T" />.</exception>
    /// <remarks>The stream remains open. UTF-8 is parsed directly; UTF-16/UTF-32 BOM input is transcoded before parsing.</remarks>
    /// <example>
    /// Deserialize JSON from stream.
    /// <code>
    /// [Serializable]
    /// public class MyClass
    /// {
    ///     public string Property1 { get; }
    ///     public int Property2 { get; }
    ///     public MyClass(string p1, int p2)
    ///     {
    ///         Property1 = p1;
    ///         Property2 = p2;
    ///     }
    /// }
    /// 
    /// var instance = new MyClass("Test", 3);
    /// var jsonStream = instance.SerializeToJsonStream(new MemoryStream());
    /// var roundTrip = jsonStream.Deserialize&lt;MyClass&gt;();
    /// </code>
    /// </example>
    public static T? Deserialize<T>(this Stream stream)
    {
        Guard.NotNull(stream);
        var (prefix, requiresTranscoding) = ReadJsonPrefix(stream);
        using var prefixedStream = new PrefixStream(prefix, stream);
        if (requiresTranscoding)
        {
            using var reader = new StreamReader(
                prefixedStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
                bufferSize: 1024, leaveOpen: true);
            return JsonSerializer.Deserialize<T>(reader.ReadToEnd(), ObjectExtensions.SerializerOptions);
        }

        return JsonSerializer.Deserialize<T>(prefixedStream, ObjectExtensions.SerializerOptions);
    }

    private static (byte[] Prefix, bool RequiresTranscoding) ReadJsonPrefix(Stream stream)
    {
        var prefix = new byte[4];
        var length = 0;
        var first = ReadByte();

        if (first == 0xFE)
        {
            var second = ReadByte();
            return (prefix[..length], second == 0xFF);
        }

        if (first == 0xFF && ReadByte() == 0xFE)
        {
            var third = ReadByte();
            if (third != 0)
                return (prefix[..length], true);

            var fourth = ReadByte();
            return (prefix[..length], fourth is 0 or -1);
        }

        if (first == 0x00 && ReadByte() == 0x00 && ReadByte() == 0xFE)
        {
            var fourth = ReadByte();
            return (prefix[..length], fourth == 0xFF);
        }

        if (first == 0xEF && ReadByte() == 0xBB)
            ReadByte();

        return (prefix[..length], false);

        int ReadByte()
        {
            var value = stream.ReadByte();
            if (value >= 0)
                prefix[length++] = (byte)value;
            return value;
        }
    }

    private sealed class PrefixStream(byte[] prefix, Stream inner) : Stream
    {
        private int _position;

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position < prefix.Length)
            {
                var copied = Math.Min(count, prefix.Length - _position);
                Buffer.BlockCopy(prefix, _position, buffer, offset, copied);
                _position += copied;
                return copied;
            }

            return inner.Read(buffer, offset, count);
        }

        public override int Read(Span<byte> buffer)
        {
            if (_position < prefix.Length)
            {
                var copied = Math.Min(buffer.Length, prefix.Length - _position);
                prefix.AsSpan(_position, copied).CopyTo(buffer);
                _position += copied;
                return copied;
            }

            return inner.Read(buffer);
        }

        public override int ReadByte()
        {
            return _position < prefix.Length ? prefix[_position++] : inner.ReadByte();
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}