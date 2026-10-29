#!/usr/bin/env bash
# Run from the deployed server directory. Preserve the server's arguments and stdin.
set -u
if (( $# == 0 )); then
    printf 'Usage: bash start-server-diagnostics.sh ./"Intersect Server" [arguments...]\n' >&2
    exit 64
fi
mkdir -p logs || exit 73
session="$(date -u +%Y%m%dT%H%M%SZ)-$$"
host_log="logs/host-exit-${session}.log"
record() {
    printf '%s %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*" >> "$host_log"
}
memory_evidence() {
    local metric
    for metric in /sys/fs/cgroup/memory.events /sys/fs/cgroup/memory.current /sys/fs/cgroup/memory.max; do
        if [[ -r "$metric" ]]; then
            printf '%s\n' "$metric" >> "$host_log"
            cat "$metric" >> "$host_log"
        fi
    done
}
record 'Supervisor started; command arguments deliberately omitted.'
memory_evidence
# A mini dump is optional because it can be large and contain private data.
# To enable .NET dumps: set DOTNET_DbgEnableMiniDump=1 before launching this script.
export DOTNET_DbgMiniDumpName="${DOTNET_DbgMiniDumpName:-$PWD/logs/server-dump-%p-%t.dmp}"
"$@" <&0 &
server_pid=$!
record "Server pid=$server_pid"
forward_signal() {
    record "Supervisor received $1; forwarding to server."
    kill -s "$1" "$server_pid" 2>/dev/null || true
}
trap 'forward_signal TERM' TERM
trap 'forward_signal INT' INT
# A trapped signal can interrupt wait while the server is still shutting down.
while true; do
    wait "$server_pid"
    code=$?
    if ! kill -0 "$server_pid" 2>/dev/null; then break; fi
done
record "Server exited; shellExitCode=$code"
if (( code > 128 && code < 193 )); then
    record "Possible signal=$((code - 128)); code alone does not establish the cause or prove OOM."
fi
memory_evidence
printf '[crash-diag] Server exit=%s; evidence=%s\n' "$code" "$host_log" >&2
exit "$code"
