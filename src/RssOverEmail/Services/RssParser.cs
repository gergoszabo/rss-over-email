using System.Security.Cryptography;
using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;
using RssOverEmail.Models;

namespace RssOverEmail.Services;

public class RssParser : IRssParser
{
    public IReadOnlyList<FeedItem> Parse(string xml, int feedId)
    {
        using var reader = XmlReader.Create(new StringReader(xml));
        var feed = SyndicationFeed.Load(reader);
        if (feed is null || !feed.Items.Any())
            return [];

        return feed.Items.Select(item => new FeedItem
        {
            FeedId = feedId,
            Title = item.Title?.Text?.Trim() ?? string.Empty,
            Link = item.Links.FirstOrDefault()?.Uri?.ToString()?.Trim() ?? string.Empty,
            PubDate = item.PublishDate.ToString("yyyy-MM-dd HH:mm"),
            Comments = item.ElementExtensions
                .ReadElementExtensions<string>("comments", "http://www.w3.org/2005/Atom")
                .FirstOrDefault(),
            Hash = ComputeHash(item.Links.FirstOrDefault()?.Uri?.ToString() ?? string.Empty),
            CreatedAt = DateTime.UtcNow
        }).ToList();
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
