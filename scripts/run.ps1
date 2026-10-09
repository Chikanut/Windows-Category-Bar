param([switch]$LocalConfig)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$executable = Join-Path $projectRoot 'src\CategoryBar\bin\Release\net10.0-windows\CategoryBar.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Run scripts\build.ps1 first.' }
if ($LocalConfig) {
    $configPath = Join-Path $projectRoot '.local\app\config.json'
    Start-Process -FilePath $executable -ArgumentList @('--config', ('"' + $configPath + '"')) -WindowStyle Hidden
} else {
    Start-Process -FilePath $executable -WindowStyle Hidden
}
