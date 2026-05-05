<#
.SYNOPSIS
    Registers the 'fretless' command in your PowerShell profile.

.DESCRIPTION
    Appends a function alias to $PROFILE so that typing 'fretless'
    from any directory launches the Fretless web server.
    Run once; safe to re-run (idempotent).

.NOTES
    Because script execution may be disabled on the machine, run this
    installer by piping it through powershell.exe -ExecutionPolicy Bypass:

        powershell.exe -ExecutionPolicy Bypass -File "C:\Users\tpeas\source\repos\Fretless\Install-FretlessCommand.ps1"
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Resolve the repo root whether the script is called with .\ or a full path.
$RepoRoot   = if ($PSScriptRoot) { $PSScriptRoot } else { 'C:\Users\tpeas\source\repos\Fretless' }
$ScriptPath = Join-Path $RepoRoot 'fretless.ps1'

if (-not (Test-Path $ScriptPath)) {
    Write-Error "fretless.ps1 not found at: $ScriptPath"
    exit 1
}

# ── Lift execution policy for the current user ───────────────────────────────
# RemoteSigned allows local scripts to run without being signed.
# Scoped to CurrentUser only. The warning about a more specific override is
# harmless — we catch it and continue regardless.
$currentPolicy = Get-ExecutionPolicy -Scope CurrentUser
if ($currentPolicy -in 'Restricted', 'AllSigned', 'Undefined') {
    Write-Host "  Lifting execution policy: $currentPolicy → RemoteSigned (CurrentUser)" -ForegroundColor Cyan
    try {
        Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser -Force -ErrorAction SilentlyContinue
    } catch { <# override warning from a broader scope — safe to ignore #> }
    Write-Host "  Done." -ForegroundColor Green
} else {
    Write-Host "  Execution policy already allows scripts: $currentPolicy" -ForegroundColor DarkGray
}

# ── Ensure profile file exists ───────────────────────────────────────────────
if (-not (Test-Path $PROFILE)) {
    New-Item -ItemType File -Path $PROFILE -Force | Out-Null
    Write-Host "  Created PowerShell profile at: $PROFILE" -ForegroundColor DarkGray
}

# ── Check for existing registration (idempotent) ─────────────────────────────
$ProfileContent = Get-Content $PROFILE -Raw -ErrorAction SilentlyContinue

$Marker = '# >>> fretless command'
if ($ProfileContent -and $ProfileContent.Contains($Marker)) {
    Write-Host "'fretless' is already registered in your PowerShell profile." -ForegroundColor Yellow
    Write-Host "Open a new shell and type: fretless" -ForegroundColor Cyan
    exit 0
}

# ── Append the function ───────────────────────────────────────────────────────
# The function calls powershell.exe with -ExecutionPolicy Bypass so the launch
# script is never blocked, even if policy is tightened later.
$Snippet = @"

$Marker <<<
function fretless {
    param(
        [switch]`$Aspire,
        [switch]`$NoBrowser,
        [ValidateSet('http','https')]
        [string]`$Profile = 'http'
    )
    powershell.exe -ExecutionPolicy Bypass -File '$ScriptPath' ``
        `$(if (`$Aspire)     { '-Aspire' })     ``
        `$(if (`$NoBrowser)  { '-NoBrowser' })  ``
        -Profile `$Profile
}
# <<< fretless command <<<
"@

Add-Content -Path $PROFILE -Value $Snippet

Write-Host ''
Write-Host "  'fretless' registered in your PowerShell profile." -ForegroundColor Green
Write-Host "  $PROFILE" -ForegroundColor Cyan
Write-Host ''
Write-Host '  Open a new PowerShell window (or run: . $PROFILE) then type:' -ForegroundColor DarkGray
Write-Host '    fretless' -ForegroundColor White
Write-Host '    fretless -Aspire' -ForegroundColor White
Write-Host '    fretless -Profile https' -ForegroundColor White
Write-Host '    fretless -NoBrowser' -ForegroundColor White
Write-Host ''
