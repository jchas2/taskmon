#!/bin/bash

# Keeps the CPU busy, for trying the CPU rules in the HEALTH view. See "Stress Testing" in README.md.
#
# Threads run at a lower priority, so the machine and taskmon keep working. Ctrl+C stops it early.

set -e
set -o pipefail

seconds=120
percent=100
threads=0

usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  --seconds <n>   How long to run. Default: 120"
    echo "  --percent <n>   How busy to keep the CPU. 93 gives a Warning without reaching Critical. Default: 100"
    echo "  --threads <n>   Default: 3 per core at 100%, which also raises the load average, otherwise 1 per core."
    echo "  -h, --help      Display this help message."
    exit 1
}

while [[ "$#" -gt 0 ]]; do
    case "$1" in
        --seconds) seconds="$2"; shift ;;
        --percent) percent="$2"; shift ;;
        --threads) threads="$2"; shift ;;
        -h|--help) usage ;;
        *) echo "Unknown option: $1"; usage ;;
    esac
    shift
done

if [[ "$percent" -lt 1 || "$percent" -gt 100 ]]; then
    echo "--percent must be from 1 to 100."
    exit 1
fi

cores=$(sysctl -n hw.logicalcpu 2>/dev/null || getconf _NPROCESSORS_ONLN)

if [[ "$threads" -eq 0 ]]; then
    if [[ "$percent" -eq 100 ]]; then
        threads=$((cores * 3))
    else
        threads=$cores
    fi
fi

pids=()

stop_workers() {
    if [[ "${#pids[@]}" -gt 0 ]]; then
        kill -KILL "${pids[@]}" 2>/dev/null || true
    fi

    echo "Stopped after $SECONDS seconds."
}

trap stop_workers EXIT
trap 'exit 130' INT TERM

for ((i = 0; i < threads; i++)); do
    nice -n 10 yes > /dev/null &
    pids+=($!)
done

echo "Running $threads threads at $percent% for $seconds seconds. Ctrl+C stops it early."

SECONDS=0

# Below 100%, the workers are paused for part of every second.
busy=$(printf '0.%03d' $((percent * 10)))
idle=$(printf '0.%03d' $((1000 - percent * 10)))

while [[ "$SECONDS" -lt "$seconds" ]]; do
    if [[ "$percent" -eq 100 ]]; then
        sleep 1
    else
        sleep "$busy"
        kill -STOP "${pids[@]}" 2>/dev/null || true
        sleep "$idle"
        kill -CONT "${pids[@]}" 2>/dev/null || true
    fi
done
