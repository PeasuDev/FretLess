<#
.SYNOPSIS
    Runs the standard local verification checks before human testing or commit.

.PARAMETER SkipFormat
    Skip dotnet format verification.

.PARAMETER SkipTests
    Skip dotnet test.

.PARAMETER Clean
    Remove generated build outputs before restoring and building.
#>
param(
    [switch]$SkipFormat,
    [switch]$SkipTests,

    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Solution = Join-Path $RepoRoot 'Fretless.slnx'

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

Invoke-Step 'Stopping running Fretless.Web processes' {
    Get-Process Fretless.Web -ErrorAction SilentlyContinue | Stop-Process -Force
}

if ($Clean) {
    Invoke-Step 'Removing generated build outputs' {
        Get-ChildItem -Path $RepoRoot -Directory -Recurse -Force -Include bin,obj |
            Remove-Item -Recurse -Force
    }
}

Invoke-Step 'Restoring .NET packages' {
    dotnet restore $Solution
}

if (-not $SkipFormat) {
    Invoke-Step 'Verifying code format' {
        dotnet format $Solution --verify-no-changes --no-restore
    }
}

Invoke-Step 'Building solution' {
    dotnet build $Solution --no-restore
}

if (-not $SkipTests) {
    Invoke-Step 'Running tests' {
        dotnet test $Solution --no-build
    }
}

Write-Host 'Verification completed successfully.' -ForegroundColor Green
