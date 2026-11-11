# Tworzy katalog zadania i zapisuje w nim task.md (treść zadania dla agentów).
$ErrorActionPreference = 'Stop'

$workDir = $env:FLOWHUB_WORK_DIR
New-Item -ItemType Directory -Force -Path $workDir | Out-Null

$task = @"
# $($env:FLOWHUB_TASK_KEY): $($env:FLOWHUB_TASK_TITLE)

- Typ: $($env:FLOWHUB_TASK_TYPE)
- Link: $($env:FLOWHUB_TASK_URL)

## Treść zadania

$($env:FLOWHUB_TASK_DESCRIPTION)
"@

[IO.File]::WriteAllText((Join-Path $workDir 'task.md'), $task, (New-Object Text.UTF8Encoding $false))

Write-Output "Katalog zadania: $workDir"
Write-Output "Zapisano task.md"

# Jawny kod wyjścia: wcześniejsze polecenia git mogły zostawić $LASTEXITCODE różny od 0.
exit 0
