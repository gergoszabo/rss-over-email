using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using RssOverEmail.Models;
using RssOverEmail.Services;

namespace RssOverEmail.Tests;

public class RssFetchServiceTests
{
    [Fact]
    public async Task ExecuteAsync_AddsMissingFeedThenFetchesItems()
    {
        var feedFetcher = Substitute.For<IFeedFetcher>();
        var rssParser = Substitute.For<IRssParser>();
        var feedRepo = Substitute.For<IFeedRepository>();
        var itemRepo = Substitute.For<IItemRepository>();
        var options = Substitute.For<IOptionsMonitor<RssOverEmailOptions>>();
        var logger = Substitute.For<ILogger<RssFetchService>>();

        options.CurrentValue.Returns(new RssOverEmailOptions
        {
            Feeds = [new FeedConfig { Name = "Test Feed", Url = "https://example.com/rss" }],
            FetchIntervalMinutes = 1
        });

        var feedEntity = new Feed { Id = 1, Name = "Test Feed", Url = "https://example.com/rss" };

        var getAllCallCount = 0;
        feedRepo.GetAllAsync(default)
            .ReturnsForAnyArgs(_ =>
            {
                getAllCallCount++;
                // First call (EnsureFeedsExistAsync) — no feeds yet
                // Second call (FetchAndStoreAllFeedsAsync) — feed exists
                return Task.FromResult(getAllCallCount switch
                {
                    1 => new List<Feed>(),
                    _ => new List<Feed> { feedEntity }
                });
            });

        feedFetcher.FetchXmlAsync(default!, default)
            .ReturnsForAnyArgs(Task.FromResult("<rss></rss>"));

        rssParser.Parse(default!, default)
            .ReturnsForAnyArgs(new List<FeedItem>
            {
                new() { FeedId = 1, Title = "Item 1", Link = "https://ex.com/1", Hash = "hash1" }
            });

        itemRepo.AddNewItemsAsync(default!, default)
            .ReturnsForAnyArgs(1);

        using var cts = new CancellationTokenSource();
        var service = new RssFetchService(feedFetcher, rssParser, feedRepo, itemRepo, options, logger);

        _ = service.StartAsync(cts.Token);
        await Task.Delay(500);
        await service.StopAsync(cts.Token);

        feedRepo.Received(1).AddAsync(
            Arg.Is<Feed>(f => f.Name == "Test Feed" && f.Url == "https://example.com/rss"),
            Arg.Any<CancellationToken>());

        itemRepo.Received(1).AddNewItemsAsync(
            Arg.Any<IEnumerable<FeedItem>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotAddFeedWhenAlreadyExists()
    {
        var feedFetcher = Substitute.For<IFeedFetcher>();
        var rssParser = Substitute.For<IRssParser>();
        var feedRepo = Substitute.For<IFeedRepository>();
        var itemRepo = Substitute.For<IItemRepository>();
        var options = Substitute.For<IOptionsMonitor<RssOverEmailOptions>>();
        var logger = Substitute.For<ILogger<RssFetchService>>();

        options.CurrentValue.Returns(new RssOverEmailOptions
        {
            Feeds = [new FeedConfig { Name = "Existing Feed", Url = "https://example.com/rss" }],
            FetchIntervalMinutes = 1
        });

        feedRepo.GetAllAsync(default)
            .ReturnsForAnyArgs(Task.FromResult(new List<Feed>
            {
                new() { Id = 1, Name = "Existing Feed", Url = "https://example.com/rss" }
            }));

        feedFetcher.FetchXmlAsync(default!, default)
            .ReturnsForAnyArgs(Task.FromResult("<rss></rss>"));
        rssParser.Parse(default!, default)
            .ReturnsForAnyArgs(new List<FeedItem>());

        using var cts = new CancellationTokenSource();
        var service = new RssFetchService(feedFetcher, rssParser, feedRepo, itemRepo, options, logger);

        _ = service.StartAsync(cts.Token);
        await Task.Delay(500);
        await service.StopAsync(cts.Token);

        feedRepo.DidNotReceive().AddAsync(Arg.Any<Feed>(), Arg.Any<CancellationToken>());
    }
}
