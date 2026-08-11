# .NET 10 Rewrite: rss-over-email — API + React Native

Replaces email delivery with a SQLite-backed API and an Expo React Native client.

---

## 1. Repository Layout

```
rss-over-email/
├── src/
│   └── RssOverEmail/                    — .NET 10 Web API + Background Worker
│       ├── RssOverEmail.csproj
│       ├── Program.cs                   — WebApplication builder, DI, middleware
│       ├── appsettings.json             — Feeds, DB path, fetch interval
│       ├── Data/
│       │   ├── AppDbContext.cs          — EF Core context (SQLite)
│       │   └── Migrations/             — EF migrations
│       ├── Models/
│       │   ├── Feed.cs                  — DB entity: Id, Name, Url
│       │   ├── FeedItem.cs              — DB entity: Id, FeedId, Title, Link, Hash, IsRead, etc.
│       │   └── RssOverEmailOptions.cs   — Strongly-typed config sections
│       ├── Services/
│       │   ├── IFeedFetcher.cs / FeedFetcher.cs
│       │   ├── IRssParser.cs / RssParser.cs
│       │   ├── IFeedRepository.cs / FeedRepository.cs
│       │   ├── IItemRepository.cs / ItemRepository.cs
│       │   └── RssFetchService.cs       — BackgroundService, periodic fetch
│       └── Controllers/
│           ├── ItemsController.cs       — GET /api/items, PATCH /api/items/{id}/read
│           └── FeedsController.cs       — GET /api/feeds
├── app/                                 — Expo React Native app
│   ├── package.json
│   ├── app.json
│   ├── App.tsx                          — Root component, navigation
│   ├── src/
│   │   ├── api/
│   │   │   └── client.ts               — fetch wrapper, base URL config
│   │   ├── screens/
│   │   │   └── ItemsScreen.tsx          — FlatList of items, pull-to-refresh
│   │   ├── components/
│   │   │   └── ItemCard.tsx            — Single item row (title, date, link, read toggle)
│   │   ├── types/
│   │   │   └── index.ts                — FeedItem, Feed types
│   │   └── utils/
│   │       └── date.ts                 — Date formatting
│   └── ...
└── api-mobile-plan.md                   — This file
```

## 2. Database Schema (EF Core / SQLite)

```csharp
class Feed
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Url { get; set; }
    public List<FeedItem> Items { get; set; }
}

class FeedItem
{
    public int Id { get; set; }
    public int FeedId { get; set; }
    public Feed Feed { get; set; }
    public string Title { get; set; }
    public string Link { get; set; }
    public string? Comments { get; set; }
    public string? PubDate { get; set; }
    public string Hash { get; set; }           // SHA256 of link — dedup key
    public bool IsRead { get; set; }           // unread by default
    public DateTime CreatedAt { get; set; }
}
```

- `Hash` column has a unique index — prevents duplicate inserts at the DB level.
- Migrations run automatically on startup (`context.Database.Migrate()`).

## 3. API Endpoints

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/feeds` | List configured feeds (name + id) |
| `GET` | `/api/items?feedId=&read=&page=&pageSize=` | Paginated items, optional filters |
| `PATCH` | `/api/items/{id}/read` | Toggle read status — body `{ "isRead": true/false }` |

**GET /api/items** response shape:
```json
{
  "items": [
    {
      "id": 1,
      "feedName": "Abc",
      "title": "...",
      "link": "https://...",
      "comments": "https://...",
      "pubDate": "2026-07-29 14:30",
      "isRead": false,
      "createdAt": "2026-07-29T14:30:00Z"
    }
  ],
  "totalCount": 42,
  "page": 1,
  "pageSize": 50
}
```

## 4. Background Worker (`RssFetchService`)

- Inherits `BackgroundService`
- Runs on a timer (configurable via `appsettings.json`, e.g. every 15 min)
- On each tick:
  1. Read feeds from `appsettings.json` (reload options on each tick)
  2. For each feed: fetch XML → parse → filter new items by hash → bulk insert into DB
- Graceful cancellation via `CancellationToken`
- Errors are logged, never crash the host

## 5. Configuration (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "Sqlite": "Data Source=rss.db"
  },
  "Feeds": [
    { "Name": "Hacker News", "Url": "https://hnrss.org/frontpage" }
  ],
  "FetchIntervalMinutes": 15
}
```

