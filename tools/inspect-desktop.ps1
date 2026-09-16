param([int]$PetProcessId)
$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class DittoWindowProbe {
  public delegate bool Callback(IntPtr h, IntPtr data);
  [DllImport("user32.dll")] static extern bool EnumWindows(Callback cb, IntPtr data);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder text, int count);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  public static string[] Windows(uint wanted) {
    var result = new List<string>();
    EnumWindows((h,data) => {
      uint pid; GetWindowThreadProcessId(h, out pid);
      if(pid == wanted) {
        var text = new StringBuilder(512); GetWindowText(h,text,512);
        result.Add(h.ToInt64()+" | "+IsWindowVisible(h)+" | "+text);
      }
      return true;
    },IntPtr.Zero);
    return result.ToArray();
  }
}
'@
[DittoWindowProbe]::Windows([uint32]$PetProcessId)
$petProcess = Get-Process -Id $PetProcessId
$petProcess.Modules | Where-Object { $_.ModuleName -in 'coreclr.dll','hostfxr.dll','hostpolicy.dll','PresentationNative_cor3.dll' } | Select-Object ModuleName,FileName
