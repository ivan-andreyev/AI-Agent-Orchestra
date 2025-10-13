Param(
    [Parameter(Mandatory = $true)] [string]$AffectedProjects,
    [Parameter(Mandatory = $true)] [string]$Description
)

$branch = git rev-parse --abbrev-ref HEAD
$seg = $branch.Split('/')[-1]
$hash = ($seg -split '[-_]',2)[0]

if ([string]::IsNullOrWhiteSpace($hash)) {
    Write-Error "Не удалось извлечь хеш задачи из имени ветки: $branch"
    exit 1
}

$msg = "$hash. $AffectedProjects. $Description. Коммит выполнен при помощи Cursor AI."
Write-Output $msg
