#!/usr/bin/env bash

# Port handshake file path
PORT_FILE="$TMPDIR/maxerz_port.txt"

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

get_port() {
  if [ -f "$PORT_FILE" ]; then
    cat "$PORT_FILE" | tr -d '\n'
  else
    echo ""
  fi
}

PORT=$(get_port)

kill_server() {
  local p=$(get_port)
  if [ -n "$p" ]; then
    local pid=$(lsof -t -i:"$p" 2>/dev/null)
    if [ -n "$pid" ]; then
      kill -9 "$pid" 2>/dev/null
    fi
  fi
  pkill -f MaxerZ.Api 2>/dev/null
}

restart_api() {
  echo "Killing any running API instances..."
  kill_server
  
  rm -f "$PORT_FILE"
  
  echo "Starting API server in background..."
  dotnet run --project src/MaxerZ.Api/ > /tmp/maxerz_test_api.log 2>&1 &
  local api_pid=$!
  
  # Wait for port file
  local count=0
  while [ ! -f "$PORT_FILE" ]; do
    sleep 0.5
    count=$((count+1))
    if [ $count -gt 40 ]; then
      echo -e "${RED}Error: API server failed to start within 20 seconds.${NC}"
      cat /tmp/maxerz_test_api.log
      exit 1
    fi
  done
  
  PORT=$(get_port)
  echo -e "${GREEN}API server successfully started on port $PORT (PID: $api_pid)${NC}"
}

# Ensure API is running
if [ -z "$PORT" ]; then
  restart_api
else
  # Double check if responding, if not restart
  if ! curl -s -f http://localhost:"$PORT"/api/settings >/dev/null 2>&1; then
    restart_api
  else
    echo "API is already running on port $PORT"
  fi
fi

FAILED=0

run_test() {
  echo -n "Running Test $1: $2 ... "
}

