#!/usr/bin/env bash

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PID_DIR="$REPO_ROOT/.demo-pids"

stop_process() {
    local name=$1
    local pidfile="$PID_DIR/$name.pid"

    if [ ! -f "$pidfile" ]; then
        return
    fi

    local pid
    pid=$(cat "$pidfile")

    echo "-> Stopping $name (PID $pid)..."

    # Kill children first (dotnet run forks the actual app process)
    case "$(uname -s)" in
        Darwin*|Linux*)
            pkill -P "$pid" 2>/dev/null || true
            kill "$pid" 2>/dev/null || true
            ;;
        MINGW*|MSYS*|CYGWIN*)
            taskkill //F //T //PID "$pid" 2>/dev/null || true
            ;;
    esac

    rm -f "$pidfile"
}

stop_process "gateway"
stop_process "sampleapi"

echo "-> Stopping Redis..."
docker compose -f "$REPO_ROOT/docker/docker-compose.yml" stop

rm -rf "$PID_DIR"

echo "Done. Redis data is preserved in the docker volume."
