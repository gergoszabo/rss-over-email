#!/bin/bash

# Set the path to the log file (should match what your main app uses)
LOG_FILE="app.log"

# Set environment variables for log_rotator.js
export LOG_FILE_PATH="./$LOG_FILE"

# Execute the log rotator script using bun
bun run ./log_rotator.js