# --- Test 1: API starts ---
run_test "1" "API starts and serves settings"
SETTINGS_RESP=$(curl -s -f http://localhost:"$PORT"/api/settings)
if [ $? -eq 0 ] && echo "$SETTINGS_RESP" | grep -q "openRouterModelChain"; then
  echo -e "${GREEN}PASS${NC}"
else
  echo -e "${RED}FAIL${NC}"
  echo "Response: $SETTINGS_RESP"
  FAILED=$((FAILED+1))
fi

# --- Test 2: Settings save and persist ---
run_test "2" "Settings save and persist across restarts"
TEST_THEME="light"
TEST_NAME="Majid Test Persist"

# Save settings
SAVE_RESP=$(curl -s -X POST http://localhost:"$PORT"/api/settings \
  -H "Content-Type: application/json" \
  -d '{"openRouterApiKey":"sk-or-test-key-saved","groqApiKey":"gsk-test-key-saved","ollamaBaseUrl":"http://ollama-test-saved:11434","theme":"'"$TEST_THEME"'","exportDirectory":"~/Documents/MaxerZ-Saved","profile":{"fullName":"'"$TEST_NAME"'","email":"majid@example.com","phone":"123","linkedInUrl":"li","gitHubUrl":"gh"},"providerPriority":["openrouter","groq","ollama"]}')

# Restart
restart_api

# Retrieve settings
NEW_SETTINGS=$(curl -s -f http://localhost:"$PORT"/api/settings)
RETRIEVED_THEME=$(echo "$NEW_SETTINGS" | jq -r '.theme')
RETRIEVED_NAME=$(echo "$NEW_SETTINGS" | jq -r '.profile.fullName')

if [ "$RETRIEVED_THEME" = "$TEST_THEME" ] && [ "$RETRIEVED_NAME" = "$TEST_NAME" ]; then
  echo -e "${GREEN}PASS${NC}"
else
  echo -e "${RED}FAIL${NC}"
  echo "Expected theme '$TEST_THEME' and name '$TEST_NAME'"
  echo "Got theme '$RETRIEVED_THEME' and name '$RETRIEVED_NAME'"
  FAILED=$((FAILED+1))
fi

# --- Test 3: Active providers when NO keys configured ---
run_test "3" "Active providers when NO keys configured"
# Clear keys
curl -s -X POST http://localhost:"$PORT"/api/settings \
  -H "Content-Type: application/json" \
  -d '{"openRouterApiKey":"","groqApiKey":"","ollamaBaseUrl":""}' > /dev/null

ACTIVE_PROV=$(curl -s -f http://localhost:"$PORT"/api/settings/active-providers)
PROV_COUNT=$(echo "$ACTIVE_PROV" | jq '.providers | length')

if [ "$PROV_COUNT" -eq 0 ]; then
  echo -e "${GREEN}PASS${NC}"
else
  echo -e "${RED}FAIL${NC}"
  echo "Expected 0 active providers, got $PROV_COUNT. Response: $ACTIVE_PROV"
  FAILED=$((FAILED+1))
fi

# --- Test 4: Active providers when OpenRouter key added ---
run_test "4" "Active providers when OpenRouter key configured"
curl -s -X POST http://localhost:"$PORT"/api/settings \
  -H "Content-Type: application/json" \
  -d '{"openRouterApiKey":"sk-or-test-key","groqApiKey":"","ollamaBaseUrl":""}' > /dev/null

ACTIVE_PROV=$(curl -s -f http://localhost:"$PORT"/api/settings/active-providers)
PROV_ID=$(echo "$ACTIVE_PROV" | jq -r '.providers[0].id')
PROV_LABEL=$(echo "$ACTIVE_PROV" | jq -r '.providers[0].label')

if [ "$PROV_ID" = "openrouter" ] && [ "$PROV_LABEL" = "OpenRouter" ]; then
  echo -e "${GREEN}PASS${NC}"
else
  echo -e "${RED}FAIL${NC}"
  echo "Expected openrouter provider active. Response: $ACTIVE_PROV"
  FAILED=$((FAILED+1))
fi

# --- Test 5: Preview without any LLM key → raw fallback used ---
run_test "5" "Preview with no LLM keys (triggers raw fallback)"
# Reset keys first
curl -s -X POST http://localhost:"$PORT"/api/settings \
  -H "Content-Type: application/json" \
  -d '{"openRouterApiKey":"","groqApiKey":"","ollamaBaseUrl":""}' > /dev/null

PREVIEW_RESP=$(curl -s -X POST http://localhost:"$PORT"/api/coverletter/preview \
  -H "Content-Type: application/json" \
  -d '{
    "companyName":"Siemens AG",
    "position":"Software Engineer",
    "companyLocation":"1010 Vienna",
    "language":"en",
    "coverLetterBody":"Dear Hiring Manager,\n\nI am applying for this role.\n\nBest regards,\nMax Mustermann"
  }')

WAS_FALLBACK=$(echo "$PREVIEW_RESP" | jq -r '.wasFallback')
PDF_BASE64=$(echo "$PREVIEW_RESP" | jq -r '.pdfBase64')
CMP_NAME=$(echo "$PREVIEW_RESP" | jq -r '.layout.companyNameFormatted')

if [ "$WAS_FALLBACK" = "true" ] && [ -n "$PDF_BASE64" ] && [ "$CMP_NAME" = "Siemens AG" ]; then
  echo -e "${GREEN}PASS${NC}"
else
  echo -e "${RED}FAIL${NC}"
  echo "wasFallback: $WAS_FALLBACK, companyName: $CMP_NAME"
  FAILED=$((FAILED+1))
fi

# --- Test 6: PDF bytes are valid ---
run_test "6" "PDF base64 decodes to valid %PDF header"
PDF_HEADER=$(echo "$PDF_BASE64" | base64 -d | head -c 4)
if [ "$PDF_HEADER" = "%PDF" ]; then
  echo -e "${GREEN}PASS${NC}"
else
  echo -e "${RED}FAIL${NC}"
  echo "Expected header %PDF, got: '$PDF_HEADER'"
  FAILED=$((FAILED+1))
fi

# --- Test 7: Export creates file on disk ---
run_test "7" "Export creates file on disk and returns valid path"
EXPORT_RESP=$(curl -s -X POST http://localhost:"$PORT"/api/coverletter/export \
  -H "Content-Type: application/json" \
  -d '{
    "companyName":"Siemens AG",
    "position":"Software Engineer",
    "companyLocation":"1010 Vienna",
    "language":"en",
    "coverLetterBody":"Dear Hiring Manager,\n\nI am applying for this role.\n\nBest regards,\nMax Mustermann"
  }')

PDF_PATH=$(echo "$EXPORT_RESP" | jq -r '.pdfPath')
SYNCED_MCP=$(echo "$EXPORT_RESP" | jq -r '.syncedToMcp')

# Check if file exists
if [ -n "$PDF_PATH" ] && [ -f "$PDF_PATH" ]; then
  echo -e "${GREEN}PASS${NC}"
else
  echo -e "${RED}FAIL${NC}"
  echo "Expected file to exist at: '$PDF_PATH'"
  FAILED=$((FAILED+1))
fi

# --- Test 8: History returns exported record ---
run_test "8" "History returns the exported record"
HISTORY_RESP=$(curl -s -f http://localhost:"$PORT"/api/coverletter/history)
HIST_COMPANY=$(echo "$HISTORY_RESP" | jq -r '.[0].companyName')

if [ "$HIST_COMPANY" = "Siemens AG" ]; then
  echo -e "${GREEN}PASS${NC}"
else
  echo -e "${RED}FAIL${NC}"
  echo "Expected Siemens AG in history, got: '$HIST_COMPANY'"
  FAILED=$((FAILED+1))
fi

# --- Test 9: MCP disabled by default → export succeeds anyway ---
run_test "9" "MCP disabled by default (syncedToMcp=false)"
if [ "$SYNCED_MCP" = "false" ]; then
  echo -e "${GREEN}PASS${NC}"
else
  echo -e "${RED}FAIL${NC}"
  echo "Expected syncedToMcp to be false, got: '$SYNCED_MCP'"
  FAILED=$((FAILED+1))
fi

# --- Test 10: German language → correct closing in PDF ---
run_test "10" "German language parses closing line correctly"
DE_PREVIEW_RESP=$(curl -s -X POST http://localhost:"$PORT"/api/coverletter/preview \
  -H "Content-Type: application/json" \
  -d '{
    "companyName":"Fronius GmbH",
    "position":"Softwareentwickler",
    "companyLocation":"4600 Wels",
    "language":"de",
    "coverLetterBody":"Sehr geehrte Damen und Herren,\n\nIch bewerbe mich für diese Stelle.\n\nMit freundlichen Grüßen,\nMax Mustermann"
  }')

DE_CLOSING=$(echo "$DE_PREVIEW_RESP" | jq -r '.layout.closingLine')

if echo "$DE_CLOSING" | grep -q "freundlichen"; then
  echo -e "${GREEN}PASS${NC}"
else
  echo -e "${RED}FAIL${NC}"
  echo "Expected closing line containing 'freundlichen', got: '$DE_CLOSING'"
  FAILED=$((FAILED+1))
fi

# --- Test 11: Provider test endpoint with invalid key ---
run_test "11" "Provider test endpoint reports failure for invalid key"
TEST_PROV_RESP=$(curl -s -X POST http://localhost:"$PORT"/api/settings/test-provider/openrouter \
  -H "Content-Type: application/json" \
  -d '{"openRouterApiKey":"invalid-key-123","openRouterModelChain":["mistralai/mistral-7b-instruct:free"]}')

SUCCESS_VAL=$(echo "$TEST_PROV_RESP" | jq -r '.success')

if [ "$SUCCESS_VAL" = "false" ]; then
  echo -e "${GREEN}PASS${NC}"
else
  echo -e "${RED}FAIL${NC}"
  echo "Expected success=false for invalid key, got: '$TEST_PROV_RESP'"
  FAILED=$((FAILED+1))
fi

# --- Test 12: No provider freezes or times out ---
run_test "12" "Performance check (preview completes under 3 seconds)"
START_TIME=$(date +%s)
curl -s -X POST http://localhost:"$PORT"/api/coverletter/preview \
  -H "Content-Type: application/json" \
  -d '{
    "companyName":"Siemens AG",
    "position":"Software Engineer",
    "companyLocation":"1010 Vienna",
    "language":"en",
    "coverLetterBody":"Dear Hiring Manager,\n\nI am applying for this role.\n\nBest regards,\nMax Mustermann"
  }' > /dev/null
END_TIME=$(date +%s)
DURATION=$((END_TIME - START_TIME))

if [ "$DURATION" -lt 3 ]; then
  echo -e "${GREEN}PASS ($DURATION sec)${NC}"
else
  echo -e "${RED}FAIL ($DURATION sec)${NC}"
  FAILED=$((FAILED+1))
fi

# --- Cleanup ---
echo "Cleaning up..."
kill_server
rm -f "$PORT_FILE"

if [ "$FAILED" -eq 0 ]; then
  echo -e "\n${GREEN}ALL 12 TESTS PASSED! Ready for Phase 2.${NC}"
  exit 0
else
  echo -e "\n${RED}$FAILED TESTS FAILED.${NC}"
  exit 1
fi
