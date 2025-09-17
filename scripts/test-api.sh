#!/bin/bash

# AI Agent Orchestra - API Testing Script

API_URL="http://localhost:5002"
SUCCESS=0
FAILED=0

echo "=== AI Agent Orchestra API Tests ==="
echo "Testing API at: $API_URL"

test_endpoint() {
    local method=$1
    local url=$2
    local body=$3
    local description=$4

    echo ""
    echo "🔹 Testing: $description"
    echo "   $method $url"

    if [ -n "$body" ]; then
        response=$(curl -s -w "%{http_code}" -X "$method" "$url" -H "Content-Type: application/json" -d "$body")
    else
        response=$(curl -s -w "%{http_code}" -X "$method" "$url")
    fi

    http_code="${response: -3}"
    body="${response%???}"

    if [ "$http_code" -ge 200 ] && [ "$http_code" -lt 300 ]; then
        echo "   ✅ SUCCESS (HTTP $http_code)"
        if [ -n "$body" ] && [ "$body" != "null" ]; then
            echo "   Response: ${body:0:200}..."
        fi
        ((SUCCESS++))
        return 0
    else
        echo "   ❌ FAILED (HTTP $http_code)"
        if [ -n "$body" ]; then
            echo "   Error: $body"
        fi
        ((FAILED++))
        return 1
    fi
}

# Test 1: Get initial state
test_endpoint "GET" "$API_URL/state" "" "Get orchestrator state"

# Test 2: Get agents
test_endpoint "GET" "$API_URL/agents" "" "Get all agents"

# Test 3: Register new agent
agent_body='{
    "Id": "test-agent-bash",
    "Name": "Bash Test Agent",
    "Type": "claude-code",
    "RepositoryPath": "/tmp/test-repo"
}'
test_endpoint "POST" "$API_URL/agents/register" "$agent_body" "Register new agent"

# Test 4: Update agent status (using numeric enum values)
ping_body='{
    "Status": 1,
    "CurrentTask": "Running bash tests"
}'
test_endpoint "POST" "$API_URL/agents/test-agent-bash/ping" "$ping_body" "Update agent status"

# Test 5: Queue a task (using numeric enum values)
task_body='{
    "Command": "Run bash test suite",
    "RepositoryPath": "/tmp/test-repo",
    "Priority": 2
}'
test_endpoint "POST" "$API_URL/tasks/queue" "$task_body" "Queue new task"

# Test 6: Get next task for agent
test_endpoint "GET" "$API_URL/agents/test-agent-bash/next-task" "" "Get next task for agent"

# Test 7: Get final state
test_endpoint "GET" "$API_URL/state" "" "Get final orchestrator state"

echo ""
echo "=== Test Results ==="
echo "✅ Passed: $SUCCESS"
echo "❌ Failed: $FAILED"
echo "🔍 Total:  $((SUCCESS + FAILED))"

if [ $FAILED -eq 0 ]; then
    echo ""
    echo "🎉 All tests passed! API is working correctly."
    exit 0
else
    echo ""
    echo "⚠️  Some tests failed. Check API connectivity and logs."
    exit 1
fi