# .NET 10 Rewrite: rss-over-email

## 1. Project Structure

```
RssOverEmail/
├── RssOverEmail.sln
├── src/RssOverEmail/
│   ├── RssOverEmail.csproj
│   ├── Program.cs                        — Host setup, DI registration, top-level loop
│   ├── appsettings.json                  — SMTP + feeds config
│   ├── Models/
│   │   └── RssOverEmailOptions.cs        — Strongly-typed options (MailConfig, FeedConfig[])
│   ├── Services/
│   │   ├── IFeedFetcher.cs / FeedFetcher.cs         — HTTP fetch RSS XML
│   │   ├── IRssParser.cs / RssParser.cs             — Parse XML → FeedItem[]
│   │   ├── IStateStore.cs / FileStateStore.cs       — Read/write db.json, dedup by hash
│   │   ├── IEmailSender.cs / SmtpEmailSender.cs     — SendMail via SMTP (MailKit)
│   │   └── IFeedProcessor.cs / FeedProcessor.cs     — Orchestrator: fetch→parse→filter→send→save
│   └── Extensions/
│       └── ServiceCollectionExtensions.cs           — DI registration helpers
└── tests/RssOverEmail.Tests/
    ├── RssOverEmail.Tests.csproj
    ├── FeedFetcherTests.cs
    ├── RssParserTests.cs
    ├── StateStoreTests.cs
    ├── SmtpEmailSenderTests.cs
    └── FeedProcessorTests.cs
```

## 2. Service Contracts & Responsibilities

| Interface | Responsibility | Key Method |
|---|---|---|
| `IFeedFetcher` | HTTP GET feed XML from URL | `Task<string> FetchXmlAsync(string url)` |
| `IRssParser` | Parse RSS XML → feed items | `IReadOnlyList<FeedItem> Parse(string xml)` |
| `IStateStore` | Persist/query seen item hashes | `bool IsNew(string hash); Task SaveAsync(FeedItem item)` |
| `IEmailSender` | Send email via SMTP | `Task SendAsync(string to, string subject, string html)` |
| `IFeedProcessor` | Orchestrate per-feed workflow | `Task ProcessFeedAsync(FeedConfig config)` |

**FeedItem model:**
```
record FeedItem(string Title, string Link, string PubDate, string? Comments, string Hash)
```

## 3. Technology Choices

| Concern | Library/API | Why |
|---|---|---|
| RSS parsing | `System.ServiceModel.Syndication` NuGet | Built-in, handles RSS 2.0 / Atom, no manual XML wrangling |
| SMTP | `MailKit` NuGet | Modern, async-first, better auth support than `SmtpClient` |
| HTTP | `HttpClient` (via `IHttpClientFactory`) | Native, DI-friendly, connection pooling |
| Config | `Microsoft.Extensions.Configuration` + `IOptions<T>` | Standard .NET pattern for JSON config |
| DI | `Microsoft.Extensions.DependencyInjection` | Built-in |
| Logging | `Microsoft.Extensions.Logging` (`ILogger<T>`) | Structured logging, console provider |
| Testing | xUnit + NSubstitute + FluentAssertions | Industry standard, clean mocks |
| Host | `Microsoft.Extensions.Hosting` | Graceful shutdown, DI container, config, logging |

## 4. Configuration (appsettings.json)

```json
{
  "Mail": {
    "Server": "smtp.example.com",
    "Port": 587,
    "Username": "user",
    "Password": "pass",
    "From": "sender@example.com",
    "To": "recipient@example.com"
  },
  "Feeds": [
    { "Name": "Abc", "Url": "https://ab.c/rss" }
  ],
  "State": {
    "FilePath": "./db.json"
  }
}
```

Bound to `RssOverEmailOptions` record via `IOptions<RssOverEmailOptions>`.

## 5. Key Design Decisions

- **Async all the way** — every I/O operation is async
- **No static/singleton state** — all services registered in DI (transient or singleton based on thread-safety)
- **FileStateStore is `Scoped`** — reads file once per scope, buffers in-memory, writes on `SaveAsync`
- **Hash computation** — SHA256 of the link (same as current), lives in `FeedItem` constructor or a utility
- **Error handling** — `FeedProcessor` wraps per-feed execution in try/catch, logs error, optionally sends error email (same as current behavior)
- **No external state files created at startup** — `FileStateStore` handles missing file gracefully (empty state)

## 6. Program.cs (Hosted Service Approach)

```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, svc) =>
    {
        svc.Configure<RssOverEmailOptions>(ctx.Configuration);
        svc.AddHttpClient<IFeedFetcher, FeedFetcher>();
        svc.AddSingleton<IStateStore, FileStateStore>();
        svc.AddTransient<IRssParser, RssParser>();
        svc.AddTransient<IEmailSender, SmtpEmailSender>();
        svc.AddTransient<IFeedProcessor, FeedProcessor>();
    })
    .Build();

var processor = host.Services.GetRequiredService<IFeedProcessor>();
var options = host.Services.GetRequiredService<IOptions<RssOverEmailOptions>>();

foreach (var feed in options.Value.Feeds)
{
    await processor.ProcessFeedAsync(feed);
}
```

## 7. Unit Tests Plan

| Test Class | Key Test Cases |
|---|---|
| `RssParserTests` | Parse valid RSS XML → returns expected items; handles empty feed; handles malformed XML; extracts CDATA content; handles missing optional fields (comments) |
| `FeedFetcherTests` | Mock `HttpMessageHandler` — returns XML on success, throws on HTTP error |
| `StateStoreTests` | First time → `IsNew` returns true; after save → `IsNew` returns false; persists across reloads |
| `SmtpEmailSenderTests` | Mock `ISmtpClient` — verify `SendAsync` is called with correct params; verify error propagation |
| `FeedProcessorTests` | Integration-style with all mocks — verify full flow: fetch→parse→filter→send→save; verify no email if no new items; verify error email on failure |

## 8. CI / Build

- `dotnet restore` / `dotnet build` / `dotnet test`
- Single-file publish: `dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true`

## 9. Migration Notes

- No need for `cheerio` — `SyndicationFeed` handles RSS parsing natively
- No need for `nodemailer` — `MailKit` replaces it
- `db.json` format stays compatible (array of `{id, title, link, comments}`)
- The output HTML format stays the same (`<div>pubdate - title<br><a href="link">link</a><br>comments</div><br>`)
