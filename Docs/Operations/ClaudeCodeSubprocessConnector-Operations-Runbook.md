# ClaudeCodeSubprocessConnector Operations Runbook

**Version**: 1.0
**Last Updated**: 2025-11-24
**Status**: Production Ready
**Author**: AI Agent Orchestra Team

---

## 1. System Architecture Overview

### Component Overview

ClaudeCodeSubprocessConnector is the production-ready component for orchestrating Claude Code AI agents with human-in-the-loop approval escalation. The system enables multi-turn dialogs with Claude Code CLI, automatic permission escalation to Telegram, and comprehensive monitoring.

```
                    AI Agent Orchestra System
    +---------------------------------------------------------+
    |                                                         |
    |  +-------------+     +------------------+               |
    |  | API Layer   |---->| MediatR CQRS     |               |
    |  | (ASP.NET)   |     | (Commands/Query) |               |
    |  +-------------+     +------------------+               |
    |        |                     |                          |
    |        v                     v                          |
    |  +-------------+     +------------------+               |
    |  | SignalR Hub |     | Command Handlers |               |
    |  +-------------+     +------------------+               |
    |                              |                          |
    |        +---------------------+---------------------+    |
    |        |                     |                     |    |
    |        v                     v                     v    |
    |  +-----------+     +---------------+     +----------+   |
    |  | Subprocess|     | Telegram      |     | Metrics  |   |
    |  | Connector |     | Escalation    |     | Service  |   |
    |  +-----------+     +---------------+     +----------+   |
    |        |                  |                    |        |
    |        v                  v                    v        |
    |   Claude Code       Telegram API         Prometheus     |
    |   CLI Process       Bot Integration      /metrics       |
    |                                                         |
    +---------------------------------------------------------+
```

### Data Flow

1. **Request Phase**: API receives task request via HTTP or SignalR
2. **Subprocess Execution**: ClaudeCodeSubprocessConnector spawns Claude Code CLI
3. **Permission Check**: If permission denied, escalation triggered
4. **Telegram Escalation**: TelegramEscalationService sends approval request
5. **Human Decision**: Operator approves/rejects via Telegram
6. **Resume/Complete**: Session resumed with approval or task cancelled

### Key Services

| Service | Purpose | Port/Endpoint |
|---------|---------|---------------|
| Orchestra.API | Main API server | 5284 |
| TelegramEscalationService | Human approval requests | Telegram API |
| ApprovalTimeoutService | Background timeout monitor | N/A (IHostedService) |
| EscalationMetricsService | Observability metrics | /metrics |
| CircuitBreakerPolicyService | Resilience patterns | N/A |

### Resilience Mechanisms

- **Circuit Breaker**: Opens after 5 consecutive failures or 50% failure rate
- **Retry Policies**: Exponential backoff with jitter (1s, 2s, 4s, max 16s)
- **Timeout Management**: Configurable approval timeouts (default: 30 min)
- **Graceful Degradation**: Returns false instead of exceptions on failures

---

## 2. Prerequisites & Environment Setup

### Required Environment Variables

| Variable | Description | Required | Default |
|----------|-------------|----------|---------|
| `TELEGRAM_BOT_TOKEN` | Telegram bot authentication token | Yes (for escalation) | "" |
| `TELEGRAM_CHAT_ID` | Telegram chat for approval requests | Yes (for escalation) | "" |
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | No | Production |
| `ConnectionStrings__DefaultConnection` | Database connection string | Yes | SQLite |

### Configuration Settings (appsettings.json)

```json
{
  "ClaudeCodeSubprocess": {
    "TelegramRetry": {
      "MaxRetryAttempts": 3,
      "InitialDelayMs": 1000,
      "MaxDelayMs": 16000,
      "JitterEnabled": true,
      "RetryOn": [429, 500, 502, 503, 504]
    },
    "ApprovalTimeout": {
      "DefaultTimeoutMinutes": 30,
      "MaxTimeoutMinutes": 120,
      "CheckIntervalSeconds": 30,
      "GracePeriodSeconds": 0,
      "MaxConcurrentTimeouts": 10
    }
  },
  "OpenTelemetry": {
    "Enabled": true,
    "ServiceName": "Orchestra.API",
    "MetricsEnabled": true,
    "PrometheusEndpoint": "/metrics"
  },
  "TelegramEscalation": {
    "BotToken": "",
    "ChatId": "",
    "Enabled": false,
    "RequestTimeoutMs": 30000,
    "MaxRetries": 3
  }
}
```

