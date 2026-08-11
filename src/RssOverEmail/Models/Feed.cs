namespace RssOverEmail.Models;

public class Feed
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public List<FeedItem> Items { get; set; } = [];
}
