<#
.SYNOPSIS
    Prepares a clean feature branch workspace for a new implementation task.

.PARAMETER BranchName
    Name of the feature branch to create or switch to.

.PARAMETER BaseBranch
    Branch to update before creating the feature branch. Defaults to master.

.PARAMETER SkipPull
    Skip pulling the latest changes from origin/BaseBranch.
#>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateNotNullOrEmpty()]
    [string]$BranchName,

    [string]$BaseBranch = 'master',

    [switch]$SkipPull
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

Invoke-Step 'Checking for uncommitted changes' {
    $status = git status --porcelain
    if ($status) {
        throw "Working tree has uncommitted changes. Commit or stash them before starting new work.`n$status"
    }
}

Invoke-Step "Switching to $BaseBranch" {
    git switch $BaseBranch
}

if (-not $SkipPull) {
    Invoke-Step "Pulling latest origin/$BaseBranch" {
        git pull origin $BaseBranch
    }
}

Invoke-Step "Creating or switching to $BranchName" {
    $existingBranch = git branch --list $BranchName
    if ($existingBranch) {
        git switch $BranchName
    }
    else {
        git switch -c $BranchName
    }
}

Invoke-Step 'Restoring .NET packages' {
    dotnet restore $Solution
}

Write-Host 'Workspace is ready for implementation.' -ForegroundColor Green
