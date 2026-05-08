using System;
using System.IO;
using System.Text;
using JamesConsulting.Internal;
using Newtonsoft.Json;

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
#if NETSTANDARD2_0
            var buffer = new byte[2];
            var total = 0;
            while (total < 2)
            {
                var read = stream.Read(buffer, total, 2 - total);
                if (read == 0) return false;
                total += read;
            }

            return buffer[0] == (byte)'M' && buffer[1] == (byte)'Z';
#else
            Span<byte> buffer = stackalloc byte[2];
            var total = 0;
            while (total < 2)
            {
                var read = stream.Read(buffer.Slice(total));
                if (read == 0) return false;
                total += read;
            }

            return buffer[0] == (byte)'M' && buffer[1] == (byte)'Z';
#endif
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    /// <summary>
    /// Deserializes the entire textual content of a stream to a CLR object using <c>Newtonsoft.Json</c>.
    /// </summary>
    /// <typeparam name="T">Target CLR type for the JSON payload.</typeparam>
    /// <param name="stream">The source stream positioned at the beginning of the JSON document.</param>
    /// <returns>An instance of <typeparamref name="T" /> or <c>null</c> if the JSON represents <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream" /> is <c>null</c>.</exception>
    /// <exception cref="JsonException">Invalid JSON content.</exception>
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
        // leaveOpen=true keeps the caller-provided stream alive after the StreamReader and
        // JsonTextReader are disposed. The default StreamReader behavior would close the
        // underlying stream, surprising callers that expected to reuse or dispose it
        // themselves. We pass Encoding.UTF8 (which detects and consumes a BOM if present)
        // and use the four-argument constructor available on every supported TFM.
        using var sr = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024, leaveOpen: true);
        using JsonReader reader = new JsonTextReader(sr);
        var serializer = new JsonSerializer();
        return serializer.Deserialize<T>(reader);
    }
}