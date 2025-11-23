# ClaudeCodeSubprocessConnector Deployment Guide

**Version**: 1.0
**Last Updated**: 2025-11-24
**Status**: Production Ready
**Author**: AI Agent Orchestra Team

---

## 1. Pre-Deployment Checklist

Before deploying to any environment, verify the following items are complete:

### Code Quality

- [x] All tests passing (100+ tests)
- [x] Build successful with 0 errors
- [x] Code review completed with 95%+ compliance
- [x] Security scanning passed (NuGet packages verified)
- [x] No hardcoded secrets in codebase

### Architecture Validation

- [x] CQRS pattern properly implemented with MediatR
- [x] Entity Framework DbContext lifecycle correct
- [x] Dependency injection configured for all services
- [x] Circuit breaker pattern operational
- [x] Retry policies with Polly configured

### Testing Validation

- [x] Unit tests: 100+ passing
- [x] Integration tests: All passing
- [x] Load tests: Performance targets met (<100ms response)
- [x] Chaos tests: Resilience validated (71 hardening tests)
- [x] Circuit breaker tests: 24 tests passing

### Production Readiness

- [x] Performance benchmarks within targets
- [x] Circuit breaker failure thresholds configured
- [x] Database migrations prepared
- [x] Environment variables documented
- [x] Monitoring endpoints operational (/metrics, /health)

### Documentation

- [x] Operations runbook created
- [x] This deployment guide complete
- [x] API documentation (Swagger) operational
- [x] Architecture documentation updated

---

## 2. Environment Setup

### Development Environment

```json
{
  "Environment": "Development",
  "Database": "SQLite (in-memory or file)",
  "ConnectionString": "Data Source=orchestra.db",
  "TelegramEnabled": false,
  "MetricsEnabled": true
}
```

**Characteristics**:
- Hot reload enabled
- Detailed logging
- Swagger UI enabled
- No Telegram integration required
- SQLite for simplicity

### Staging Environment

```json
{
  "Environment": "Staging",
  "Database": "PostgreSQL 13+",
  "ConnectionString": "Host=staging-db;Database=orchestra_staging;Username=app;Password=***",
  "TelegramEnabled": true,
  "MetricsEnabled": true
}
```

**Characteristics**:
- Production-like configuration
- Real Telegram bot (test channel)
- PostgreSQL database
- Full monitoring enabled
- Performance profiling

### Production Environment

```json
{
  "Environment": "Production",
  "Database": "PostgreSQL 13+ with replication",
  "ConnectionString": "Host=prod-db-primary;Database=orchestra;Username=app;Password=***;Pooling=true;MaxPoolSize=100",
  "TelegramEnabled": true,
  "MetricsEnabled": true,
  "TLS": "Required"
}
```

**Characteristics**:
- High availability setup
- Database replication
- TLS/SSL required
- Production Telegram bot
- Full alerting configured

### Secrets Management

**Recommended Solutions**:

| Platform | Solution | Configuration |
|----------|----------|---------------|
| Azure | Azure Key Vault | `Azure:KeyVault:VaultUri` |
| AWS | AWS Secrets Manager | `AWS:SecretsManager:SecretId` |
| Kubernetes | K8s Secrets | Mounted as environment variables |
| Docker | Docker Secrets | `/run/secrets/` mount |

**Required Secrets**:

```bash
# Telegram Integration
TELEGRAM_BOT_TOKEN=<your-bot-token>
TELEGRAM_CHAT_ID=<your-chat-id>

# Database
DB_CONNECTION_STRING=<connection-string>

# Optional: Monitoring
OTEL_EXPORTER_OTLP_ENDPOINT=<otlp-endpoint>
```

### TLS Certificate Requirements

| Environment | Certificate | Source |
|-------------|-------------|--------|
| Development | Self-signed | `dotnet dev-certs https` |
| Staging | Let's Encrypt | certbot or acme.sh |
| Production | Commercial CA | DigiCert, GlobalSign, etc. |

---

## 3. Deployment Steps

### Step 1: Prepare Infrastructure

