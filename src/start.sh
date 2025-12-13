#!/bin/bash

# Detect the operating system architecture
ARCH=$(uname -m)

# Determine the executable name based on architecture
if [ "$ARCH" = "arm64" ] || [ "$ARCH" = "aarch64" ]; then
  EXECUTABLE="./rss-over-email-arm64"
elif [ "$ARCH" = "x86_64" ]; then
  EXECUTABLE="./rss-over-email-x64"
else
  echo "Unsupported architecture: $ARCH"
  exit 1
fi

# Log file path
LOG_FILE="app.log"

# Database file path
DB_FILE="serverdb.json"

export DB_FILE_PATH="./$DB_FILE" # Export as an environment variable

exec "$EXECUTABLE" 2>&1 | tee "$LOG_FILE" | bunx pino-pretty
