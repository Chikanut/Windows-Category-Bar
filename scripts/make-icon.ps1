# Builds Assets\App.ico from the vector source Assets\App.svg.
# The SVG is rasterized once at 1024 px by a headless Chromium browser (Edge or Chrome) with a
# transparent background, then scaled down to every size an .ico needs.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assets = Join-Path (Split-Path -Parent $PSScriptRoot) 'src\CategoryBar\Assets'
$svg = Join-Path $assets 'App.svg'
$target = Join-Path $assets 'App.ico'

$browser = @(
    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe"
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $browser) { throw 'Edge or Chrome is required to rasterize the SVG.' }

$work = Join-Path ([System.IO.Path]::GetTempPath()) ('CategoryBarIcon-' + [guid]::NewGuid())
New-Item -ItemType Directory -Force $work | Out-Null
try {
    $page = Join-Path $work 'icon.html'
    $png = Join-Path $work 'icon.png'
    $source = ([uri]$svg).AbsoluteUri
    Set-Content -LiteralPath $page -Encoding utf8 -Value "<html><body style='margin:0;background:transparent;overflow:hidden'><img src='$source' style='display:block;width:1024px;height:1024px'></body></html>"
    $arguments = @('--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run', '--default-background-color=00000000',
        '--window-size=1024,1024', "--user-data-dir=`"$work\profile`"", "--screenshot=`"$png`"", "`"$(([uri]$page).AbsoluteUri)`"")
    $process = Start-Process -FilePath $browser -ArgumentList $arguments -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'The browser did not finish rendering.' }
    if (-not (Test-Path -LiteralPath $png)) { throw 'The browser produced no image.' }

    $master = [System.Drawing.Image]::FromFile($png)
    $sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
    $images = foreach ($size in $sizes) {
        $bitmap = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.InterpolationMode = 'HighQualityBicubic'; $graphics.PixelOffsetMode = 'HighQuality'; $graphics.CompositingQuality = 'HighQuality'
        $graphics.DrawImage($master, 0, 0, $size, $size)
        $graphics.Dispose()
        $stream = New-Object System.IO.MemoryStream
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $bitmap.Dispose()
        , $stream.ToArray()
    }
    $master.Dispose()

    $out = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter $out
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($image in $images) { $writer.Write([byte[]]$image) }
    [System.IO.File]::WriteAllBytes($target, $out.ToArray())
    Copy-Item -LiteralPath $png -Destination (Join-Path $work '..\CategoryBarIcon-preview.png') -Force
    Write-Output "Written $target ($($out.Length) bytes) from $svg"
} finally {
    Start-Sleep -Milliseconds 300
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
