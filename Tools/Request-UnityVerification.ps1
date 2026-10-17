param(
    [ValidateSet('music', 'full', 'burst', 'orders', 'items', 'duel')]
    [string]$Suite = 'full',
    [switch]$Wait,
    [int]$TimeoutSeconds = 900
)
$ErrorActionPreference = 'Stop'
$colaProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$colaMailbox = Join-Path $colaProject 'Library/COLA_COLA'
$colaStatus = Join-Path $colaProject 'output/verification/latest/status.json'
$colaRequestPath = Join-Path $colaMailbox 'verification-request.json'
if (Test-Path -LiteralPath $colaStatus) {
    $colaExisting = Get-Content -LiteralPath $colaStatus -Raw | ConvertFrom-Json
    if ($colaExisting.phase -notin @('passed', 'failed')) {
        throw "A verification request is already $($colaExisting.phase). Check $colaStatus"
    }
}
if (Test-Path -LiteralPath $colaRequestPath) { throw 'An unconsumed verification request already exists.' }
New-Item -ItemType Directory -Path $colaMailbox -Force | Out-Null
$colaRequestId = [guid]::NewGuid().ToString('N')
$colaRequestJson = @{ id = $colaRequestId; suite = $Suite } | ConvertTo-Json
[IO.File]::WriteAllText($colaRequestPath, $colaRequestJson, [Text.UTF8Encoding]::new($false))
Write-Output "Queued $Suite ($colaRequestId). The open project Editor imports changes and enters Play; results: $colaStatus"
if (-not $Wait) { return }
$colaDeadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
while ([DateTime]::UtcNow -lt $colaDeadline) {
    if (Test-Path -LiteralPath $colaStatus) {
        try { $colaCurrent = Get-Content -LiteralPath $colaStatus -Raw | ConvertFrom-Json } catch { Start-Sleep -Milliseconds 300; continue }
        if ($colaCurrent.id -eq $colaRequestId) {
            if ($colaCurrent.phase -eq 'passed') { $colaCurrent; return }
            if ($colaCurrent.phase -eq 'failed') { throw $colaCurrent.message }
            if ($colaCurrent.phase -eq 'waiting_for_saved_scenes') { Write-Warning $colaCurrent.message; return }
        }
    }
    Start-Sleep -Milliseconds 500
}
throw 'Timed out waiting for the Editor. Ensure this project is open and has no compile errors; see latest/status.json and verification.log.'
