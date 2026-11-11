# Tworzy branch zadania (ta sama nazwa we wszystkich repo) albo przełącza się na istniejący.
$ErrorActionPreference = 'Continue'

$path = $env:FLOWHUB_REPO_PATH
$branch = $env:FLOWHUB_BRANCH

& git -C $path rev-parse --verify --quiet "refs/heads/$branch" 2>&1 | Out-Null
$exists = $LASTEXITCODE -eq 0

if ($exists) {
    & git -C $path checkout --quiet $branch 2>&1 | ForEach-Object { "$_" }
}
else {
    & git -C $path checkout --quiet -b $branch 2>&1 | ForEach-Object { "$_" }
}

if ($LASTEXITCODE -ne 0) {
    Write-Output "Nie udało się przełączyć na branch $branch"
    exit 1
}

Write-Output "Repo $($env:FLOWHUB_REPO_NAME): branch $branch"
