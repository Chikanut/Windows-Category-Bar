param([switch]$Publish, [switch]$VisualCheck, [switch]$SelfContained)
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
    if ($VisualCheck) {
        $reviewFolder = Join-Path $projectRoot '.local\settings-review'
        $executable = Join-Path $projectRoot 'src\CategoryBar\bin\Release\net10.0-windows\CategoryBar.exe'
        foreach ($settingsPage in @('Panel', 'General', 'Categories', 'About')) {
            $renderProcess = Start-Process -FilePath $executable -ArgumentList @('--render-settings', ('"' + $reviewFolder + '"'), $settingsPage) -WindowStyle Hidden -PassThru
            if (-not $renderProcess.WaitForExit(20000)) { Stop-Process -Id $renderProcess.Id; throw 'Settings render timed out.' }
            if ($renderProcess.ExitCode -ne 0) { throw "Settings checks failed. See $reviewFolder\failure.txt" }
        }
        Write-Output 'PASS: Four settings pages rendered.'
        Get-Content -LiteralPath (Join-Path $reviewFolder 'checks.txt')
    }
    if ($Publish -or $SelfContained) {
        if ($SelfContained) {
            dotnet publish src\CategoryBar -c Release -r win-x64 --self-contained true -o artifacts\portable -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:UseSharedCompilation=false --disable-build-servers
        } else {
            dotnet publish src\CategoryBar -c Release --no-restore --self-contained false -o artifacts\portable -p:UseSharedCompilation=false --disable-build-servers
        }
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    }
} finally { Pop-Location }
