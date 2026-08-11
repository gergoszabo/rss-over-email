# Rss over Email

Always-on .NET 10 feed scanner with a read-tracking API. Scans configured RSS/Atom feeds every 15 minutes (or your configured interval), stores new items in SQLite, and serves them via a tiny HTTP API.

## API

- `GET /list/unread` — flat JSON array of unread items, newest first:
  `[{"id":"...sha256(link)...","title":"...","link":"...","feed":"...","publishedAt":"..."}]`
- `POST /:id/read` — marks the item with the given id as read. Returns `204` on success, `404` if the id is unknown.

There is no authentication and the API binds to all interfaces (`0.0.0.0`). Put a reverse proxy in front if it should not be reachable on the network.

## Configuration

Copy `appsettings.sample.json` to `appsettings.json` and fill it in:

```json
{
  "Feeds": [
    { "Name": "Example", "Url": "https://example.com/rss" }
  ],
  "PollIntervalSeconds": 900,
  "Port": 8080,
  "Database": { "Path": "rss.db" }
}
```

- `Feeds` — the RSS/Atom feeds to scan. `Name` is used for attribution.
- `PollIntervalSeconds` — scan interval, defaults to `900` (15 minutes).
- `Port` — HTTP listen port, defaults to `8080`.
- `Database:Path` — SQLite database file, defaults to `rss.db`.

On the first scan every currently-visible item is stored as unread; afterwards only new items are inserted.

## Build

```sh
dotnet build -c Release
```

## Run as a systemd service

Publish a single-file binary and install the service:

```sh
dotnet publish -c Release
sudo install -d /opt/rss-over-email
sudo install -m 755 bin/Release/net10.0/publish/rss-over-email /opt/rss-over-email/
sudo install -m 640 appsettings.json /opt/rss-over-email/appsettings.json
sudo install -m 640 rss-over-email.service /etc/systemd/system/rss-over-email.service
sudo systemctl daemon-reload
sudo systemctl enable --now rss-over-email
sudo systemctl status rss-over-email
```

The service unit sets `WorkingDirectory=/opt/rss-over-email`, so the SQLite file lives there.
