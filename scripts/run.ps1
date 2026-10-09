# Builds and starts the development build. -LocalConfig keeps your real categories untouched by using
# .local\dev\config.json; -Fresh deletes that file first so the first-run wizard appears again.
param([switch]$LocalConfig, [switch]$Fresh, [switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$executable = Join-Path $projectRoot 'src\CategoryBar\bin\Release\net10.0-windows\CategoryBar.exe'
Get-Process CategoryBar -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $executable } | Stop-Process -Force
Start-Sleep -Milliseconds 300
if (-not $NoBuild) {
    dotnet build (Join-Path $projectRoot 'WindowsCategoryBar.sln') -c Release -nologo -v q | Select-String -Pattern 'error|warn' | Sort-Object -Unique
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
if ($LocalConfig -or $Fresh) {
    $folder = Join-Path $projectRoot '.local\dev'
    if ($Fresh -and (Test-Path -LiteralPath $folder)) { Remove-Item -LiteralPath $folder -Recurse -Force }
    Start-Process -FilePath $executable -ArgumentList @('--config', ('"' + (Join-Path $folder 'config.json') + '"'))
} else {
    Start-Process -FilePath $executable
}
