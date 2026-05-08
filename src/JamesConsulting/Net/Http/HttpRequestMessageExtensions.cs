using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using JamesConsulting.Internal;

namespace JamesConsulting.Net.Http;

/// <summary>
/// Provides extension methods for configuring <see cref="HttpRequestMessage" /> instances.
/// </summary>
public static class HttpRequestMessageExtensions
{
    /// <summary>
    /// Replaces all current headers with the provided key/value pairs.
    /// </summary>
    /// <param name="httpRequestMessage">The request message to modify.</param>
    /// <param name="headers">A collection of headers to set.</param>
    /// <returns>The same <see cref="HttpRequestMessage" /> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="httpRequestMessage" /> or <paramref name="headers" /> is
    /// <c>null</c>.
    /// </exception>
    /// <example>
    /// Replace headers.
    /// <code>
    /// var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
    /// request.Headers.Add("Old", "Value");
    /// request.SetHeaders(new Dictionary&lt;string,string&gt;{ { "X-Test", "123" }, { "X-Other","456" } });
    /// // Now only X-Test and X-Other remain.
    /// </code>
    /// </example>
    public static HttpRequestMessage SetHeaders(this HttpRequestMessage httpRequestMessage,
        IDictionary<string, string> headers)
    {
        Guard.NotNull(httpRequestMessage);
        Guard.NotNull(headers);
        if (httpRequestMessage.Headers.Any()) httpRequestMessage.Headers.Clear();
        foreach (var header in headers) httpRequestMessage.Headers.Add(header.Key, header.Value);
        return httpRequestMessage;
    }
}