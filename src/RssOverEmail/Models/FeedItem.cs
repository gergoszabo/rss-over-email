namespace RssOverEmail.Models;

public class FeedItem
{
    public int Id { get; set; }
    public int FeedId { get; set; }
    public Feed Feed { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public string? PubDate { get; set; }
    public string Hash { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
