#!/bin/bash

# Detect the operating system architecture
ARCH=$(uname -m)

# Determine the executable name based on architecture
# Since only aarch64 build is needed, we directly use the arm64 executable
EXECUTABLE="./rss-over-email-arm64"

# Log file path
LOG_FILE="app.log"

# Database file path
DB_FILE="serverdb.json"

export DB_FILE_PATH="./$DB_FILE" # Export as an environment variable

exec "$EXECUTABLE" 2>&1 | tee "$LOG_FILE" | bunx pino-pretty
