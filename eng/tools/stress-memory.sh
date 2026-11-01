#!/bin/bash

# Puts macOS under memory pressure, for trying the Memory rules in the HEALTH view, using the
# memory_pressure tool that ships with macOS. See "Stress Testing" in README.md.
#
# The tool may need sudo. The memory is released when the time is up or on Ctrl+C.

set -e
set -o pipefail

seconds=120
level="warn"
simulate=false

usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  --level <level>   The pressure level to reach: warn or critical. Default: warn"
    echo "  --seconds <n>     How long to hold it. Default: 120"
    echo "  --simulate        Have the kernel report the level without using any memory."
    echo "  -h, --help        Display this help message."
    exit 1
}

while [[ "$#" -gt 0 ]]; do
    case "$1" in
        --level) level="$2"; shift ;;
        --seconds) seconds="$2"; shift ;;
        --simulate) simulate=true ;;
        -h|--help) usage ;;
        *) echo "Unknown option: $1"; usage ;;
    esac
    shift
done

if [[ "$level" != "warn" && "$level" != "critical" ]]; then
    echo "--level must be warn or critical."
    exit 1
fi

if [[ "$(uname)" != "Darwin" ]]; then
    echo "This uses the macOS memory_pressure tool. On Windows, use stress-memory.ps1."
    exit 1
fi

arguments=(-l "$level")

if [[ "$simulate" == true ]]; then
    arguments=(-S -l "$level")
fi

worker=""

stop_worker() {
    if [[ -n "$worker" ]]; then
        kill "$worker" 2>/dev/null || true
    fi

    echo "Stopped."
}

trap stop_worker EXIT
trap 'exit 130' INT TERM

memory_pressure "${arguments[@]}" &
worker=$!

echo "Applying $level pressure for $seconds seconds. Ctrl+C stops it early."

SECONDS=0

while [[ "$SECONDS" -lt "$seconds" ]]; do
    sleep 1

    if ! kill -0 "$worker" 2>/dev/null; then
        status=0
        wait "$worker" || status=$?
        worker=""

        if [[ "$status" -ne 0 ]]; then
            echo "memory_pressure exited with status $status. It may need sudo."
            exit "$status"
        fi

        echo "memory_pressure finished after $SECONDS seconds."
        break
    fi
done
