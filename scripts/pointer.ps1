# Drives the mouse for manual interface checks during development.
# Usage: pointer.ps1 move 100 200 | click 100 200 | rclick 100 200 | dclick 100 200 | drag 100 200 300 400 | key {ESC}
param([Parameter(Mandatory)][string]$Action, [int]$X, [int]$Y, [int]$X2, [int]$Y2, [string]$Keys)
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System.Runtime.InteropServices;
public static class Pointer {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern System.IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(System.IntPtr window, out uint processId);
    public static string ForegroundProcess() { uint id; GetWindowThreadProcessId(GetForegroundWindow(), out id); try { return System.Diagnostics.Process.GetProcessById((int)id).ProcessName; } catch { return ""; } }
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, System.UIntPtr extra);
}
'@
[Pointer]::SetProcessDPIAware() | Out-Null
function Glide([int]$fromX, [int]$fromY, [int]$toX, [int]$toY) {
    for ($i = 1; $i -le 12; $i++) {
        [Pointer]::SetCursorPos($fromX + ($toX - $fromX) * $i / 12, $fromY + ($toY - $fromY) * $i / 12) | Out-Null
        Start-Sleep -Milliseconds 25
    }
}
switch ($Action) {
    'move'   { [Pointer]::SetCursorPos($X, $Y) | Out-Null }
    'click'  { [Pointer]::SetCursorPos($X, $Y) | Out-Null; Start-Sleep -Milliseconds 120; [Pointer]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 40; [Pointer]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero) }
    'dclick' { [Pointer]::SetCursorPos($X, $Y) | Out-Null; Start-Sleep -Milliseconds 120; 1..2 | ForEach-Object { [Pointer]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); [Pointer]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 60 } }
    'rclick' { [Pointer]::SetCursorPos($X, $Y) | Out-Null; Start-Sleep -Milliseconds 120; [Pointer]::mouse_event(8, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 40; [Pointer]::mouse_event(16, 0, 0, 0, [UIntPtr]::Zero) }
    'drag'   { [Pointer]::SetCursorPos($X, $Y) | Out-Null; Start-Sleep -Milliseconds 150; [Pointer]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 120; Glide $X $Y $X2 $Y2; Start-Sleep -Milliseconds 250; [Pointer]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero) }
    'hold'   { [Pointer]::SetCursorPos($X, $Y) | Out-Null; Start-Sleep -Milliseconds 150; [Pointer]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 120; Glide $X $Y $X2 $Y2 }
    'release' { [Pointer]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero) }
    'key'    {
        # Keys go to whatever has the keyboard; refuse to type into anything but the application under test.
        $owner = [Pointer]::ForegroundProcess()
        if ($owner -eq 'CategoryBar') { [System.Windows.Forms.SendKeys]::SendWait($Keys) } else { Write-Output "Keys not sent: foreground is '$owner'." }
    }
}