### Database Requirements

| Environment | Database | Connection String Pattern |
|-------------|----------|---------------------------|
| Development | SQLite | `Data Source=orchestra.db` |
| Production | PostgreSQL | `Host=;Database=;Username=;Password=` |

### Hardware Requirements

| Resource | Minimum | Recommended |
|----------|---------|-------------|
| RAM | 2 GB | 4 GB |
| CPU | 1 core | 2+ cores |
| Disk | 1 GB | 5 GB |
| Network | Outbound HTTPS | Low latency |

### Network Requirements

- **Outbound HTTPS (443)**: Required for Telegram API
- **Internal Port 5284**: API server
- **Internal Port 6379**: Redis (optional, for distributed caching)

---

## 3. Startup Procedures

### Standard Startup

```bash
# Navigate to API project
cd src/Orchestra.API

# Start API server
dotnet run --launch-profile http

# Expected output:
# info: Microsoft.Hosting.Lifetime[14]
#       Now listening on: http://localhost:5284
# info: Microsoft.Hosting.Lifetime[0]
#       Application started.
```

### Production Startup

```bash
# Build release version
dotnet publish -c Release -o /app/release

# Set environment variables
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__DefaultConnection="Host=db.example.com;Database=orchestra;..."
export TELEGRAM_BOT_TOKEN="your-bot-token"
export TELEGRAM_CHAT_ID="your-chat-id"

# Start application
cd /app/release
dotnet Orchestra.API.dll
```

### Service Verification

```bash
# 1. Health check endpoint
curl http://localhost:5284/health
# Expected: 200 OK

# 2. Metrics endpoint
curl http://localhost:5284/metrics
# Expected: Prometheus format metrics

# 3. Database check
curl http://localhost:5284/api/diagnostics/database
# Expected: 200 OK with database status

# 4. API documentation
curl http://localhost:5284/swagger/index.html
# Expected: Swagger UI page
```

### Background Services Verification

The following IHostedService instances start automatically:

| Service | Purpose | Log Pattern |
|---------|---------|-------------|
| ApprovalTimeoutService | Monitor approval timeouts | "ApprovalTimeoutService started" |
| AgentDiscoveryService | Discover Claude Code agents | "Agent discovery started" |
| AgentHealthCheckService | Monitor agent health | "Health check service started" |

---

## 4. Monitoring & Alerts

### Key Metrics to Monitor

#### Escalation Queue Metrics

| Metric | Type | Alert Threshold | Description |
|--------|------|-----------------|-------------|
| `escalation_queue_size` | Gauge | > 100 | Current pending approvals |
| `escalation_queue_enqueue_total` | Counter | N/A | Total items added to queue |
| `escalation_queue_dequeue_total` | Counter | N/A | Total items processed |

#### Approval Statistics

| Metric | Type | Alert Threshold | Description |
|--------|------|-----------------|-------------|
| `escalation_approvals_accepted_total` | Counter | N/A | Total approved requests |
| `escalation_approvals_rejected_total` | Counter | N/A | Total rejected requests |
| `escalation_approvals_timeout_total` | Counter | > 5/hour | Timed-out requests |
| `escalation_response_time_seconds` | Histogram | P95 > 300s | Decision time |

#### Telegram API Metrics

| Metric | Type | Alert Threshold | Description |
|--------|------|-----------------|-------------|
| `telegram_api_requests_total` | Counter | N/A | Total API calls |
| `telegram_api_failures_total` | Counter | > 5% rate | Failed API calls |
| `telegram_api_retry_attempts_total` | Counter | > 10/min | Retry attempts |
| `telegram_api_duration_seconds` | Histogram | P95 > 5s | API latency |

#### System Health

| Metric | Type | Alert Threshold | Description |
|--------|------|-----------------|-------------|
| `approval_timeout_service_health` | Gauge | = 0 | Service health (1=OK) |
| `telegram_service_health` | Gauge | = 0 | Telegram connection |

### Prometheus Scrape Configuration

```yaml
# prometheus.yml
scrape_configs:
  - job_name: 'orchestra-api'
    scrape_interval: 15s
    static_configs:
      - targets: ['localhost:5284']
    metrics_path: '/metrics'
```

### Log Patterns to Watch

