#!/bin/bash

# Crashes a throwaway process, so a real report appears under Recent events in the HEALTH view. See
# "Stress Testing" in README.md.
#
# Nothing else is affected, but macOS keeps the crash report, so it stays listed in HEALTH for the
# lookback period. HEALTH reads the reports once a minute.

if [[ "$(uname)" != "Darwin" ]]; then
    echo "This relies on macOS writing a crash report. On Windows, use stress-crash.ps1."
    exit 1
fi

/bin/bash -c 'kill -SEGV $$' || true

echo "Crashed a throwaway process. The report can take a few seconds to appear."
