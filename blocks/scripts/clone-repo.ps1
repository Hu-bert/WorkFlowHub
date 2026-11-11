# Klonuje repo do katalogu zadania albo odświeża istniejący klon.
# Adres demo://Nazwa tworzy lokalne repo demonstracyjne (bez sieci) - do pracy bez dostępu do TFS.
$ErrorActionPreference = 'Stop'

$name = $env:FLOWHUB_REPO_NAME
$url = $env:FLOWHUB_REPO_URL
$path = $env:FLOWHUB_REPO_PATH
$defaultBranch = $env:FLOWHUB_DEFAULT_BRANCH

function Invoke-Git {
    # git pisze postęp na stderr - przy 'Stop' PowerShell 5.1 uznałby to za błąd.
    $ErrorActionPreference = 'Continue'
    & git @args 2>&1 | ForEach-Object { "$_" }
    if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') zakończył się kodem $LASTEXITCODE" }
}

function New-DemoRepository {
    New-Item -ItemType Directory -Force -Path (Join-Path $path 'src') | Out-Null
    Set-Content -Path (Join-Path $path 'README.md') -Encoding utf8 -Value "# $name`n`nRepo demonstracyjne FlowHub."
    Set-Content -Path (Join-Path $path 'src/Program.cs') -Encoding utf8 -Value "Console.WriteLine(`"$name`");"

    Invoke-Git -C $path init --quiet --initial-branch $defaultBranch
    Invoke-Git -C $path add --all
    Invoke-Git -C $path -c user.name=FlowHub -c user.email=flowhub@localhost commit --quiet -m 'Repo demonstracyjne'
}

if (Test-Path (Join-Path $path '.git')) {
    if ($url -like 'demo://*') {
        Write-Output "Repo demonstracyjne $name już istnieje."
    }
    else {
        Write-Output "Odświeżam $name..."
        Invoke-Git -C $path fetch --prune
    }
}
elseif ($url -like 'demo://*') {
    Write-Output "Tworzę repo demonstracyjne $name..."
    New-DemoRepository
}
else {
    Write-Output "Klonuję $url..."
    Invoke-Git clone --branch $defaultBranch $url $path
}

Write-Output "Repo $name gotowe: $path"