| Pattern | Severity | Action |
|---------|----------|--------|
| `ERROR` | Error | Investigate immediately |
| `timeout` | Warning | Check network/Telegram API |
| `circuit breaker open` | Critical | Telegram API likely down |
| `retry attempt` | Info | Monitor frequency |
| `approval cancelled` | Info | Normal timeout behavior |

### Grafana Dashboard Queries

```promql
# Queue size over time
escalation_queue_size

# Approval rate (approvals per minute)
rate(escalation_approvals_accepted_total[5m]) * 60

# Failure rate percentage
rate(telegram_api_failures_total[5m]) / rate(telegram_api_requests_total[5m]) * 100

# P95 response time
histogram_quantile(0.95, rate(escalation_response_time_seconds_bucket[5m]))
```

---

## 5. Troubleshooting Guide

### Problem: Queue Growing Indefinitely

**Symptoms**:
- `escalation_queue_size` metric increasing
- Approvals not being processed

**Diagnosis**:
```bash
# Check queue size
curl http://localhost:5284/api/diagnostics/metrics | jq '.QueueSize'

# Check Telegram service health
curl http://localhost:5284/api/diagnostics/metrics | jq '.TelegramServiceHealthy'
```

**Solutions**:
1. Check Telegram bot token is valid
2. Verify Telegram chat ID is correct
3. Check network connectivity to api.telegram.org
4. Restart ApprovalTimeoutService if stuck

---

### Problem: Approvals Timing Out

**Symptoms**:
- `escalation_approvals_timeout_total` increasing
- Users not receiving Telegram messages

**Diagnosis**:
```bash
# Check recent timeouts
grep "approval.*timeout" /var/log/orchestra/api.log | tail -20

# Verify Telegram delivery
curl http://localhost:5284/api/diagnostics/telegram/status
```

**Solutions**:
1. Verify human operators are available
2. Check Telegram notification settings
3. Increase timeout duration if needed
4. Review approval request clarity

---

### Problem: Metrics Missing

**Symptoms**:
- `/metrics` endpoint returns empty or partial data
- Grafana dashboards show no data

**Diagnosis**:
```bash
# Check metrics endpoint
curl http://localhost:5284/metrics | head -50

# Check OpenTelemetry configuration
grep -A10 "OpenTelemetry" appsettings.json
```

**Solutions**:
1. Verify `OpenTelemetry.Enabled` is true
2. Check `MetricsEnabled` is true
3. Restart application to reinitialize meters
4. Check for exceptions in startup logs

---

### Problem: Circuit Breaker Open

**Symptoms**:
- Log messages: "Circuit breaker open"
- Telegram messages not being sent
- Immediate fallback responses

**Diagnosis**:
```bash
# Check circuit breaker state
curl http://localhost:5284/api/diagnostics/circuit-breaker

# Check recent failures
grep "telegram.*fail" /var/log/orchestra/api.log | tail -20
```

**Solutions**:
1. Wait for break duration (default: 30 seconds)
2. Check Telegram API status: https://core.telegram.org/api/errors
3. Verify bot token hasn't been revoked
4. Check for rate limiting (HTTP 429)

---

### Problem: High Response Latency

**Symptoms**:
- `escalation_response_time_seconds` P95 > 300s
- API requests timing out

**Diagnosis**:
```bash
# Check resource usage
top -p $(pgrep -f Orchestra.API)

# Check database performance
curl http://localhost:5284/api/diagnostics/database/performance
```

**Solutions**:
1. Check database connection pool exhaustion
2. Monitor memory usage for leaks
3. Scale horizontally if needed
4. Review slow query logs

---

### Problem: Session Lost

**Symptoms**:
- "Session not found" errors
- Unable to resume Claude Code sessions

**Diagnosis**:
```bash
# Check session in database
sqlite3 orchestra.db "SELECT * FROM AgentSessions WHERE SessionId='...';"

# Check for crashes
grep -i "crash\|exception\|unhandled" /var/log/orchestra/api.log
```

**Solutions**:
1. Verify database file isn't corrupted
2. Check disk space
3. Review Entity Framework context lifecycle
4. Restart session from scratch if needed

---

### Common Error Messages

| Error | Cause | Solution |
|-------|-------|----------|
| "Attempted to update or delete an entity that does not exist" | DbContext tracking issue | Reload entity before update |
| "Circuit breaker open" | Telegram API unavailable | Wait for recovery or check API |
| "Timeout waiting for approval" | Human response pending | Normal behavior, increase timeout |
| "Failed to send Telegram message" | Network/token issue | Check token and network |
| "Session not found" | Invalid session ID | Create new session |
| "Maximum retry attempts exceeded" | Persistent failures | Check root cause, wait for recovery |