```bash
# 1.1 Provision PostgreSQL database (production)
# AWS RDS example:
aws rds create-db-instance \
  --db-instance-identifier orchestra-prod \
  --db-instance-class db.t3.medium \
  --engine postgres \
  --engine-version 15.4 \
  --allocated-storage 20 \
  --master-username orchestra_admin \
  --master-user-password <secure-password>

# 1.2 Create backup bucket
aws s3 mb s3://orchestra-backups-prod

# 1.3 Configure Telegram bot
# Visit https://t.me/BotFather and create bot
# Get bot token and target chat ID

# 1.4 Set up monitoring (Prometheus)
# Deploy Prometheus with Orchestra scrape target
```

### Step 2: Build & Package

```bash
# 2.1 Clone repository (if needed)
git clone https://github.com/your-org/AI-Agent-Orchestra.git
cd AI-Agent-Orchestra

# 2.2 Restore dependencies
dotnet restore AI-Agent-Orchestra.sln

# 2.3 Run tests (must pass!)
dotnet test src/Orchestra.Tests/
# Expected: 100+ tests passing

# 2.4 Build release version
dotnet publish src/Orchestra.API/ -c Release -o /app/release

# 2.5 Verify build output
ls -la /app/release/
# Expected: Orchestra.API.dll and dependencies
```

### Step 3: Database Migration

```bash
# 3.1 Install EF Core tools (if needed)
dotnet tool install --global dotnet-ef

# 3.2 Generate migration script (for review)
cd src/Orchestra.API
dotnet ef migrations script -o migration.sql

# 3.3 Apply migrations to database
dotnet ef database update --connection "<connection-string>"

# 3.4 Verify migrations applied
psql -h <host> -U <user> -d orchestra -c "SELECT * FROM __EFMigrationsHistory;"
# Expected: List of applied migrations
```

### Step 4: Deploy Application

```bash
# 4.1 Stop previous version (if exists)
systemctl stop orchestra-api
# Or: docker stop orchestra-api

# 4.2 Backup current version
cp -r /app/current /app/previous-$(date +%Y%m%d)

# 4.3 Copy release files
cp -r /app/release/* /app/current/

# 4.4 Set environment variables
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__DefaultConnection="<connection-string>"
export TelegramEscalation__BotToken="<bot-token>"
export TelegramEscalation__ChatId="<chat-id>"
export TelegramEscalation__Enabled="true"

# 4.5 Start API server
cd /app/current
dotnet Orchestra.API.dll &

# Or with systemd:
systemctl start orchestra-api

# Or with Docker:
docker run -d --name orchestra-api \
  -p 5284:5284 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__DefaultConnection="..." \
  -e TelegramEscalation__BotToken="..." \
  -e TelegramEscalation__Enabled="true" \
  orchestra-api:latest
```

### Step 5: Smoke Tests

```bash
# 5.1 Health check
curl -f http://localhost:5284/health
# Expected: 200 OK

# 5.2 Metrics endpoint
curl -f http://localhost:5284/metrics | head -20
# Expected: Prometheus format metrics

# 5.3 API documentation
curl -f http://localhost:5284/swagger/index.html | head -5
# Expected: HTML response

# 5.4 Database connectivity
curl -f http://localhost:5284/api/diagnostics/database
# Expected: 200 OK with database status

# 5.5 Create test approval request (optional)
curl -X POST http://localhost:5284/api/approvals/test \
  -H "Content-Type: application/json" \
  -d '{"toolName": "TestTool", "sessionId": "test-session"}'
# Expected: 201 Created or test message sent to Telegram

# 5.6 Verify Telegram message received
# Check your Telegram chat for test message

# 5.7 Cancel test approval
curl -X DELETE http://localhost:5284/api/approvals/test
# Expected: 200 OK
```

### Step 6: Service Verification

