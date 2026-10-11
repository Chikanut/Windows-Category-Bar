param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$Output,
    [ValidateSet('ps_2_0', 'ps_3_0')][string]$Profile = 'ps_2_0'
)
$ErrorActionPreference = 'Stop'
if (-not ('CategoryBar.ThemeTools.ShaderCompiler' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'ThemeShaderCompiler.cs') }
$sourcePath = (Resolve-Path -LiteralPath $Source).Path
$outputPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Output)
$bytes = [CategoryBar.ThemeTools.ShaderCompiler]::Compile([IO.File]::ReadAllText($sourcePath), $Profile)
[IO.File]::WriteAllBytes($outputPath, $bytes)
Write-Output "Compiled $Profile shader: $outputPath"
