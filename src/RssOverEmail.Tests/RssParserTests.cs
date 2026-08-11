using System.ServiceModel.Syndication;
using System.Xml;
using FluentAssertions;
using RssOverEmail.Services;

namespace RssOverEmail.Tests;

public class RssParserTests
{
    private readonly RssParser _sut = new();

    [Fact]
    public void Parse_ValidRss_ReturnsItems()
    {
        var xml = """
            <?xml version="1.0"?>
            <rss version="2.0">
                <channel>
                    <item>
                        <title><![CDATA[Test Title]]></title>
                        <link>https://example.com/1</link>
                        <pubDate>Mon, 01 Jan 2024 12:00:00 GMT</pubDate>
                    </item>
                    <item>
                        <title>Second Item</title>
                        <link>https://example.com/2</link>
                        <pubDate>Tue, 02 Jan 2024 12:00:00 GMT</pubDate>
                    </item>
                </channel>
            </rss>
            """;

        var items = _sut.Parse(xml, feedId: 1);

        items.Should().HaveCount(2);
        items[0].Title.Should().Be("Test Title");
        items[0].Link.Should().Be("https://example.com/1");
        items[0].FeedId.Should().Be(1);
        items[1].Title.Should().Be("Second Item");
    }

    [Fact]
    public void Parse_EmptyFeed_ReturnsEmpty()
    {
        var xml = """
            <?xml version="1.0"?>
            <rss version="2.0">
                <channel></channel>
            </rss>
            """;

        var items = _sut.Parse(xml, feedId: 1);

        items.Should().BeEmpty();
    }

    [Fact]
    public void Parse_MalformedXml_Throws()
    {
        var act = () => _sut.Parse("not xml", feedId: 1);

        act.Should().Throw<XmlException>();
    }

    [Fact]
    public void Parse_ItemWithComments_ExtractsComments()
    {
        var xml = """
            <?xml version="1.0"?>
            <rss version="2.0">
                <channel>
                    <item>
                        <title>With Comments</title>
                        <link>https://example.com/1</link>
                        <comments xmlns="http://www.w3.org/2005/Atom">https://example.com/comments</comments>
                        <pubDate>Mon, 01 Jan 2024 12:00:00 GMT</pubDate>
                    </item>
                </channel>
            </rss>
            """;

        var items = _sut.Parse(xml, feedId: 1);

        items.Should().ContainSingle();
        items[0].Comments.Should().Be("https://example.com/comments");
    }

    [Fact]
    public void Parse_GeneratesSha256HashForLink()
    {
        var xml = """
            <?xml version="1.0"?>
            <rss version="2.0">
                <channel>
                    <item>
                        <title>Test</title>
                        <link>https://example.com/unique</link>
                        <pubDate>Mon, 01 Jan 2024 12:00:00 GMT</pubDate>
                    </item>
                </channel>
            </rss>
            """;

        var items = _sut.Parse(xml, feedId: 1);

        items[0].Hash.Should().NotBeNullOrEmpty();
        items[0].Hash.Should().HaveLength(64);
    }

    [Fact]
    public void Parse_SameLink_YieldsSameHash()
    {
        var xml1 = """
            <?xml version="1.0"?>
            <rss version="2.0">
                <channel>
                    <item>
                        <title>A</title>
                        <link>https://example.com/same</link>
                        <pubDate>Mon, 01 Jan 2024 12:00:00 GMT</pubDate>
                    </item>
                </channel>
            </rss>
            """;
        var xml2 = """
            <?xml version="1.0"?>
            <rss version="2.0">
                <channel>
                    <item>
                        <title>B</title>
                        <link>https://example.com/same</link>
                        <pubDate>Mon, 01 Jan 2024 12:00:00 GMT</pubDate>
                    </item>
                </channel>
            </rss>
            """;

        var items1 = _sut.Parse(xml1, feedId: 1);
        var items2 = _sut.Parse(xml2, feedId: 1);

        items1[0].Hash.Should().Be(items2[0].Hash);
    }
}
