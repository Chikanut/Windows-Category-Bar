param([switch]$SelfContained, [switch]$Publish)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    dotnet build WindowsCategoryBar.sln -c Release -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    dotnet run --project tests\CategoryBar.Tests -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Checks failed.' }
    dotnet run --project tests\CategoryBar.ThemeTests -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Native theme checks failed.' }
    if ($SelfContained) {
        dotnet publish src\CategoryBar -c Release -r win-x64 --self-contained true -o artifacts\portable -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -nologo -v q
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    } elseif ($Publish) {
        dotnet publish src\CategoryBar -c Release --self-contained false -o artifacts\portable -nologo -v q
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    }
} finally { Pop-Location }
