using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using FluentAssertions;
using JamesConsulting.Net.Http;
using Xunit;

namespace JamesConsulting.Tests.Net.Http;

public class HttpRequestMessageExtensionsTests
{
    [Fact]
    public void SetHeadersResultHeaderListShouldMatchHeadersPassedIn()
    {
        var requestMessage = new HttpRequestMessage();
        requestMessage.Headers.Add("Test", "Test");

        IDictionary<string, string> headers = new Dictionary<string, string>
            { { "Test2", "Test2 " }, { "Test3", "Test3 " } };
        requestMessage.SetHeaders(headers);

        requestMessage.Headers.Contains("Test").Should().BeFalse();
        requestMessage.Headers.Count().Should().Be(2);
    }

    [Fact]
    public void SetHeadersThrowsArgumentNullExceptionWhenHeadersIsNull()
    {
        var requestMessage = new HttpRequestMessage();
        Assert.Throws<ArgumentNullException>(() => requestMessage.SetHeaders(default!));
    }

    [Fact]
    public void SetHeadersThrowsArgumentNullExceptionWhenRequestMessageIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => default(HttpRequestMessage)!.SetHeaders(default!));
    }
}