param([switch]$Publish)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.local\dotnet'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
    $env:MSBuildEnableWorkloadResolver = 'false'
    dotnet build WindowsCategoryBar.sln -c Release -m:1 -p:UseSharedCompilation=false --disable-build-servers
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    dotnet run --project tests\CategoryBar.Tests -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Checks failed.' }
    if ($Publish) {
        dotnet publish src\CategoryBar -c Release --no-restore --self-contained false -o artifacts\portable -p:UseSharedCompilation=false --disable-build-servers
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    }
} finally { Pop-Location }
