using System.Security.Cryptography;
using System.Text;
using CodeHollow.FeedReader;

internal class Rss
{
    public static async Task<IEnumerable<RssItem>> getItems(String? url)
    {
        Logger.log($"Getting RSS - {url}");
        var before = DateTime.Now;
        var feed = await FeedReader.ReadAsync(url);

        var rssItems = (from item in feed.Items
                        select new RssItem
                        {
                            Title = item.Title,
                            Link = item.Link,
                            PubDate = DateTime.Parse(item.PublishingDateString)
                        });

        Logger.log($"Got RSS items for {url} in {(DateTime.Now - before).TotalMilliseconds} ms");
        return rssItems;
    }
}

internal class RssItem
{
    public String ID
    {
        get
        {
            return BitConverter.ToString(SHA256.Create().ComputeHash(UTF8Encoding.UTF8.GetBytes(this.Link ?? ""))).Replace("-", "");
        }
    }
    public String? Title { get; init; }
    public String? Link { get; init; }
    public DateTime PubDate { get; init; }

    public override string ToString()
    {
        return $"{this.PubDate.ToString("yyyy.MM.dd HH:mm")}: {this.Title} - {this.Link}";
    }

    public string ToEmailString()
    {
        return $"<div>{PubDate} - {Title}</a><br>{Link}</div>";
    }
}
