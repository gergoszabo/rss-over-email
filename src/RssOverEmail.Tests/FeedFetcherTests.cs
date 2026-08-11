using FluentAssertions;
using RssOverEmail.Services;

namespace RssOverEmail.Tests;

public class FeedFetcherTests
{
    [Fact]
    public async Task FetchXmlAsync_ReturnsContentOnSuccess()
    {
        var handler = new TestHttpMessageHandler(request =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("<rss><channel></channel></rss>")
            });

        var sut = new FeedFetcher(new HttpClient(handler));
        var result = await sut.FetchXmlAsync("http://example.com/feed");

        result.Should().Be("<rss><channel></channel></rss>");
    }

    [Fact]
    public async Task FetchXmlAsync_ThrowsOnNonSuccess()
    {
        var handler = new TestHttpMessageHandler(request =>
            new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));

        var sut = new FeedFetcher(new HttpClient(handler));
        var act = () => sut.FetchXmlAsync("http://example.com/feed");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private class TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
