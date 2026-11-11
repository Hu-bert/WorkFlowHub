# Kopiuje agentów i skille FlowHub do repo zadania (.github/agents, .github/skills).
# Skopiowane pliki trafiają do .git/info/exclude, żeby nie weszły do commita ani PR.
# Pliku, który repo już śledzi w Git, nie nadpisujemy.
$ErrorActionPreference = 'Stop'

$blocks = $env:FLOWHUB_BLOCKS_DIR
$repo = $env:FLOWHUB_REPO_PATH
$copied = 0
$skipped = 0

function Test-Tracked([string]$relativePath) {
    $ErrorActionPreference = 'Continue'
    & git -C $repo ls-files --error-unmatch -- $relativePath 2>$null | Out-Null
    return $LASTEXITCODE -eq 0
}

function Add-Exclude([string]$relativePath) {
    $exclude = Join-Path $repo '.git/info/exclude'
    New-Item -ItemType Directory -Force -Path (Split-Path $exclude) | Out-Null
    $line = '/' + $relativePath
    if (-not (Test-Path $exclude) -or -not (Select-String -Path $exclude -SimpleMatch -Pattern $line -Quiet)) {
        Add-Content -Path $exclude -Value $line
    }
}

foreach ($kind in 'agents', 'skills') {
    $source = Join-Path $blocks $kind
    if (-not (Test-Path $source)) { continue }

    $files = Get-ChildItem -Path $source -Recurse -File | Where-Object { $_.Name -ne '.gitkeep' }
    foreach ($file in $files) {
        $relative = ".github/$kind/" + $file.FullName.Substring($source.Length + 1).Replace('\', '/')

        if (Test-Tracked $relative) {
            Write-Output "Pomijam (repo ma własny plik): $relative"
            $skipped++
            continue
        }

        $target = Join-Path $repo $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
        Copy-Item -Path $file.FullName -Destination $target -Force
        Add-Exclude $relative
        $copied++
    }
}

Write-Output "Repo $($env:FLOWHUB_REPO_NAME): skopiowano $copied plików, pominięto $skipped."
