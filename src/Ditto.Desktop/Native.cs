using System;
using System.Runtime.InteropServices;
using System.Text;
using Ditto.Core;

namespace Ditto.Desktop;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct MonitorInfo
    { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern IntPtr MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] internal static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] internal static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] internal static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern bool IsZoomed(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern int GetClassName(IntPtr hwnd, StringBuilder name, int count);
    internal static void SetPetStyles(IntPtr hwnd, bool transparent=false)
    {
        long style=GetWindowLongPtr(hwnd,-20).ToInt64()|0x08000000L|0x80L;
        if(transparent) style|=0x20L;
        SetWindowLongPtr(hwnd,-20,new IntPtr(style));
    }
    internal static bool IsPrimaryFullscreen(IntPtr foreground, bool allowOwnProcess=false)
    {
        if(foreground==IntPtr.Zero || !IsWindowVisible(foreground) || IsZoomed(foreground)) return false;
        GetWindowThreadProcessId(foreground,out uint pid);
        if(!allowOwnProcess && pid==Environment.ProcessId) return false;
        var name=new StringBuilder(128); GetClassName(foreground,name,128);
        if(name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;
        var info=new MonitorInfo { Size=Marshal.SizeOf<MonitorInfo>() };
        if(!GetMonitorInfo(MonitorFromPoint(new Point(),1),ref info) || !GetWindowRect(foreground,out var r)) return false;
        var m=info.Monitor;
        return Math.Abs(r.Left-m.Left)<=2 && Math.Abs(r.Top-m.Top)<=2 && Math.Abs(r.Right-m.Right)<=2 && Math.Abs(r.Bottom-m.Bottom)<=2;
    }
    internal static WorkArea PrimaryArea(double scale)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromPoint(new Point(), 1), ref info))
            throw new InvalidOperationException("无法读取主显示器工作区。");
        return new(info.Work.Left / scale, info.Work.Top / scale,
            (info.Work.Right - info.Work.Left) / scale, (info.Work.Bottom - info.Work.Top) / scale);
    }
}
