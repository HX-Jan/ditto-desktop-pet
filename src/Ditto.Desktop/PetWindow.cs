using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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
    internal static string Version => Assembly.GetExecutingAssembly().GetName().Version!.ToString(3);
    internal readonly PetEngine Engine = new();
    internal readonly VisibilityPolicy VisibilityState = new();
    private readonly Image sprite = new() { Stretch=Stretch.Fill, SnapsToDevicePixels=true };
    private readonly DispatcherTimer timer, fullscreenTimer;
    private readonly Stopwatch clock=Stopwatch.StartNew();
    private readonly string settingsPath;
    private readonly Forms.NotifyIcon tray;
    private readonly System.Drawing.Icon trayIcon;
    private readonly Forms.ContextMenuStrip trayMenu=new();
    internal readonly OverlayWindow BallWindow;
    internal readonly OverlayWindow EmoteWindow;
    private bool disposed;
    private double lastTime;
    private HwndSource? source;
    internal bool DiagnosticControl { get; set; }
    internal Action<double>? DiagnosticFrame { get; set; }
    internal IntPtr Handle { get; private set; }
    internal double Scale => Handle==IntPtr.Zero?1:Native.GetDpiForWindow(Handle)/96.0;
    internal bool TimerRunning => timer.IsEnabled;

    internal PetWindow(bool test)
    {
        settingsPath=test?Path.Combine(Path.GetTempPath(),"DittoDesktopPet-tests",Environment.ProcessId.ToString(),"settings.json")
            :Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DittoDesktopPet","settings.json");
        var settings=Settings.Load(settingsPath);
        Engine.SetActivity(settings.Activity);
        Title="百变怪桌宠"; Width=Height=settings.Size; Left=settings.X; Top=0;
        AllowsTransparency=true; WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize;
        Background=Brushes.Transparent; Topmost=true; ShowInTaskbar=false; ShowActivated=false; Focusable=false;
        UseLayoutRounding=true; RenderOptions.SetBitmapScalingMode(sprite,BitmapScalingMode.NearestNeighbor);
        Content=sprite; Icon=Sprites.Get(PetState.Idle,0);
        BallWindow=new(this,"百变怪的小球",false);
        EmoteWindow=new(this,"百变怪的心情",true);
        using var iconStream=Application.GetResourceStream(new Uri("pack://application:,,,/Assets/ditto.ico"))!.Stream;
        using var original=new System.Drawing.Icon(iconStream);
        trayIcon=(System.Drawing.Icon)original.Clone();
        tray=new Forms.NotifyIcon { Icon=trayIcon, Text="百变怪桌宠 · 右键打开菜单", Visible=true, ContextMenuStrip=trayMenu };
        tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(Reveal);
        trayMenu.Opening+=(_,_)=>{ Engine.MenuOpen=true; BuildTrayMenu(); };
        trayMenu.Closed+=(_,_)=>{ Engine.MenuOpen=false; ResetClock(); };
        timer=new DispatcherTimer(DispatcherPriority.Render) { Interval=TimeSpan.FromMilliseconds(1000.0/30) };
        timer.Tick+=(_,_)=>
        {
            double now=clock.Elapsed.TotalSeconds;
            DiagnosticFrame?.Invoke(now);
            if(!DiagnosticControl) SampleCursor();
            Engine.Tick(now-lastTime); lastTime=now; Sync();
        };
        fullscreenTimer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(500) };
        fullscreenTimer.Tick+=(_,_)=> { if(!DiagnosticControl) ApplyFullscreen(Native.IsPrimaryFullscreen(Native.GetForegroundWindow())); };
        SourceInitialized+=(_,_)=>
        {
            Handle=new WindowInteropHelper(this).Handle; source=HwndSource.FromHwnd(Handle); source.AddHook(WndProc);
            Native.SetPetStyles(Handle);
            Engine.Configure(Native.PrimaryArea(Scale),settings.Size,settings.X);
            Sync(); ResetClock(); timer.Start(); fullscreenTimer.Start();
        };
        MouseLeftButtonDown+=(_,e)=>
        { var p=CursorPoint(); Engine.PointerDown(p.X,p.Y); CaptureMouse(); e.Handled=true; };
        MouseMove+=(_,_)=>
        {
            if(!Engine.PointerHeld) return;
            var p=CursorPoint(); Engine.PointerMove(p.X,p.Y,SystemParameters.MinimumHorizontalDragDistance,SystemParameters.MinimumVerticalDragDistance); Sync();
        };
        MouseLeftButtonUp+=(_,e)=>{ Engine.PointerUp(); ReleaseMouseCapture(); Sync(); Save(); e.Handled=true; };
        LostMouseCapture+=(_,_)=>{ Engine.CancelPointer(); Sync(); };
        MouseRightButtonUp+=(_,e)=>{ OpenMenu(); e.Handled=true; };
        BallWindow.MouseLeftButtonDown+=(_,e)=>
        { var p=CursorPoint(); Engine.BallPointerDown(p.X,p.Y,clock.Elapsed.TotalSeconds); BallWindow.CaptureMouse(); e.Handled=true; };
        BallWindow.MouseMove+=(_,_)=>
        {
            if(!Engine.Ball.Dragging) return;
            var p=CursorPoint(); Engine.BallPointerMove(p.X,p.Y,clock.Elapsed.TotalSeconds); Sync();
        };
        BallWindow.MouseLeftButtonUp+=(_,e)=>
        { Engine.BallPointerUp(clock.Elapsed.TotalSeconds); BallWindow.ReleaseMouseCapture(); Sync(); e.Handled=true; };
        BallWindow.LostMouseCapture+=(_,_)=>Engine.Ball.CancelDrag();
        BallWindow.MouseRightButtonUp+=(_,e)=>{ OpenMenu(); e.Handled=true; };
        Closing+=(_,_)=>{ Save(); Application.Current.Shutdown(); };
    }
    private Point CursorPoint() { Native.GetCursorPos(out var p); return new(p.X/Scale,p.Y/Scale); }
    private void SampleCursor()
    {
        var p=CursorPoint(); var a=Engine.Area;
        bool inside=p.X>=a.Left && p.X<a.Left+a.Width && p.Y>=a.Top && p.Y<a.Top+a.Height;
        bool buttons=(Native.GetAsyncKeyState(1)&0x8000)!=0 || (Native.GetAsyncKeyState(2)&0x8000)!=0;
        Engine.UpdateCursor(new CursorSnapshot(p.X,p.Y,inside,buttons));
    }
    private IntPtr WndProc(IntPtr hwnd,int msg,IntPtr w,IntPtr l,ref bool handled)
    {
        if(msg==0x21) { handled=true; return new IntPtr(3); }
        if(msg is 0x007E or 0x001A or 0x02E0) Dispatcher.BeginInvoke(RefreshArea,DispatcherPriority.Loaded);
        return IntPtr.Zero;
    }
    internal void RefreshArea()
    {
        if(disposed) return;
        Engine.CancelPointer(); ReleaseMouseCapture(); BallWindow.ReleaseMouseCapture();
        Engine.Configure(Native.PrimaryArea(Scale),Engine.Size); Sync(); Save();
    }
    internal void SetSize(int size) { Engine.Configure(Native.PrimaryArea(Scale),size); Sync(); Save(); }
    internal void SetActivity(ActivityMode activity) { Engine.SetActivity(activity); Sync(); Save(); }
    internal void TogglePaused() { Engine.SetPaused(!Engine.Paused); ResetClock(); Sync(); }
    internal void ToggleSleep() { Engine.SetSleeping(!Engine.Sleeping); ResetClock(); Sync(); }
    internal void StartPlay() { Engine.StartPlay(); Sync(); }
    internal void SpawnBall() { Engine.SpawnBall(); Sync(); }
    internal void EndPlay() { Engine.EndPlay(); Sync(); }
    internal void Conceal()
    {
        Engine.CancelPointer(); ReleaseMouseCapture(); BallWindow.ReleaseMouseCapture();
        Engine.EndPlay(); Engine.Expressions.Clear(); VisibilityState.ManualHidden=true; ApplyVisibility(); Save();
    }
    internal void Reveal()
    {
        VisibilityState.ManualHidden=false;
        if(!DiagnosticControl) VisibilityState.Fullscreen=Native.IsPrimaryFullscreen(Native.GetForegroundWindow());
        ApplyVisibility();
    }
    internal void ApplyFullscreen(bool fullscreen)
    {
        if(VisibilityState.Fullscreen==fullscreen) return;
        VisibilityState.Fullscreen=fullscreen;
        if(fullscreen) { Engine.CancelPointer(); ReleaseMouseCapture(); Engine.Ball.CancelDrag(); BallWindow.ReleaseMouseCapture(); }
        ApplyVisibility();
    }
    private void ApplyVisibility()
    {
        Engine.Hidden=VisibilityState.Hidden;
        if(Engine.Hidden) { timer.Stop(); BallWindow.Hide(); EmoteWindow.Hide(); Hide(); }
        else { if(!IsVisible) Show(); ResetClock(); Sync(); timer.Start(); }
    }
    internal void Sync()
    {
        if(disposed) return;
        Width=Height=Engine.Size; Left=Engine.X; Top=Engine.Y;
        sprite.Source=Sprites.Get(Engine.State,Engine.AnimationTime,Engine.FacingLeft,Engine.Expressions.Emotion,Engine.GazeX,Engine.GazeY);
        if(Engine.Hidden) { BallWindow.Hide(); EmoteWindow.Hide(); return; }
        if(Engine.Ball.Visible)
            BallWindow.Present(Sprites.Ball(),Engine.Ball.X,Engine.Ball.Y,Engine.Ball.Diameter);
        else BallWindow.Hide();
        if(Engine.Expressions.Icon!=Emote.None)
        {
            double size=Engine.Size/3;
            double x=Math.Clamp(Engine.X+Engine.Size*.68,Engine.Area.Left,Math.Max(Engine.Area.Left,Engine.Area.Left+Engine.Area.Width-size));
            double y=Math.Max(Engine.Area.Top,Engine.Y+Engine.Size*.16-size);
            EmoteWindow.Present(Sprites.Icon(Engine.Expressions.Icon),x,y,size);
        }
        else EmoteWindow.Hide();
    }
    private void ResetClock() => lastTime=clock.Elapsed.TotalSeconds;
    private void Save() => new Settings(Size:(int)Engine.Size,X:Engine.X,Activity:Engine.Activity).Save(settingsPath);
    private void OpenMenu()
    {
        var menu=new ContextMenu { FontSize=14 };
        menu.Items.Add(new MenuItem { Header="百变怪桌宠 · v"+Version, IsEnabled=false });
        menu.Items.Add(new Separator());
        var sizes=new MenuItem { Header="大小" };
        foreach(var (name,size) in new[]{("小 · 96",96),("中 · 128",128),("大 · 160",160)})
        {
            var item=new MenuItem { Header=name, IsCheckable=true, IsChecked=Engine.Size==size };
            item.Click+=(_,_)=>SetSize(size); sizes.Items.Add(item);
        }
        menu.Items.Add(sizes);
        foreach(var (name,mode) in new[]{("活泼黏人",ActivityMode.Lively),("安静陪伴",ActivityMode.Quiet)})
        {
            var item=new MenuItem { Header=name, IsCheckable=true, IsChecked=Engine.Activity==mode };
            item.Click+=(_,_)=>SetActivity(mode); menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        Add(menu,"陪我玩",StartPlay); Add(menu,"叫出小球",SpawnBall); Add(menu,"结束玩耍",EndPlay,Engine.Playing);
        menu.Items.Add(new Separator());
        Add(menu,Engine.Paused?"继续活动":"暂停活动",TogglePaused);
        Add(menu,Engine.Sleeping?"唤醒":"睡一会儿",ToggleSleep);
        Add(menu,"隐藏到托盘",Conceal); Add(menu,"退出",Close);
        menu.Opened+=(_,_)=>Engine.MenuOpen=true;
        menu.Closed+=(_,_)=>{ Engine.MenuOpen=false; ResetClock(); };
        menu.PlacementTarget=this; menu.IsOpen=true;
    }
    private static void Add(ContextMenu menu,string text,Action action,bool enabled=true)
    { var item=new MenuItem { Header=text, IsEnabled=enabled }; item.Click+=(_,_)=>action(); menu.Items.Add(item); }
    private void BuildTrayMenu()
    {
        while(trayMenu.Items.Count>0) { var old=trayMenu.Items[0]; trayMenu.Items.RemoveAt(0); old.Dispose(); }
        trayMenu.Items.Add("百变怪桌宠 · v"+Version).Enabled=false;
        var sizes=new Forms.ToolStripMenuItem("大小");
        foreach(var (name,size) in new[]{("小 · 96",96),("中 · 128",128),("大 · 160",160)})
        {
            var item=new Forms.ToolStripMenuItem(name) { Checked=Engine.Size==size };
            item.Click+=(_,_)=>SetSize(size); sizes.DropDownItems.Add(item);
        }
        trayMenu.Items.Add(sizes);
        foreach(var (name,mode) in new[]{("活泼黏人",ActivityMode.Lively),("安静陪伴",ActivityMode.Quiet)})
        {
            var item=new Forms.ToolStripMenuItem(name) { Checked=Engine.Activity==mode };
            item.Click+=(_,_)=>SetActivity(mode); trayMenu.Items.Add(item);
        }
        trayMenu.Items.Add(new Forms.ToolStripSeparator());
        trayMenu.Items.Add("陪我玩",null,(_,_)=>StartPlay()).Enabled=!Engine.Hidden;
        trayMenu.Items.Add("叫出小球",null,(_,_)=>SpawnBall()).Enabled=!Engine.Hidden;
        trayMenu.Items.Add("结束玩耍",null,(_,_)=>EndPlay()).Enabled=Engine.Playing;
        trayMenu.Items.Add(new Forms.ToolStripSeparator());
        trayMenu.Items.Add(Engine.Paused?"继续活动":"暂停活动",null,(_,_)=>TogglePaused());
        trayMenu.Items.Add(Engine.Sleeping?"唤醒":"睡一会儿",null,(_,_)=>ToggleSleep());
        trayMenu.Items.Add(VisibilityState.ManualHidden?"显示桌宠":"隐藏到托盘",null,(_,_)=>{ if(VisibilityState.ManualHidden) Reveal(); else Conceal(); });
        trayMenu.Items.Add(new Forms.ToolStripSeparator()); trayMenu.Items.Add("退出",null,(_,_)=>Close());
    }
    public void Dispose()
    {
        if(disposed) return; disposed=true;
        timer.Stop(); fullscreenTimer.Stop(); source?.RemoveHook(WndProc);
        BallWindow.Close(); EmoteWindow.Close();
        tray.Visible=false; tray.Dispose(); trayMenu.Dispose(); trayIcon.Dispose();
    }
}