```bash
# 6.1 Verify database connectivity
psql -h <host> -U <user> -d orchestra -c "SELECT 1;"
# Expected: Returns 1

# 6.2 Verify Telegram API (manual)
# Send test message via bot API:
curl "https://api.telegram.org/bot<token>/sendMessage?chat_id=<chat_id>&text=DeploymentTest"
# Expected: 200 OK with message details

# 6.3 Verify metrics collection
curl http://localhost:5284/metrics | grep escalation
# Expected: escalation_* metrics present

# 6.4 Verify circuit breaker state
curl http://localhost:5284/api/diagnostics/circuit-breaker
# Expected: State = Closed (healthy)

# 6.5 Check background services
grep -E "ApprovalTimeout|AgentDiscovery|HealthCheck" /var/log/orchestra/api.log | tail -5
# Expected: Service started messages
```

---

## 4. Rollback Procedure

### Prerequisites

- Keep previous 2 versions in `/app/previous-*`
- Database backup before upgrade
- Documented rollback steps

### Rollback Steps

```bash
# 1. Stop current API server
systemctl stop orchestra-api

# 2. Identify rollback version
ls -la /app/previous-*
# Select appropriate version

# 3. Restore from previous backup (if data corruption)
# SQLite:
cp /backup/orchestra-<date>.db /app/data/orchestra.db

# PostgreSQL:
pg_restore -h <host> -U <user> -d orchestra /backup/orchestra-<date>.dump

# 4. Restore previous application version
rm -rf /app/current/*
cp -r /app/previous-<date>/* /app/current/

# 5. Start previous version
systemctl start orchestra-api

# 6. Verify all services operational
curl -f http://localhost:5284/health
curl -f http://localhost:5284/metrics | head -5

# 7. Document rollback reason
echo "Rollback on $(date): <reason>" >> /var/log/orchestra/rollback.log
```

### Maximum Downtime Target

- **Target**: 5 minutes
- **Steps**: Stop (30s) + Restore (2-3min) + Start (30s) + Verify (1min)

### Rollback Decision Criteria

| Condition | Action |
|-----------|--------|
| Health check fails after 2 minutes | Rollback |
| Error rate > 10% for 5 minutes | Rollback |
| Database connection failures | Rollback |
| Critical functionality broken | Rollback |

---

## 5. Database Migration Strategy

### Migration Principles

1. **Always test migrations on staging first**
2. **Keep 2 schema versions backward compatible**
3. **Use EF Core down migrations for rollback**
4. **Backup before any schema change**

### Zero-Downtime Migration Process

```bash
# Phase 1: Apply additive changes (safe)
# - New tables
# - New columns with defaults
# - New indexes

# Phase 2: Deploy new application version
# - Application handles both old and new schema

# Phase 3: Data migration (if needed)
# - Background job for data transformation
# - Verify data integrity

# Phase 4: Remove deprecated elements (next release)
# - Drop old columns/tables
# - Only after confirming no rollback needed
```

### Rollback Migration

```bash
# Generate down migration
dotnet ef migrations script <current-migration> <previous-migration> -o rollback.sql

# Apply rollback
psql -h <host> -U <user> -d orchestra -f rollback.sql
```

### Migration Verification

```sql
-- Check migration history
SELECT * FROM __EFMigrationsHistory ORDER BY MigrationId DESC;

-- Verify schema version
SELECT table_name FROM information_schema.tables WHERE table_schema = 'public';

-- Check for missing indexes
SELECT indexname, indexdef FROM pg_indexes WHERE schemaname = 'public';
```

---

## 6. Monitoring Post-Deployment

### First 24 Hours Monitoring

| Metric | Check Frequency | Alert Threshold |
|--------|-----------------|-----------------|
| Error rate (HTTP 5xx) | Every minute | > 1% |
| Response latency P95 | Every minute | > 1000ms |
| Circuit breaker state | Every 5 minutes | Open |
| Queue size | Every 5 minutes | > 50 |
| Memory usage | Every 5 minutes | > 80% |
| CPU usage | Every 5 minutes | > 70% |

### Alert Configuration

```yaml
# Prometheus alerting rules
groups:
  - name: orchestra-deployment
    rules:
      - alert: HighErrorRate
        expr: rate(http_requests_total{status=~"5.."}[5m]) / rate(http_requests_total[5m]) > 0.01
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: High error rate detected

      - alert: HighLatency
        expr: histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m])) > 1
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: High latency detected

      - alert: CircuitBreakerOpen
        expr: circuit_breaker_state == 1
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: Circuit breaker is open
```

