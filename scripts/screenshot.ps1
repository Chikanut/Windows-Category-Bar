# Captures the primary screen (or a region of it) to a PNG. Used to review the interface during development.
param([string]$Path = "$PSScriptRoot\..\.local\shots\screen.png", [int]$X = 0, [int]$Y = 0, [int]$Width = 0, [int]$Height = 0)
Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type @'
using System.Runtime.InteropServices;
public static class Dpi { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); }
'@
[Dpi]::SetProcessDPIAware() | Out-Null
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
if ($Width -le 0) { $Width = $bounds.Width - $X }
if ($Height -le 0) { $Height = $bounds.Height - $Y }
$bitmap = New-Object System.Drawing.Bitmap $Width, $Height
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($bounds.X + $X, $bounds.Y + $Y, 0, 0, $bitmap.Size)
New-Item -ItemType Directory -Force (Split-Path -Parent $Path) | Out-Null
$bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose(); $bitmap.Dispose()
Write-Output "$Path $($bounds.Width)x$($bounds.Height)"