## 6. Technology Stack

| Layer | Choice |
|---|---|
| Framework | .NET 10 — ASP.NET Core minimal API / controllers |
| ORM | EF Core + `Microsoft.EntityFrameworkCore.Sqlite` |
| RSS parsing | `System.ServiceModel.Syndication` |
| HTTP fetching | `HttpClient` via `IHttpClientFactory` |
| Background work | `BackgroundService` / `IHostedService` (built-in) |
| Testing | xUnit + NSubstitute + FluentAssertions |
| Client | Expo (managed workflow) + TypeScript |
| Navigation | `@react-navigation/native` + stack navigator |
| HTTP in app | plain `fetch` (built-in) |

## 7. React Native (Expo) App

**Single screen — ItemsScreen:**

- `FlatList` renders `ItemCard` components
- `ItemCard` shows:
  - Feed name (badge/label)
  - Title (bold if unread, normal if read)
  - PubDate (formatted nicely)
  - Link (truncated, tappable → opens external browser via `Linking.openURL`)
  - Comments link if present
- **Tap** on the card body → calls `PATCH /api/items/{id}/read` to toggle read status, optimistically updates local state
- **Long press or dedicated button** → same toggle (alternative UX)
- **Pull-to-refresh** → re-fetches `GET /api/items`
- Visual distinction: unread items have bold title + subtle background tint; read items are dimmed
- Simple header showing total unread count

**API client (`src/api/client.ts`):**
```typescript
const BASE_URL = "http://192.168.1.x:5000/api"; // configurable

async function getItems(params?: { feedId?: number; read?: boolean; page?: number }) { ... }
async function markRead(id: number, isRead: boolean) { ... }
```

## 8. Key Design Decisions

- **Worker + API in the same process** — single `WebApplication`, no docker-compose needed
- **EF Core auto-migration** — `app.db` created/migrated on startup, zero manual steps
- **Items never deleted** — `Hash` unique index prevents re-insertion; new items accumulate indefinitely (paginated API)
- **Feed config lives in `appsettings.json`** — edit file and restart to add feeds; keeps things simple
- **CORS** — allow all origins (internal network, no auth)
- **No HTTPS** — plain HTTP on internal network (configurable but default)

## 9. Unit Tests Plan

| Test Class | Key Tests |
|---|---|
| `RssParserTests` | Parse valid RSS → correct fields; CDATA extraction; empty feed; malformed XML throws |
| `FeedFetcherTests` | Mock `HttpMessageHandler` — success returns XML, failure throws |
| `ItemRepositoryTests` | In-memory SQLite — insert, dedup by hash, query with filters, mark read |
| `RssFetchServiceTests` | Mock all dependencies — verify per-feed fetch→parse→store flow; error tolerance |
| `ItemsControllerTests` | Integration — `WebApplicationFactory` — verify endpoints return correct JSON, PATCH updates DB |

## 10. Run & Build

```bash
# Backend
cd src/RssOverEmail
dotnet run                              # starts on http://localhost:5000
dotnet test                             # run unit tests
dotnet publish -c Release -o ./publish  # single-folder publish

# Expo app
cd app
npx expo install                        # install deps
npx expo start                          # dev server, scan QR with Expo Go
```

## 11. Migration from Current App

- `db.json` contents can be imported into SQLite via a one-shot script or by keeping the old file path logic for a migration endpoint — optional, not required for MVP
- Feeds are re-fetched on first worker tick, so the DB will populate naturally
- Config structure changes from SMTP-based to DB-based — old `config.js` is replaced by `appsettings.json`
