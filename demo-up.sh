#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PID_DIR="$REPO_ROOT/.demo-pids"
LOG_DIR="$REPO_ROOT/.demo-logs"
case "$(uname -s)" in
    Darwin*)          CLAUDE_CONFIG="$HOME/Library/Application Support/Claude/claude_desktop_config.json" ;;
    MINGW*|MSYS*|CYGWIN*) CLAUDE_CONFIG="$APPDATA/Claude/claude_desktop_config.json" ;;
    *) echo "Unsupported OS: $(uname -s)"; exit 1 ;;
esac
GATEWAY_URL="http://localhost:5128"

for cmd in curl jq docker dotnet; do
    if ! command -v "$cmd" &>/dev/null; then
        echo "Required tool not found: $cmd"
        case "$cmd" in
            jq)     echo "  Mac: brew install jq  |  Windows: winget install jqlang.jq" ;;
            docker) echo "  Install Docker Desktop: https://www.docker.com/products/docker-desktop" ;;
        esac
        exit 1
    fi
done

mkdir -p "$PID_DIR" "$LOG_DIR"

if [ -f "$PID_DIR/gateway.pid" ]; then
    echo "A demo session is already running. Run ./demo-down.sh first."
    exit 1
fi

if [ ! -f "$REPO_ROOT/src/Ithil.Gateway/models/all-MiniLM-L6-v2.onnx" ]; then
    echo "-> Downloading ONNX model files (one-time, ~22 MB)..."
    bash "$REPO_ROOT/scripts/download-models.sh"
fi

echo "-> Starting Redis..."
docker compose -f "$REPO_ROOT/docker/docker-compose.yml" up -d

echo "-> Building SampleApi..."
dotnet build "$REPO_ROOT/samples/SampleApi/SampleApi.csproj" >> "$LOG_DIR/sampleapi.log" 2>&1

echo "-> Building Gateway..."
dotnet build "$REPO_ROOT/src/Ithil.Gateway/Ithil.Gateway.csproj" >> "$LOG_DIR/gateway.log" 2>&1

echo "-> Starting SampleApi..."
dotnet run --no-build --project "$REPO_ROOT/samples/SampleApi/SampleApi.csproj" \
    >> "$LOG_DIR/sampleapi.log" 2>&1 &
echo $! > "$PID_DIR/sampleapi.pid"

echo "-> Starting Gateway..."
dotnet run --no-build --project "$REPO_ROOT/src/Ithil.Gateway/Ithil.Gateway.csproj" \
    >> "$LOG_DIR/gateway.log" 2>&1 &
echo $! > "$PID_DIR/gateway.pid"

echo "-> Waiting for Gateway (this includes build time)..."
ATTEMPTS=0
until curl -sf "$GATEWAY_URL/health/live" > /dev/null 2>&1; do
    ATTEMPTS=$((ATTEMPTS + 1))
    if [ "$ATTEMPTS" -ge 45 ]; then
        echo "   Gateway did not start after 90s. Check .demo-logs/gateway.log"
        exit 1
    fi
    sleep 2
done
echo "   Gateway is up."

echo "-> Fetching dev JWT..."
TOKEN=$(curl -sf "$GATEWAY_URL/dev/token" | jq -r '.token')

echo "-> Clearing mcp-remote session cache..."
rm -rf ~/.mcp-auth/*

echo "-> Patching Claude Desktop config..."
jq --arg token "$TOKEN" '
    .mcpServers.ithil.args = [
        "mcp-remote",
        "http://localhost:5128/mcp",
        "--transport", "http-first",
        "--header", ("Authorization: Bearer " + $token)
    ]
' "$CLAUDE_CONFIG" > "$CLAUDE_CONFIG.tmp" && mv "$CLAUDE_CONFIG.tmp" "$CLAUDE_CONFIG"

echo ""
echo "Demo is up."
echo "  Gateway:   $GATEWAY_URL"
echo "  SampleApi: http://localhost:5200"
echo "  Logs:      .demo-logs/"
echo ""
echo "Restart Claude Desktop to pick up the new token (expires in 8h)."
echo "Run ./demo-down.sh to stop everything."
