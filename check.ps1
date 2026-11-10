<#
.SYNOPSIS
  Pełna weryfikacja: build + testy backendu i frontendu. To samo, co musi przejść po każdej fazie.
#>
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$web = Join-Path $root 'web'

function Step([string]$name, [scriptblock]$action) {
    Write-Host "`n== $name ==" -ForegroundColor Magenta
    & $action
    if ($LASTEXITCODE -ne 0) { throw "Nie przeszło: $name" }
}

Step 'dotnet build' { dotnet build (Join-Path $root 'FlowHub.slnx') -nologo -v q }
Step 'dotnet test' { dotnet test --solution (Join-Path $root 'FlowHub.slnx') --no-build }
Step 'npm lint' { npm --prefix $web run lint }
Step 'npm test' { npm --prefix $web test }
Step 'npm build' { npm --prefix $web run build }

Write-Host "`nWszystko zielone." -ForegroundColor Green
