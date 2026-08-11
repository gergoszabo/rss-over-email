# PLAN: `rss-over-email` rewrite in .NET 10

## Decisions (all confirmed)

- **Email/SMTP**: removed entirely. Old JS files (`index.js`, `package.json`, `package-lock.json`, `config.sample.js`) deleted; `db.json` schema replaced.
- **Form**: single `Program.cs` (top-level statements, nested `BackgroundService` class at end of file), `Microsoft.NET.Sdk.Web`. Optionally single-file publish for deployment.
- **Parse**: `System.ServiceModel.Syndication` (built-in, no NuGet dep beyond ASP.NET + Sqlite).
- **DB**: `Microsoft.Data.Sqlite`, raw SQL, one connection per operation, WAL + busy timeout.
- **Identity/dedup**: `id = sha256hex(link)`, `INSERT OR IGNORE`.
- **First run**: full history inserted as unread.
- **Scheduling**: immediate scan at startup, then every 15 min (`PeriodicTimer`, no overlap), interval from config.
- **API**: only `GET /list/unread` (flat, newest-first) and `POST /:id/read` (204/404). All interfaces, no auth.
- **Config**: `appsettings.json` (gitignored) + committed `appsettings.sample.json` (mirrors old `config.sample.js` pattern).
- **Retention**: keep forever.
- **Storage**: metadata only (no content).
- **Deploy**: `rss-over-email.service` committed; long-running, self-scheduled; no timer.

## File layout

```
rss-over-email.sln/csproj     (net10.0, Microsoft.NET.Sdk.Web)
Program.cs                     (single file: config, DB init, server, scan loop)
appsettings.sample.json        (Feeds[], PollIntervalSeconds=900, Database:Path=rss.db, Port=8080)
rss-over-email.service         (Type=simple, Restart=on-failure)
README.md                      (rewritten: build, publish, systemd install)
.gitignore                     (+ appsettings.json, rss.db)
```

## Schema

```sql
CREATE TABLE IF NOT EXISTS items (
  id TEXT PRIMARY KEY,        -- sha256 hex of link
  title TEXT NOT NULL,
  link TEXT NOT NULL,
  feed TEXT NOT NULL,
  published_at TEXT,          -- ISO-8601 UTC, NULL if missing
  is_read INTEGER NOT NULL DEFAULT 0,
  inserted_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_items_unread ON items (is_read, published_at);
```

## Runtime behavior

1. On startup: load `appsettings.json`, create schema, start Kestrel on `0.0.0.0:8080`, start scan loop.
2. Scan loop (each tick): for each feed — fetch with shared `HttpClient` (30s timeout, UA header), parse via `SyndicationFeed.Load`, `INSERT OR IGNORE` each item. Per-feed try/catch, log errors, continue. Log per-scan summary.
3. `GET /list/unread`: `SELECT id,title,link,feed,published_at FROM items WHERE is_read=0 ORDER BY (published_at IS NULL), published_at DESC, inserted_at DESC` -> JSON array `[{id,title,link,feed,publishedAt}]`.
4. `POST /:id/read`: `UPDATE ... SET is_read=1 WHERE id=@id`; 204 if affected > 0, else 404.

## Verification

- `dotnet build` clean.
- Manual smoke: start, confirm first scan inserts items, `curl /list/unread`, `curl -X POST /:id/read`, confirm 204/404 and removal from unread list.
- No unit tests unless you want them (single-file app, personal tool).
