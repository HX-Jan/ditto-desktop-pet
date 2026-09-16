using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Ditto.Core;
using Forms = System.Windows.Forms;

namespace Ditto.Desktop;

internal sealed class PetWindow : Window, IDisposable
{
    internal readonly PetEngine Engine = new();
    private readonly Image sprite = new() { Stretch = Stretch.Fill, SnapsToDevicePixels = true };
    private readonly DispatcherTimer timer;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly string settingsPath;
    private readonly Forms.NotifyIcon tray;
    private readonly System.Drawing.Icon trayIcon;
    private readonly Forms.ContextMenuStrip trayMenu = new();
    private bool disposed;
    private bool menuOpen;
    private double lastTime;
    private HwndSource? source;
    internal IntPtr Handle { get; private set; }
    internal double Scale => Handle == IntPtr.Zero ? 1 : Native.GetDpiForWindow(Handle) / 96.0;
    internal bool TimerRunning => timer.IsEnabled;

    internal PetWindow(bool test)
    {
        settingsPath = test ? Path.Combine(Path.GetTempPath(),"DittoDesktopPet-tests",Environment.ProcessId.ToString(),"settings.json")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DittoDesktopPet","settings.json");
        var settings=Settings.Load(settingsPath);
        Title="百变怪桌宠";
        Width=Height=settings.Size;
        Left=settings.X; Top=0;
        AllowsTransparency=true; WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize;
        Background=Brushes.Transparent; Topmost=true; ShowInTaskbar=false; ShowActivated=false; Focusable=false;
        UseLayoutRounding=true;
        RenderOptions.SetBitmapScalingMode(sprite,BitmapScalingMode.NearestNeighbor);
        Content=sprite;
        Icon=Sprites.Get(PetState.Idle,0);
        using var iconStream=Application.GetResourceStream(new Uri("pack://application:,,,/Assets/ditto.ico"))!.Stream;
        using var original=new System.Drawing.Icon(iconStream);
        trayIcon=(System.Drawing.Icon)original.Clone();
        tray = new Forms.NotifyIcon { Icon=trayIcon, Text="百变怪桌宠 · 右键打开菜单", Visible=true, ContextMenuStrip=trayMenu };
        tray.DoubleClick += (_,_) => Dispatcher.Invoke(Reveal);
        trayMenu.Opening += (_,_) => { menuOpen=true; BuildTrayMenu(); };
        trayMenu.Closed += (_,_) => { menuOpen=false; ResetClock(); };
        timer=new DispatcherTimer(DispatcherPriority.Render) { Interval=TimeSpan.FromMilliseconds(1000.0/30) };
        timer.Tick += (_,_) =>
        {
            double now=clock.Elapsed.TotalSeconds;
            if(!menuOpen) Engine.Tick(now-lastTime);
            lastTime=now; Sync();
        };
        SourceInitialized += (_,_) =>
        {
            Handle=new WindowInteropHelper(this).Handle;
            source=HwndSource.FromHwnd(Handle); source.AddHook(WndProc);
            long style=Native.GetWindowLongPtr(Handle,-20).ToInt64();
            Native.SetWindowLongPtr(Handle,-20,new IntPtr(style|0x08000000L|0x00000080L));
            Engine.Configure(Native.PrimaryArea(Scale),settings.Size,settings.X); Sync();
            ResetClock(); timer.Start();
        };
        MouseLeftButtonDown += (_,e) =>
        {
            var p=GetCursorPoint(); Engine.PointerDown(p.X,p.Y); CaptureMouse(); e.Handled=true;
        };
        MouseMove += (_,_) =>
        {
            if(!Engine.PointerHeld) return;
            var p=GetCursorPoint(); Engine.PointerMove(p.X,p.Y,SystemParameters.MinimumHorizontalDragDistance,SystemParameters.MinimumVerticalDragDistance); Sync();
        };
        MouseLeftButtonUp += (_,e) => { Engine.PointerUp(); ReleaseMouseCapture(); Sync(); Save(); e.Handled=true; };
        LostMouseCapture += (_,_) => { Engine.CancelPointer(); Sync(); };
        MouseRightButtonUp += (_,e) => { OpenMenu(); e.Handled=true; };
        Closing += (_,_) => { Save(); Application.Current.Shutdown(); };
    }
    private Point GetCursorPoint() { Native.GetCursorPos(out var p); return new(p.X/Scale,p.Y/Scale); }
    private IntPtr WndProc(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if(msg==0x0021) { handled=true; return new IntPtr(3); } // MA_NOACTIVATE: preserve typing focus.
        if(msg is 0x007E or 0x001A or 0x02E0)
            Dispatcher.BeginInvoke(RefreshArea,DispatcherPriority.Loaded);
        return IntPtr.Zero;
    }
    internal void RefreshArea()
    {
        if(disposed) return;
        Engine.CancelPointer(); ReleaseMouseCapture();
        Engine.Configure(Native.PrimaryArea(Scale),Engine.Size); Sync(); Save();
    }
    internal void SetSize(int size) { Engine.Configure(Native.PrimaryArea(Scale),size); Sync(); Save(); }
    internal void TogglePaused() { Engine.SetPaused(!Engine.Paused); ResetClock(); Sync(); }
    internal void ToggleSleep() { Engine.SetSleeping(!Engine.Sleeping); ResetClock(); Sync(); }
    internal void Conceal() { Engine.CancelPointer(); ReleaseMouseCapture(); Engine.Hidden=true; timer.Stop(); Hide(); Save(); }
    internal void Reveal() { Engine.Hidden=false; Show(); Sync(); ResetClock(); timer.Start(); }
    internal void Sync()
    {
        Width=Height=Engine.Size;
        Left=Engine.X; Top=Engine.Y;
        sprite.Source=Sprites.Get(Engine.State,Engine.AnimationTime,Engine.FacingLeft);
    }
    private void ResetClock() => lastTime=clock.Elapsed.TotalSeconds;
    private void Save() => new Settings(Size:(int)Engine.Size,X:Engine.X).Save(settingsPath);
    private void OpenMenu()
    {
        var menu=new ContextMenu { FontSize=14 };
        menu.Items.Add(new MenuItem { Header="百变怪桌宠 · v0.1.0", IsEnabled=false });
        menu.Items.Add(new Separator());
        var sizes=new MenuItem { Header="大小" };
        foreach(var (name,size) in new[]{("小 · 96",96),("中 · 128",128),("大 · 160",160)})
        {
            var item=new MenuItem { Header=name, IsCheckable=true, IsChecked=Engine.Size==size };
            item.Click+=(_,_)=>SetSize(size); sizes.Items.Add(item);
        }
        menu.Items.Add(sizes);
        Add(menu,Engine.Paused?"继续活动":"暂停活动",TogglePaused);
        Add(menu,Engine.Sleeping?"唤醒":"睡一会儿",ToggleSleep);
        Add(menu,"隐藏到托盘",Conceal);
        menu.Items.Add(new Separator()); Add(menu,"退出",Close);
        menu.Opened+=(_,_)=>menuOpen=true;
        menu.Closed+=(_,_)=>{menuOpen=false; ResetClock();};
        menu.PlacementTarget=this; menu.IsOpen=true;
    }
    private static void Add(ContextMenu menu,string text,Action action)
    { var item=new MenuItem { Header=text }; item.Click+=(_,_)=>action(); menu.Items.Add(item); }
    private void BuildTrayMenu()
    {
        while(trayMenu.Items.Count>0) { var old=trayMenu.Items[0]; trayMenu.Items.RemoveAt(0); old.Dispose(); }
        trayMenu.Items.Add("百变怪桌宠 · v0.1.0").Enabled=false;
        trayMenu.Items.Add(new Forms.ToolStripSeparator());
        var sizes=new Forms.ToolStripMenuItem("大小");
        foreach(var (name,size) in new[]{("小 · 96",96),("中 · 128",128),("大 · 160",160)})
        {
            var item=new Forms.ToolStripMenuItem(name) { Checked=Engine.Size==size };
            item.Click+=(_,_)=>SetSize(size); sizes.DropDownItems.Add(item);
        }
        trayMenu.Items.Add(sizes);
        trayMenu.Items.Add(Engine.Paused?"继续活动":"暂停活动",null,(_,_)=>TogglePaused());
        trayMenu.Items.Add(Engine.Sleeping?"唤醒":"睡一会儿",null,(_,_)=>ToggleSleep());
        trayMenu.Items.Add(Engine.Hidden?"显示桌宠":"隐藏到托盘",null,(_,_)=>{if(Engine.Hidden) Reveal(); else Conceal();});
        trayMenu.Items.Add(new Forms.ToolStripSeparator());
        trayMenu.Items.Add("退出",null,(_,_)=>Close());
    }
    public void Dispose()
    {
        if(disposed) return; disposed=true;
        timer.Stop(); source?.RemoveHook(WndProc);
        tray.Visible=false; tray.Dispose(); trayMenu.Dispose(); trayIcon.Dispose();
    }
}
