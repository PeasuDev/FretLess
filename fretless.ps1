<#
.SYNOPSIS
    Launches the Fretless local development server.

.PARAMETER Aspire
    Run via the Aspire AppHost instead of the web project directly.

.PARAMETER NoBrowser
    Suppress automatic browser launch.

.PARAMETER Profile
    Launch profile to use: 'http' (default) or 'https'.
#>
param(
    [switch]$Aspire,
    [switch]$NoBrowser,
    [ValidateSet('http','https')]
    [string]$Profile = 'http'
)

$RepoRoot       = $PSScriptRoot
$WebProject     = Join-Path $RepoRoot 'Fretless.Web\Fretless.Web\Fretless.Web.csproj'
$AppHostProject = Join-Path $RepoRoot 'Fretless.AppHost\Fretless.AppHost.csproj'

$env:ASPNETCORE_SUPPRESS_STARTUP_BROWSER = if ($NoBrowser) { '1' } else { $null }

if ($Aspire) {
    dotnet run --project $AppHostProject --launch-profile $Profile
} else {
    dotnet run --project $WebProject --launch-profile $Profile
}