---

## 6. Backup & Recovery

### Backup Strategy

```bash
# Daily SQLite backup
sqlite3 orchestra.db ".backup '/backup/orchestra-$(date +%Y%m%d).db'"

# PostgreSQL backup
pg_dump -h db.example.com -U orchestra_user orchestra > backup.sql

# Upload to S3
aws s3 cp backup.sql s3://orchestra-backups/$(date +%Y%m%d)/
```

### Backup Verification

```bash
# Verify SQLite backup integrity
sqlite3 backup.db "PRAGMA integrity_check;"
# Expected: ok

# Verify data exists
sqlite3 backup.db "SELECT COUNT(*) FROM ApprovalRequests;"
# Expected: non-zero count
```

### Recovery Procedure

```bash
# 1. Stop application
systemctl stop orchestra-api

# 2. Restore database
# SQLite:
cp /backup/orchestra-latest.db orchestra.db

# PostgreSQL:
psql -h db.example.com -U orchestra_user -d orchestra < backup.sql

# 3. Verify restoration
sqlite3 orchestra.db "SELECT COUNT(*) FROM AgentSessions;"

# 4. Start application
systemctl start orchestra-api

# 5. Verify health
curl http://localhost:5284/health
```

### Data Retention Policy

| Data Type | Retention Period | Action |
|-----------|------------------|--------|
| Approval Requests | 90 days | Archive then delete |
| Agent Sessions | 90 days | Archive then delete |
| Metrics Data | 30 days | Prometheus handles |
| Logs | 7 days | Rotate and compress |

### Disaster Recovery

**Manual Approval Callback** (when Telegram is completely unavailable):

```bash
# Approve pending request via API
curl -X POST http://localhost:5284/api/approvals/{approvalId}/approve \
  -H "Content-Type: application/json" \
  -d '{"approved": true, "reason": "Manual approval due to Telegram outage"}'
```

---

## 7. Performance Tuning

### Database Optimization

```sql
-- Create indexes for common queries
CREATE INDEX IF NOT EXISTS IX_AgentSessions_SessionId ON AgentSessions(SessionId);
CREATE INDEX IF NOT EXISTS IX_AgentSessions_Status ON AgentSessions(Status);
CREATE INDEX IF NOT EXISTS IX_ApprovalRequests_CreatedAt ON HumanApprovals(CreatedAt);
CREATE INDEX IF NOT EXISTS IX_ApprovalRequests_Status ON HumanApprovals(Status);

-- Analyze tables for query optimizer
ANALYZE AgentSessions;
ANALYZE HumanApprovals;
```

### Connection Pool Configuration

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=db;Database=orchestra;Pooling=true;MinPoolSize=5;MaxPoolSize=100;"
  }
}
```

### Timeout Tuning

| Setting | Default | High Volume | Low Latency |
|---------|---------|-------------|-------------|
| CheckIntervalSeconds | 30 | 60 | 15 |
| DefaultTimeoutMinutes | 30 | 60 | 15 |
| MaxConcurrentTimeouts | 10 | 50 | 5 |

### Circuit Breaker Tuning

| Setting | Default | Stable API | Unstable API |
|---------|---------|------------|--------------|
| FailureRateThreshold | 50% | 30% | 70% |
| ConsecutiveFailuresThreshold | 5 | 3 | 10 |
| BreakDurationSeconds | 30 | 60 | 15 |

### Metric Collection Tuning

```json
{
  "OpenTelemetry": {
    "ExportIntervalSeconds": 60,
    "MaxMetricPoints": 10000,
    "DetailedMetricsEnabled": false
  }
}
```

**Recommendation**: For high-throughput systems, set `ExportIntervalSeconds` to 30 and enable sampling.

---

## Contact & Support

| Issue Type | Contact | Response Time |
|------------|---------|---------------|
| Critical (system down) | DevOps On-Call | 15 minutes |
| High (degraded service) | DevOps Team | 1 hour |
| Medium (feature issue) | Development Team | 4 hours |
| Low (documentation) | Documentation Team | 24 hours |

---

**Document Version**: 1.0
**Created**: 2025-11-24
**Next Review**: 2026-02-24
