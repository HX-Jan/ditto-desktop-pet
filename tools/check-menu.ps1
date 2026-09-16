param([Parameter(Mandatory)][int]$PetProcessId, [Parameter(Mandatory)][string]$Executable)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
. (Join-Path $PSScriptRoot 'inspect-desktop.ps1') -PetProcessId $PetProcessId
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class DittoMouseProbe {
  [StructLayout(LayoutKind.Sequential)] public struct Point { public int X,Y; }
  [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
  [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out Rect rect);
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point point);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags,uint dx,uint dy,uint data, UIntPtr extra);
}
'@
[DittoMouseProbe]::SetThreadDpiAwarenessContext([IntPtr](-4)) | Out-Null
$petLine = [DittoWindowProbe]::Windows([uint32]$PetProcessId) | Where-Object { $_ -match '百变怪桌宠$' } | Select-Object -First 1
$petHandle = [IntPtr]([long]($petLine.Split('|')[0].Trim()))
$originalCursor = New-Object DittoMouseProbe+Point
[DittoMouseProbe]::GetCursorPos([ref]$originalCursor) | Out-Null
try {
    $rect = New-Object DittoMouseProbe+Rect
    [DittoMouseProbe]::GetWindowRect($petHandle,[ref]$rect) | Out-Null
    [DittoMouseProbe]::SetCursorPos([int](($rect.Left+$rect.Right)/2),[int]($rect.Top+($rect.Bottom-$rect.Top)*0.8)) | Out-Null
    [DittoMouseProbe]::mouse_event(0x08,0,0,0,[UIntPtr]::Zero)
    [DittoMouseProbe]::mouse_event(0x10,0,0,0,[UIntPtr]::Zero)
    Start-Sleep -Milliseconds 300
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$PetProcessId)
    $elements = [System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Descendants,$condition)
    $menuNames = @($elements | ForEach-Object { $_.Current.Name })
    $hide = $elements | Where-Object { $_.Current.Name -eq '隐藏到托盘' } | Select-Object -First 1
    if (-not $hide) { throw ('Hide menu unavailable. Elements: ' + ($menuNames -join ', ')) }
    $invoke = $hide.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $invoke.Invoke()
    Start-Sleep -Milliseconds 150
    $hidden = [bool]([DittoWindowProbe]::Windows([uint32]$PetProcessId) | Where-Object { $_ -match 'False.*百变怪桌宠$' })
    $duplicate = Start-Process -FilePath $Executable -WindowStyle Hidden -PassThru
    $duplicateExited = $duplicate.WaitForExit(5000)
    Start-Sleep -Milliseconds 300
    $restored = [bool]([DittoWindowProbe]::Windows([uint32]$PetProcessId) | Where-Object { $_ -match 'True.*百变怪桌宠$' })
    @{ menuNames=$menuNames; hiddenViaRealMenu=$hidden; duplicateExited=$duplicateExited; hiddenInstanceRestored=$restored } | ConvertTo-Json | Tee-Object artifacts/menu-test.json
    if(-not ($hidden -and $duplicateExited -and $restored)) { throw 'Menu / single instance check failed.' }
} finally {
    [DittoMouseProbe]::SetCursorPos($originalCursor.X,$originalCursor.Y) | Out-Null
    [DittoWindowProbe]::PostMessage($petHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null
}
