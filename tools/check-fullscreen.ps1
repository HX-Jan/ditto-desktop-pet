param([Parameter(Mandatory)][string]$Executable, [string]$OutputPath='artifacts/fullscreen-test.json')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class FullscreenProbe {
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X,Y; }
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
}
'@
[FullscreenProbe]::SetThreadDpiAwarenessContext([IntPtr](-4)) | Out-Null
$pet=Start-Process -FilePath $Executable -WindowStyle Hidden -PassThru
$fixture=New-Object System.Windows.Forms.Form
$savedCursor=New-Object FullscreenProbe+Point
[FullscreenProbe]::GetCursorPos([ref]$savedCursor) | Out-Null
try {
    Start-Sleep -Milliseconds 1200
    if($pet.HasExited) { throw 'Close any existing normal pet before running this test.' }
    . (Join-Path $PSScriptRoot 'inspect-desktop.ps1') -PetProcessId $pet.Id
    $previousFocus=[DittoWindowProbe]::GetForegroundWindow()
    function Pet-Visible {
        return [bool]([DittoWindowProbe]::Windows([uint32]$pet.Id) | Where-Object { $_ -match 'True.*百变怪桌宠$' })
    }
    function Pump([int]$Milliseconds) {
        $watch=[Diagnostics.Stopwatch]::StartNew()
        while($watch.ElapsedMilliseconds -lt $Milliseconds) {
            [System.Windows.Forms.Application]::DoEvents()
            Start-Sleep -Milliseconds 20
        }
    }
    $fixture.Text='Ditto fullscreen validation fixture'
    $fixture.FormBorderStyle='None'
    $fixture.StartPosition='Manual'
    $fixture.Bounds=[System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $fixture.TopMost=$true
    $label=New-Object System.Windows.Forms.Label
    $label.Text='桌宠全屏联动检查 · 即将自动关闭'
    $label.AutoSize=$true
    $label.Location=New-Object System.Drawing.Point(40,40)
    $label.Font=New-Object System.Drawing.Font('Microsoft YaHei',20)
    $fixture.Controls.Add($label)
    $fixture.Show()
    [FullscreenProbe]::SetForegroundWindow($fixture.Handle) | Out-Null
    Pump 150
    # A real click activates our fixture when Windows rejects foreground stealing.
    [FullscreenProbe]::SetCursorPos($fixture.Left+200,$fixture.Top+200) | Out-Null
    [FullscreenProbe]::mouse_event(0x02,0,0,0,[UIntPtr]::Zero)
    [FullscreenProbe]::mouse_event(0x04,0,0,0,[UIntPtr]::Zero)
    Pump 1500
    $focused=[DittoWindowProbe]::GetForegroundWindow() -eq $fixture.Handle
    $hidden= -not (Pet-Visible)
    $fixture.FormBorderStyle='Sizable'
    $fixture.WindowState='Normal'
    $fixture.Bounds=New-Object System.Drawing.Rectangle(80,80,700,420)
    Pump 1200
    $restored=Pet-Visible
    $focusKept=[DittoWindowProbe]::GetForegroundWindow() -eq $fixture.Handle
    $fixture.WindowState='Maximized'
    Pump 1200
    $maximizedVisible=Pet-Visible
    $result=[ordered]@{externalFixtureForeground=$focused; actualFullscreenHidesPet=$hidden; exitingFullscreenRestoresPet=$restored; restorationPreservesFocus=$focusKept; ordinaryMaximizedKeepsPetVisible=$maximizedVisible}
    $result | ConvertTo-Json | Tee-Object -FilePath $OutputPath
    if($result.Values -contains $false) { throw 'Actual fullscreen integration check failed.' }
} finally {
    $fixture.Close(); $fixture.Dispose()
    [FullscreenProbe]::SetCursorPos($savedCursor.X,$savedCursor.Y) | Out-Null
    if($previousFocus) { [FullscreenProbe]::SetForegroundWindow($previousFocus) | Out-Null }
    if(-not $pet.HasExited) {
        $line=[DittoWindowProbe]::Windows([uint32]$pet.Id) | Where-Object { $_ -match '百变怪桌宠$' } | Select-Object -First 1
        if($line) { [DittoWindowProbe]::PostMessage([IntPtr]([long]($line.Split('|')[0].Trim())),0x10,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null }
        $pet.WaitForExit(5000) | Out-Null
    }
}