### Performance Baseline

Capture baseline metrics in the first week:

```bash
# Export initial baseline
curl http://localhost:5284/metrics > baseline-$(date +%Y%m%d).txt

# Key metrics to capture:
# - Average response time
# - P95 response time
# - Error rate
# - Memory usage
# - CPU usage
# - Queue processing rate
```

### User Feedback Channels

- Monitor Telegram channel for escalation issues
- Check error logs for unhandled exceptions
- Review approval completion rates
- Track timeout frequency

---

## 7. Post-Deployment Validation

### Validation Checklist

- [ ] All metrics flowing to Prometheus
- [ ] No unhandled exceptions in logs
- [ ] Queue processing at expected rate
- [ ] Human approvals completing successfully
- [ ] No circuit breaker state issues
- [ ] Database connections stable
- [ ] Memory usage within limits
- [ ] Response times within SLA

### Validation Commands

```bash
# 1. Check metrics flow
curl http://localhost:5284/metrics | grep -c "#"
# Expected: > 50 (metric entries)

# 2. Check for exceptions
grep -c "Exception" /var/log/orchestra/api.log
# Expected: 0 or very low

# 3. Check queue processing
curl http://localhost:5284/api/diagnostics/metrics | jq '.QueueSize'
# Expected: Low number (< 10)

# 4. Check circuit breaker
curl http://localhost:5284/api/diagnostics/circuit-breaker | jq '.State'
# Expected: "Closed"

# 5. Check database connections
curl http://localhost:5284/api/diagnostics/database | jq '.ConnectionsActive'
# Expected: < MaxPoolSize
```

### Sign-Off Criteria

| Criteria | Target | Validation Method |
|----------|--------|-------------------|
| Health check | 200 OK | `curl /health` |
| Error rate | < 0.1% | Prometheus metrics |
| Response time | < 100ms P95 | Prometheus metrics |
| Circuit breaker | Closed | `/api/diagnostics/circuit-breaker` |
| Metrics flowing | Yes | `/metrics` non-empty |
| Logs clean | No errors | Log file review |

---

## Docker Deployment (Alternative)

### Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5284

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Orchestra.API/Orchestra.API.csproj", "Orchestra.API/"]
COPY ["src/Orchestra.Core/Orchestra.Core.csproj", "Orchestra.Core/"]
COPY ["src/Orchestra.Agents/Orchestra.Agents.csproj", "Orchestra.Agents/"]
RUN dotnet restore "Orchestra.API/Orchestra.API.csproj"
COPY src/ .
RUN dotnet publish "Orchestra.API/Orchestra.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Orchestra.API.dll"]
```

### Docker Compose

```yaml
version: '3.8'
services:
  orchestra-api:
    build: .
    ports:
      - "5284:5284"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Host=db;Database=orchestra;Username=app;Password=secret
      - TelegramEscalation__BotToken=${TELEGRAM_BOT_TOKEN}
      - TelegramEscalation__ChatId=${TELEGRAM_CHAT_ID}
      - TelegramEscalation__Enabled=true
    depends_on:
      - db
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5284/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  db:
    image: postgres:15
    environment:
      - POSTGRES_DB=orchestra
      - POSTGRES_USER=app
      - POSTGRES_PASSWORD=secret
    volumes:
      - postgres_data:/var/lib/postgresql/data

  prometheus:
    image: prom/prometheus
    ports:
      - "9090:9090"
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml

volumes:
  postgres_data:
```

---

## Contact & Support

| Issue Type | Contact | Escalation Time |
|------------|---------|-----------------|
| Deployment failure | DevOps Team | Immediate |
| Database issues | DBA Team | 15 minutes |
| Application errors | Development Team | 30 minutes |
| Security concerns | Security Team | Immediate |

---

**Document Version**: 1.0
**Created**: 2025-11-24
**Next Review**: 2026-02-24
