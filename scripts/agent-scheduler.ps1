# AI Agent Orchestra - Agent Scheduler
# Простой скрипт для автоматического пинга и управления агентами

param(
    [int]$IntervalSeconds = 30,
    [string]$OrchestratorUrl = "http://localhost:5000",
    [string]$ConfigPath = "agent-config.json"
)

Write-Host "=== AI Agent Orchestra Scheduler ===" -ForegroundColor Green
Write-Host "Orchestrator URL: $OrchestratorUrl" -ForegroundColor Yellow
Write-Host "Ping Interval: $IntervalSeconds seconds" -ForegroundColor Yellow
Write-Host "Config Path: $ConfigPath" -ForegroundColor Yellow
Write-Host ""

# Функция для загрузки конфигурации агентов
function Load-AgentConfig {
    param([string]$Path)

    if (Test-Path $Path) {
        try {
            $config = Get-Content $Path | ConvertFrom-Json
            Write-Host "Loaded configuration with $($config.agents.Count) agents" -ForegroundColor Green
            return $config
        }
        catch {
            Write-Host "Error loading config: $($_.Exception.Message)" -ForegroundColor Red
            return $null
        }
    }
    else {
        Write-Host "Config file not found. Creating sample config..." -ForegroundColor Yellow
        $sampleConfig = @{
            agents = @(
                @{
                    id = "claude-1"
                    name = "Claude Agent 1"
                    type = "claude-code"
                    repositoryPath = "C:\Users\mrred\RiderProjects\Galactic-Idlers"
                    terminalPath = "C:\Users\mrred\RiderProjects\Galactic-Idlers"
                    enabled = $true
                },
                @{
                    id = "claude-2"
                    name = "Claude Agent 2"
                    type = "claude-code"
                    repositoryPath = "C:\Users\mrred\RiderProjects\AI-Agent-Orchestra"
                    terminalPath = "C:\Users\mrred\RiderProjects\AI-Agent-Orchestra"
                    enabled = $true
                }
            )
        }
        $sampleConfig | ConvertTo-Json -Depth 3 | Set-Content $Path
        Write-Host "Sample config created at $Path" -ForegroundColor Green
        return $sampleConfig
    }
}

# Функция для регистрации агента в оркестраторе
function Register-Agent {
    param($Agent, $OrchestratorUrl)

    try {
        $body = @{
            Id = $Agent.id
            Name = $Agent.name
            Type = $Agent.type
            RepositoryPath = $Agent.repositoryPath
        } | ConvertTo-Json

        $response = Invoke-RestMethod -Uri "$OrchestratorUrl/agents/register" -Method Post -Body $body -ContentType "application/json"
        Write-Host "✓ Registered agent: $($Agent.name)" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "✗ Failed to register agent $($Agent.name): $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Функция для пинга агента
function Ping-Agent {
    param($Agent, $OrchestratorUrl)

    try {
        # Проверяем доступность директории
        $status = if (Test-Path $Agent.repositoryPath) { "Idle" } else { "Error" }

        $body = @{
            Status = $status
            CurrentTask = $null
        } | ConvertTo-Json

        $response = Invoke-RestMethod -Uri "$OrchestratorUrl/agents/$($Agent.id)/ping" -Method Post -Body $body -ContentType "application/json"
        Write-Host "→ Pinged $($Agent.name) [$status]" -ForegroundColor Cyan
        return $true
    }
    catch {
        Write-Host "✗ Failed to ping agent $($Agent.name): $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Функция для проверки состояния оркестратора
function Get-OrchestratorState {
    param($OrchestratorUrl)

    try {
        $state = Invoke-RestMethod -Uri "$OrchestratorUrl/state" -Method Get
        return $state
    }
    catch {
        Write-Host "✗ Failed to get orchestrator state: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

# Основной цикл
try {
    $config = Load-AgentConfig -Path $ConfigPath
    if (-not $config) {
        Write-Host "Cannot continue without valid configuration" -ForegroundColor Red
        exit 1
    }

    # Регистрируем всех агентов при запуске
    Write-Host "Registering agents..." -ForegroundColor Yellow
    foreach ($agent in $config.agents) {
        if ($agent.enabled) {
            Register-Agent -Agent $agent -OrchestratorUrl $OrchestratorUrl
        }
    }

    Write-Host "`nStarting monitoring loop..." -ForegroundColor Green
    $iteration = 0

    while ($true) {
        $iteration++
        Write-Host "`n--- Iteration $iteration $(Get-Date -Format 'HH:mm:ss') ---" -ForegroundColor Magenta

        # Пингуем всех активных агентов
        foreach ($agent in $config.agents) {
            if ($agent.enabled) {
                Ping-Agent -Agent $agent -OrchestratorUrl $OrchestratorUrl
            }
        }

        # Показываем состояние оркестратора каждые 5 итераций
        if ($iteration % 5 -eq 0) {
            Write-Host "`nOrchestrator State:" -ForegroundColor Yellow
            $state = Get-OrchestratorState -OrchestratorUrl $OrchestratorUrl
            if ($state) {
                Write-Host "Agents: $($state.Agents.Count)" -ForegroundColor White
                Write-Host "Tasks in queue: $($state.TaskQueue.Count)" -ForegroundColor White
                Write-Host "Last update: $($state.LastUpdate)" -ForegroundColor White
            }
        }

        Start-Sleep -Seconds $IntervalSeconds
    }
}
catch {
    Write-Host "Scheduler stopped with error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
finally {
    Write-Host "`nScheduler stopped." -ForegroundColor Yellow
}