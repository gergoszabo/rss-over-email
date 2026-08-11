using System.ServiceModel.Syndication;
using System.Xml;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

var feeds = builder.Configuration.GetSection("Feeds").Get<FeedConfig[]>() ?? [];
var pollIntervalSeconds = builder.Configuration.GetValue<int>("PollIntervalSeconds", 900);
var dbPath = builder.Configuration.GetValue<string>("Database:Path") ?? "rss.db";
var port = builder.Configuration.GetValue<int>("Port", 8080);

if (feeds.Length == 0)
{
    Console.Error.WriteLine(
        "No feeds configured. Copy appsettings.sample.json to appsettings.json and set the Feeds section.");
    return 1;
}

if (pollIntervalSeconds <= 0)
{
    Console.Error.WriteLine("PollIntervalSeconds must be greater than zero.");
    return 1;
}

Database.Init(dbPath);

builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("rss-over-email/1.0");

builder.Services.AddSingleton(http);
builder.Services.AddSingleton(dbPath);
builder.Services.AddSingleton(new ScanOptions(feeds, pollIntervalSeconds));
builder.Services.AddHostedService<FeedScanner>();

var app = builder.Build();

app.MapGet("/list/unread", () => Results.Json(Database.GetUnread(dbPath)));

app.MapPost("/{id:length(64)}/read", (string id) =>
{
    var updated = Database.MarkRead(dbPath, id.ToLowerInvariant());
    return updated > 0 ? Results.NoContent() : Results.NotFound();
});

app.Run();
return 0;

record FeedConfig(string Name, string Url);

record ScanOptions(FeedConfig[] Feeds, int PollIntervalSeconds);

record UnreadItem(string Id, string Title, string Link, string Feed, string? PublishedAt);

record StoredItem(string Id, string Title, string Link, string Feed, string? PublishedAt, string InsertedAt);

sealed class FeedScanner : BackgroundService
{
    private readonly FeedConfig[] _feeds;
    private readonly int _pollIntervalSeconds;
    private readonly string _dbPath;
    private readonly HttpClient _http;

    public FeedScanner(ScanOptions options, string dbPath, HttpClient http)
    {
        _feeds = options.Feeds;
        _pollIntervalSeconds = options.PollIntervalSeconds;
        _dbPath = dbPath;
        _http = http;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_pollIntervalSeconds));

        while (true)
        {
            await ScanAllAsync(stoppingToken);

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ScanAllAsync(CancellationToken stoppingToken)
    {
        foreach (var feed in _feeds)
        {
            try
            {
                var inserted = await ScanFeedAsync(feed, stoppingToken);
                Console.WriteLine($"[scan] {feed.Name}: {inserted} new item(s)");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[scan] {feed.Name}: failed - {ex.Message}");
            }
        }
    }

    private async Task<int> ScanFeedAsync(FeedConfig feed, CancellationToken stoppingToken)
    {
        using var response = await _http.GetAsync(feed.Url, stoppingToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(stoppingToken);
        using var reader = XmlReader.Create(
            stream,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreWhitespace = true,
            });

        var syndication = SyndicationFeed.Load(reader);

        var inserted = 0;
        foreach (var item in syndication.Items)
        {
            var link = item.Links.FirstOrDefault()?.Uri?.ToString();
            if (string.IsNullOrEmpty(link))
            {
                continue;
            }

            var publishedAt = item.PublishDate == default(DateTimeOffset)
                ? null
                : item.PublishDate.UtcDateTime.ToString("O");

            var stored = new StoredItem(
                FeedHasher.Hash(link),
                item.Title?.Text ?? "(no title)",
                link,
                feed.Name,
                publishedAt,
                DateTime.UtcNow.ToString("O"));

            if (Database.InsertItem(_dbPath, stored))
            {
                inserted++;
            }
        }

        return inserted;
    }
}

static class FeedHasher
{
    public static string Hash(string link)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(link));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

static class Database
{
    public static void Init(string dbPath)
    {
        using var conn = Open(dbPath);
        Execute(conn, "PRAGMA journal_mode=WAL;");
        Execute(
            conn,
            """
            CREATE TABLE IF NOT EXISTS items (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                link TEXT NOT NULL,
                feed TEXT NOT NULL,
                published_at TEXT,
                is_read INTEGER NOT NULL DEFAULT 0,
                inserted_at TEXT NOT NULL
            );
            """);
        Execute(conn, "CREATE INDEX IF NOT EXISTS idx_items_unread ON items (is_read, published_at);");
    }

    public static bool InsertItem(string dbPath, StoredItem item)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            INSERT OR IGNORE INTO items (id, title, link, feed, published_at, inserted_at)
            VALUES (@id, @title, @link, @feed, @published_at, @inserted_at)
            """;
        cmd.Parameters.AddWithValue("@id", item.Id);
        cmd.Parameters.AddWithValue("@title", item.Title);
        cmd.Parameters.AddWithValue("@link", item.Link);
        cmd.Parameters.AddWithValue("@feed", item.Feed);
        cmd.Parameters.AddWithValue("@published_at", (object?)item.PublishedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@inserted_at", item.InsertedAt);
        return cmd.ExecuteNonQuery() > 0;
    }

    public static List<UnreadItem> GetUnread(string dbPath)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, title, link, feed, published_at
            FROM items
            WHERE is_read = 0
            ORDER BY (published_at IS NULL), published_at DESC, inserted_at DESC
            """;
        using var reader = cmd.ExecuteReader();

        var items = new List<UnreadItem>();
        while (reader.Read())
        {
            items.Add(
                new UnreadItem(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return items;
    }

    public static int MarkRead(string dbPath, string id)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE items SET is_read = 1 WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        return cmd.ExecuteNonQuery();
    }

    private static SqliteConnection Open(string dbPath)
    {
        var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA busy_timeout=5000;";
        cmd.ExecuteNonQuery();

        return conn;
    }

    private static void Execute(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
