# Быстрое автоматическое исправление ревью
# Версия: 1.0
# Назначение: Одной командой исправить основные нарушения

param(
    [string]$Path = "Docs/PLAN",
    [switch]$Force = $false
)

Write-Host "⚡ БЫСТРОЕ АВТОМАТИЧЕСКОЕ ИСПРАВЛЕНИЕ" -ForegroundColor Green
Write-Host "=" * 50

# Запускаем полную автоматизированную систему с исправлениями
$autoFixArgs = @(
    "-Path", $Path,
    "-AutoFix",
    "-GenerateReport", 
    "-Interactive:$(!$Force)",
    "-CommitChanges:$Force"
)

try {
    Write-Host "🚀 Запускаю автоматизированную систему исправлений..." -ForegroundColor Cyan
    & "$PSScriptRoot/AutomatedReviewSystem.ps1" @autoFixArgs
    
    Write-Host "`n🔄 Проверяю результаты..." -ForegroundColor Cyan
    & "$PSScriptRoot/PlanStructureValidator.ps1" -Path $Path
    
    Write-Host "`n✅ Быстрое исправление завершено!" -ForegroundColor Green
}
catch {
    Write-Host "❌ Ошибка: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}