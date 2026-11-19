$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host '.NET 8 SDK is required. Install it from https://dotnet.microsoft.com/download/dotnet/8.0' -ForegroundColor Yellow
    exit 1
}

$sdkList = dotnet --list-sdks
if (-not ($sdkList | Select-String '^8\.')) {
    Write-Host 'The .NET 8 SDK was not found. Install the .NET 8 SDK, then reopen PowerShell.' -ForegroundColor Yellow
    exit 1
}

$project = Join-Path $PSScriptRoot 'Z8PCConnect.Server\Z8PCConnect.Server.csproj'
Write-Host 'Building Z8 PC CONNECT Windows LAN server...' -ForegroundColor Cyan
dotnet restore $project
if ($LASTEXITCODE -ne 0) {
    Write-Host 'Package restore failed. Check that NuGet.org is enabled: dotnet nuget list source' -ForegroundColor Red
    exit $LASTEXITCODE
}

dotnet build $project -c Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host 'Starting server. Pairing code will appear below.' -ForegroundColor Green
dotnet run --project $project -c Release --no-build
