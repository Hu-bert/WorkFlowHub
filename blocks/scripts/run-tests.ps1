# Buduje i testuje repo zadania. Sam rozpoznaje technologię (.NET / npm).
# Kod wyjścia różny od 0 = testy nie przeszły (krok dostaje status Błąd).
# 'Continue', bo dotnet i npm piszą na stderr - przy 'Stop' PowerShell 5.1 uznałby to za błąd.
$ErrorActionPreference = 'Continue'

$repo = $env:FLOWHUB_REPO_PATH
Set-Location $repo
$ran = $false

$solution = Get-ChildItem -Path $repo -Include *.sln, *.slnx -Recurse -Depth 2 -File | Select-Object -First 1
if ($solution) {
    Write-Output "dotnet test $($solution.Name)"
    & dotnet test $solution.FullName --nologo 2>&1 | ForEach-Object { "$_" }
    if ($LASTEXITCODE -ne 0) { Write-Output 'Testy .NET: BŁĄD'; exit $LASTEXITCODE }
    Write-Output 'Testy .NET: OK'
    $ran = $true
}

$package = Join-Path $repo 'package.json'
if ((Test-Path $package) -and ((Get-Content $package -Raw | ConvertFrom-Json).scripts.test)) {
    Write-Output 'npm test'
    & npm test 2>&1 | ForEach-Object { "$_" }
    if ($LASTEXITCODE -ne 0) { Write-Output 'Testy npm: BŁĄD'; exit $LASTEXITCODE }
    Write-Output 'Testy npm: OK'
    $ran = $true
}

if (-not $ran) {
    Write-Output "Repo $($env:FLOWHUB_REPO_NAME): nie znalazłem testów do uruchomienia (brak solucji .NET i skryptu npm test)."
}

exit 0
