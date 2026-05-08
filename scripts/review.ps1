<#
.SYNOPSIS
    Builds and launches the Blazor app for human acceptance testing.

.PARAMETER Aspire
    Run via the Aspire AppHost instead of the web project directly.

.PARAMETER NoBrowser
    Suppress automatic browser launch.

.PARAMETER Profile
    Launch profile to use: http or https. Defaults to http.

.PARAMETER SkipVerify
    Skip the verification script before launching.

.PARAMETER Clean
    Remove generated build outputs before verification.
#>
param(
    [switch]$Aspire,
    [switch]$NoBrowser,

    [ValidateSet('http','https')]
    [string]$Profile = 'http',

    [switch]$SkipVerify,

    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$LaunchScript = Join-Path $RepoRoot 'fretless.ps1'
$VerifyScript = Join-Path $PSScriptRoot 'verify.ps1'

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Action
    )

    Write-Host "==> $Message" -ForegroundColor Cyan
    & $Action
}

Set-Location $RepoRoot

if (-not $SkipVerify) {
    Invoke-Step 'Running verification checks' {
        $verifyArguments = @()
        if ($Clean) {
            $verifyArguments += '-Clean'
        }

        & $VerifyScript @verifyArguments
    }
}
else {
    Invoke-Step 'Stopping running Fretless.Web processes' {
        Get-Process Fretless.Web -ErrorAction SilentlyContinue | Stop-Process -Force
    }

    if ($Clean) {
        Invoke-Step 'Removing generated build outputs' {
            Get-ChildItem -Path $RepoRoot -Directory -Recurse -Force -Include bin,obj |
                Remove-Item -Recurse -Force
        }
    }
}

Write-Host ''
Write-Host 'Ready for human testing.' -ForegroundColor Green
Write-Host 'Suggested checks:' -ForegroundColor Yellow
Write-Host '  1. Confirm the app loads without browser console errors.'
Write-Host '  2. Confirm only one Add an Instrument card is visible.'
Write-Host '  3. Select one note and confirm non-selected notes use the darker shade.'
Write-Host '  4. Select multiple notes and confirm non-selected notes use the same darker shade.'
Write-Host ''

$arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $LaunchScript, '-Profile', $Profile)
if ($Aspire) {
    $arguments += '-Aspire'
}
if ($NoBrowser) {
    $arguments += '-NoBrowser'
}

Invoke-Step 'Launching Fretless local development server' {
    & powershell.exe $arguments
}
