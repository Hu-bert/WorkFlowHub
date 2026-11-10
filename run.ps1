<#
.SYNOPSIS
  Buduje frontend i uruchamia FlowHub (http://localhost:5080).
.EXAMPLE
  ./run.ps1          # build frontendu + start API
  ./run.ps1 -Dev     # tryb dev: API + Vite z hot reload (http://localhost:5173)
#>
param([switch]$Dev)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$web = Join-Path $root 'web'
$api = Join-Path $root 'src/FlowHub.Api'

if (-not (Test-Path (Join-Path $web 'node_modules'))) {
    npm --prefix $web ci
}

if ($Dev) {
    $shell = if (Get-Command pwsh -ErrorAction SilentlyContinue) { 'pwsh' } else { 'powershell' }
    Start-Process $shell -ArgumentList '-NoExit', '-Command', "dotnet watch --project '$api'"
    npm --prefix $web run dev
    return
}

npm --prefix $web run build
dotnet run --project $api
