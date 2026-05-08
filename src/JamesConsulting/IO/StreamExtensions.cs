using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using JamesConsulting.Internal;

namespace JamesConsulting.IO
{
    /// <summary>
    /// Provides extension methods for common <see cref="Stream"/> operations.
    /// </summary>
    public static class StreamExtensions
    {
        /// <summary>
        /// Determines whether a stream represents a Windows PE/EXE file by checking the first two bytes for the "MZ" signature.
        /// </summary>
        /// <param name="stream">The stream to inspect. Must be readable and seekable.</param>
        /// <returns><c>true</c> if the first two bytes match "MZ"; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <c>null</c>.</exception>
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
            var firstBytes = new byte[2];
            stream.Position = 0;
            var read = stream.Read(firstBytes, 0, 2);
            return read == 2 && Encoding.UTF8.GetString(firstBytes) == "MZ";
        }

        /// <summary>
        /// Deserializes the entire textual content of a stream to a CLR object using <c>Newtonsoft.Json</c>.
        /// </summary>
        /// <typeparam name="T">Target CLR type for the JSON payload.</typeparam>
        /// <param name="stream">The source stream positioned at the beginning of the JSON document.</param>
        /// <returns>An instance of <typeparamref name="T"/> or <c>null</c> if the JSON represents <c>null</c>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <c>null</c>.</exception>
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
            using var sr = new StreamReader(stream);
            using JsonReader reader = new JsonTextReader(sr);
            var serializer = new JsonSerializer();
            return serializer.Deserialize<T>(reader);
        }
    }
}