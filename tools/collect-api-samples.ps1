# Zbiera wersje i przykładowe odpowiedzi API z TFS, TeamCity, Octopus i Jiry — TYLKO ODCZYT (same zapytania GET).
# Wynik: pliki JSON w katalogu api-samples/ (jest w .gitignore). Tokeny wpisujesz w oknie, nie trafiają do plików.
#
# Użycie (uzupełnij tylko to, co masz; pominięte systemy są pomijane):
#   ./tools/collect-api-samples.ps1 `
#       -TfsCollectionUrl https://tfs.firma.pl/tfs/DefaultCollection -TfsProject Projekt -TfsRepo Billing.Api `
#       -TeamCityUrl https://teamcity.firma.pl -TeamCityBuildType Billing_Api_Build `
#       -OctopusUrl https://octopus.firma.pl -OctopusProject billing-api `
#       -JiraUrl https://jira.firma.pl -JiraIssue PROJ-123 `
#       -Branch feature/PROJ-123-opis
#
# PRZED WKLEJENIEM PLIKÓW GDZIEKOLWIEK: przejrzyj je i usuń dane, których nie wolno wynosić z firmy
# (nazwiska, adresy, treść zadań). Do dopasowania klienta wystarczą nazwy pól i przykładowe wartości.
param(
    [string]$TfsCollectionUrl, [string]$TfsProject, [string]$TfsRepo, [string]$TfsApiVersion = '6.0',
    [string]$TeamCityUrl, [string]$TeamCityBuildType,
    [string]$OctopusUrl, [string]$OctopusProject,
    [string]$JiraUrl, [string]$JiraIssue, [string]$JiraEmail,
    [string]$Branch = 'main',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\api-samples')
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

function Read-Token([string]$label) {
    $secure = Read-Host -Prompt "$label (puste = pomiń)" -AsSecureString
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}

function Get-Basic([string]$user, [string]$password) {
    return 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("${user}:${password}"))
}

# Jedno zapytanie = jeden plik. Błąd (np. 404) też zapisujemy — to również informacja o serwerze.
function Save-Sample([string]$name, [string]$url, [hashtable]$headers) {
    $file = Join-Path $OutputDirectory "$name.json"
    try {
        $headers['Accept'] = 'application/json'
        $response = Invoke-WebRequest -Uri $url -Headers $headers -UseBasicParsing -TimeoutSec 30
        [IO.File]::WriteAllText($file, $response.Content, (New-Object Text.UTF8Encoding $false))
        Write-Host "OK    $name"
        return ($response.Content | ConvertFrom-Json)
    }
    catch {
        $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 'brak odpowiedzi' }
        [IO.File]::WriteAllText($file, (@{ error = $_.Exception.Message; status = "$status"; url = $url } | ConvertTo-Json), (New-Object Text.UTF8Encoding $false))
        Write-Host "BŁĄD  $name ($status)"
        return $null
    }
}

$branchRef = [Uri]::EscapeDataString("refs/heads/$Branch")

if ($TfsCollectionUrl) {
    $token = Read-Token 'TFS: Personal Access Token'
    if ($token) {
        $headers = @{ Authorization = (Get-Basic '' $token) }
        $base = "$($TfsCollectionUrl.TrimEnd('/'))/$([Uri]::EscapeDataString($TfsProject))/_apis"
        Save-Sample 'tfs-repositories' "$base/git/repositories?api-version=$TfsApiVersion" $headers | Out-Null
        if ($TfsRepo) {
            $pullRequests = Save-Sample 'tfs-pullrequests' "$base/git/repositories/$TfsRepo/pullrequests?searchCriteria.sourceRefName=$branchRef&searchCriteria.status=all&api-version=$TfsApiVersion" $headers
            $pullRequest = $pullRequests.value | Select-Object -First 1
            if ($pullRequest) {
                $id = $pullRequest.pullRequestId
                Save-Sample 'tfs-threads' "$base/git/repositories/$TfsRepo/pullRequests/$id/threads?api-version=$TfsApiVersion" $headers | Out-Null
                $artifact = [Uri]::EscapeDataString("vstfs:///CodeReview/CodeReviewId/$($pullRequest.repository.project.id)/$id")
                Save-Sample 'tfs-policies' "$base/policy/evaluations?artifactId=$artifact&api-version=6.0-preview.1" $headers | Out-Null
            }
        }
    }
}

if ($TeamCityUrl) {
    $token = Read-Token 'TeamCity: token dostępu'
    if ($token) {
        $headers = @{ Authorization = "Bearer $token" }
        $base = "$($TeamCityUrl.TrimEnd('/'))/app/rest"
        Save-Sample 'teamcity-server' "$base/server" $headers | Out-Null
        if ($TeamCityBuildType) {
            $builds = Save-Sample 'teamcity-builds' "$base/builds?locator=buildType:(id:$TeamCityBuildType),branch:(name:$Branch),defaultFilter:false,count:3" $headers
            Save-Sample 'teamcity-changes' "$base/changes?locator=buildType:(id:$TeamCityBuildType),branch:(name:$Branch),count:5" $headers | Out-Null
            $build = $builds.build | Select-Object -First 1
            if ($build) {
                Save-Sample 'teamcity-build' "$base/builds/id:$($build.id)" $headers | Out-Null
                Save-Sample 'teamcity-failed-tests' "$base/testOccurrences?locator=build:(id:$($build.id)),status:FAILURE,count:20" $headers | Out-Null
            }
        }
    }
}

if ($OctopusUrl) {
    $token = Read-Token 'Octopus: klucz API'
    if ($token) {
        $headers = @{ 'X-Octopus-ApiKey' = $token }
        $base = "$($OctopusUrl.TrimEnd('/'))/api"
        Save-Sample 'octopus-root' $base $headers | Out-Null
        Save-Sample 'octopus-environments' "$base/environments?take=30" $headers | Out-Null
        if ($OctopusProject) {
            $project = Save-Sample 'octopus-project' "$base/projects/$OctopusProject" $headers
            if ($project.Id) {
                $releases = Save-Sample 'octopus-releases' "$base/projects/$($project.Id)/releases?take=3" $headers
                Save-Sample 'octopus-dashboard' "$base/dashboard/dynamic?projects=$($project.Id)" $headers | Out-Null
                $release = $releases.Items | Select-Object -First 1
                if ($release) {
                    Save-Sample 'octopus-deployments' "$base/releases/$($release.Id)/deployments?take=10" $headers | Out-Null
                }
            }
        }
    }
}

if ($JiraUrl) {
    $token = Read-Token 'Jira: token'
    if ($token) {
        $authorization = if ($JiraEmail) { Get-Basic $JiraEmail $token } else { "Bearer $token" }
        $headers = @{ Authorization = $authorization }
        $base = "$($JiraUrl.TrimEnd('/'))/rest/api/2"
        Save-Sample 'jira-serverinfo' "$base/serverInfo" $headers | Out-Null
        if ($JiraIssue) {
            Save-Sample 'jira-issue' "$base/issue/$JiraIssue`?fields=summary,description" $headers | Out-Null
        }
    }
}

Write-Host ""
Write-Host "Gotowe. Pliki: $((Resolve-Path $OutputDirectory).Path)"
Write-Host "Przejrzyj je i usuń dane, których nie wolno wynosić z firmy, zanim je komukolwiek pokażesz."
