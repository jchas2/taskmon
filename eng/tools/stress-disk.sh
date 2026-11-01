#!/bin/bash

# Keeps a disk busy with writes, for trying the Disk rules in the HEALTH view. See "Stress Testing"
# in README.md.
#
# Writes only: shell tools can't bypass the file cache, so reads would mostly never reach the disk.
# The test files are removed afterwards, or on Ctrl+C.

set -e
set -o pipefail

seconds=120
target="${TMPDIR:-/tmp}"
file_mb=256
threads=8

usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  --seconds <n>   How long to run. Default: 120"
    echo "  --path <dir>    A folder on the disk to test. Default: the temp folder."
    echo "  --file-mb <n>   Size of each thread's test file. Default: 256"
    echo "  --threads <n>   Number of writers. Default: 8"
    echo "  -h, --help      Display this help message."
    exit 1
}

while [[ "$#" -gt 0 ]]; do
    case "$1" in
        --seconds) seconds="$2"; shift ;;
        --path) target="$2"; shift ;;
        --file-mb) file_mb="$2"; shift ;;
        --threads) threads="$2"; shift ;;
        -h|--help) usage ;;
        *) echo "Unknown option: $1"; usage ;;
    esac
    shift
done

needed_kb=$(((threads + 1) * file_mb * 1024 * 2))
free_kb=$(df -k "$target" | awk 'NR == 2 { print $4 }')

if [[ "$free_kb" -lt "$needed_kb" ]]; then
    echo "Not enough free space on $target for $threads files of $file_mb MB."
    exit 1
fi

folder=$(mktemp -d "$target/taskmon-stress.XXXXXX")
pids=()

stop_writers() {
    if [[ "${#pids[@]}" -gt 0 ]]; then
        kill -KILL "${pids[@]}" 2>/dev/null || true
        wait "${pids[@]}" 2>/dev/null || true
    fi

    rm -rf "$folder"
    echo "Removed the test files."
}

trap stop_writers EXIT
trap 'exit 130' INT TERM

echo "Writing a $file_mb MB seed file to $folder"
dd if=/dev/urandom of="$folder/seed" bs=1048576 count="$file_mb" 2>/dev/null

write_forever() {
    while true; do
        dd if="$folder/seed" of="$folder/writer.$1" bs=1048576 2>/dev/null
        sync
    done
}

for ((i = 0; i < threads; i++)); do
    write_forever "$i" &
    pids+=($!)
done

echo "Running $threads writers for $seconds seconds. Ctrl+C stops it early."

SECONDS=0

while [[ "$SECONDS" -lt "$seconds" ]]; do
    sleep 1
done
