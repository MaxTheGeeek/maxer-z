#!/usr/bin/env bash

# ==============================================================================
# MaxerZ Local Application Launcher
# ==============================================================================
# Usage:
#   ./run_app.sh            - Build Angular frontend & start embedded API server
#   ./run_app.sh --dev      - Start Angular dev server (4200) + API server concurrently
#   ./run_app.sh --no-build - Start API server using existing wwwroot build
# ==============================================================================

set -e

# Terminal colors
BOLD='\033[1m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Determine project directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PORT_FILE="$TMPDIR/maxerz_port.txt"
WEB_DIR="$SCRIPT_DIR/src/MaxerZ.Web"
API_DIR="$SCRIPT_DIR/src/MaxerZ.Api"

DEV_MODE=false
SKIP_BUILD=false

# Parse flags
for arg in "$@"; do
  case $arg in
    --dev)
      DEV_MODE=true
      shift
      ;;
    --no-build)
      SKIP_BUILD=true
      shift
      ;;
    --help|-h)
      echo "MaxerZ App Launcher"
      echo "Usage:"
      echo "  ./run_app.sh            Default: Build UI & run combined API/Web server"
      echo "  ./run_app.sh --dev      Run Angular hot-reload dev server + API server"
      echo "  ./run_app.sh --no-build Skip Angular build step and launch API server"
      exit 0
      ;;
  esac
done

cleanup() {
  echo -e "\n${YELLOW}Shutting down MaxerZ processes...${NC}"
  if [ -n "$API_PID" ]; then
    kill -9 "$API_PID" 2>/dev/null || true
  fi
  if [ -n "$WEB_PID" ]; then
    kill -9 "$WEB_PID" 2>/dev/null || true
  fi
  if [ -f "$PORT_FILE" ]; then
    OLD_PORT=$(cat "$PORT_FILE" 2>/dev/null || true)
    if [ -n "$OLD_PORT" ]; then
      lsof -t -i:"$OLD_PORT" 2>/dev/null | xargs kill -9 2>/dev/null || true
    fi
  fi
  pkill -f "MaxerZ.Api" 2>/dev/null || true
  rm -f "$PORT_FILE"
  echo -e "${GREEN}Cleanup complete.${NC}"
}

trap cleanup EXIT INT TERM

echo -e "${BOLD}${CYAN}====================================================${NC}"
echo -e "${BOLD}${CYAN}           MAXERZ LOCAL APP LAUNCHER               ${NC}"
echo -e "${BOLD}${CYAN}====================================================${NC}"

# Kill any existing server instances
echo -e "${YELLOW}Stopping any running MaxerZ instances...${NC}"
cleanup 2>/dev/null || true
trap cleanup EXIT INT TERM

# 1. Build Angular Frontend (if not skipped and not dev mode)
if [ "$DEV_MODE" = false ] && [ "$SKIP_BUILD" = false ]; then
  echo -e "\n${BOLD}${CYAN}[1/2] Building Angular Frontend (MaxerZ.Web)...${NC}"
  npm --prefix "$WEB_DIR" run build
  echo -e "${GREEN}Angular build completed successfully!${NC}"
elif [ "$DEV_MODE" = true ]; then
  echo -e "\n${BOLD}${CYAN}[1/2] Starting Angular Dev Server (ng serve)...${NC}"
  npm --prefix "$WEB_DIR" start > /tmp/maxerz_web_dev.log 2>&1 &
  WEB_PID=$!
  echo -e "${GREEN}Angular dev server started (PID: $WEB_PID). Logs at /tmp/maxerz_web_dev.log${NC}"
else
  echo -e "\n${BOLD}${CYAN}[1/2] Skipping Angular build (--no-build flag set)...${NC}"
fi

# 2. Launch ASP.NET Core API Server
echo -e "\n${BOLD}${CYAN}[2/2] Starting ASP.NET Core API Server (MaxerZ.Api)...${NC}"
rm -f "$PORT_FILE"

dotnet run --project "$API_DIR/MaxerZ.Api.csproj" > /tmp/maxerz_api.log 2>&1 &
API_PID=$!

echo -n "Waiting for API server port handshake..."
COUNT=0
while [ ! -f "$PORT_FILE" ]; do
  sleep 0.5
  echo -n "."
  COUNT=$((COUNT+1))
  if [ $COUNT -gt 40 ]; then
    echo -e "\n${RED}Error: API server failed to start within 20 seconds.${NC}"
    echo -e "${YELLOW}API Log Output:${NC}"
    cat /tmp/maxerz_api.log
    exit 1
  fi
done

PORT=$(cat "$PORT_FILE" | tr -d '\n')
echo -e "\n${GREEN}API Server successfully started on port $PORT (PID: $API_PID)!${NC}"

# 3. Print Ready Status & Access URLs
echo -e "\n${BOLD}${GREEN}====================================================${NC}"
echo -e "${BOLD}${GREEN}         MAXERZ IS NOW RUNNING LOCALLY!            ${NC}"
echo -e "${BOLD}${GREEN}====================================================${NC}"

if [ "$DEV_MODE" = true ]; then
  echo -e "   ${BOLD}Angular Dev UI:${NC}    ${CYAN}http://localhost:4200/${NC}"
  echo -e "   ${BOLD}Backend API:${NC}       ${CYAN}http://localhost:$PORT/${NC}"
else
  echo -e "   ${BOLD}Application URL:${NC}   ${CYAN}http://localhost:$PORT/${NC}"
  echo -e "   ${BOLD}API Settings:${NC}      ${CYAN}http://localhost:$PORT/api/settings${NC}"
fi

echo -e "\n   ${YELLOW}Press Ctrl+C at any time to stop all servers.${NC}"
echo -e "${BOLD}${GREEN}====================================================${NC}\n"

# Open URL in default web browser if open command is available
if command -v open >/dev/null 2>&1; then
  if [ "$DEV_MODE" = true ]; then
    open "http://localhost:4200/" 2>/dev/null || true
  else
    open "http://localhost:$PORT/" 2>/dev/null || true
  fi
fi

# Stream logs in real-time
tail -f /tmp/maxerz_api.log
