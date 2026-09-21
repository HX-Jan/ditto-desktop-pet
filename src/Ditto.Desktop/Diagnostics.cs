using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Ditto.Core;

namespace Ditto.Desktop;

internal static class Diagnostics
{
    internal static async void Start(PetWindow window,string[] args)
    {
        string output=Path.GetFullPath(args.SkipWhile(a=>a!="--output").Skip(1).FirstOrDefault() ?? "artifacts/diagnostics");
        Directory.CreateDirectory(output);
        var checks=new Dictionary<string,bool>();
        var watch=Stopwatch.StartNew();
        try
        {
            window.DiagnosticControl=true;
            if(args.Contains("--export-demo"))
            {
                foreach(PetState state in Enum.GetValues<PetState>())
                    for(int frame=0;frame<32;frame++)
                    {
                        var encoder=new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(Sprites.Get(state,frame/10.0)));
                        using var file=File.Create(Path.Combine(output,$"{state}-{frame:00}.png")); encoder.Save(file);
                    }
                foreach(Emote icon in Enum.GetValues<Emote>())
                {
                    var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(Sprites.Icon(icon)));
                    using var file=File.Create(Path.Combine(output,$"Emote-{icon}.png")); encoder.Save(file);
                }
                foreach(Emotion emotion in Enum.GetValues<Emotion>())
                {
                    var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(Sprites.Get(PetState.Idle,0,false,emotion)));
                    using var file=File.Create(Path.Combine(output,$"Emotion-{emotion}.png")); encoder.Save(file);
                }
                var ballEncoder=new PngBitmapEncoder(); ballEncoder.Frames.Add(BitmapFrame.Create(Sprites.Ball()));
                using(var file=File.Create(Path.Combine(output,"Ball.png"))) ballEncoder.Save(file);
                window.Close(); return;
            }
            await Task.Delay(750);
            checks["nonActivatingWindowStyle"]=(Native.GetWindowLongPtr(window.Handle,-20).ToInt64()&0x08000000)!=0;
            checks["toolWindowStyle"]=(Native.GetWindowLongPtr(window.Handle,-20).ToInt64()&0x80)!=0;
            checks["topmostStyle"]=(Native.GetWindowLongPtr(window.Handle,-20).ToInt64()&8)!=0;
            checks["mouseActivateReturnsNoActivate"]=Native.SendMessage(window.Handle,0x21,IntPtr.Zero,IntPtr.Zero)==new IntPtr(3);
            Native.GetWindowRect(window.Handle,out var rect);
            checks["transparentCornerPassesThrough"]=Native.WindowFromPoint(new Native.Point{X=rect.Left+2,Y=rect.Top+2})!=window.Handle;
            checks["opaqueBodyIsInteractive"]=Native.WindowFromPoint(new Native.Point{X=rect.Left+(rect.Right-rect.Left)/2,Y=rect.Top+(int)((rect.Bottom-rect.Top)*.8)})==window.Handle;
            foreach(int size in new[]{96,128,160,128})
            {
                window.SetSize(size); await Task.Delay(150);
                Native.GetWindowRect(window.Handle,out rect);
                checks[$"size{size}"]=Math.Abs((rect.Right-rect.Left)-size*window.Scale)<=1;
            }
            window.TogglePaused();
            var before=(window.Engine.X,window.Engine.Y,window.Engine.AnimationTime);
            await Task.Delay(500);
            checks["pauseFreezesMovementAndAnimation"]=before==(window.Engine.X,window.Engine.Y,window.Engine.AnimationTime);
            window.TogglePaused();
            window.ToggleSleep(); await Task.Delay(250);
            checks["sleep"]=window.Engine.State==PetState.Sleeping;
            window.ToggleSleep();
            var engine=window.Engine;
            engine.PointerDown(engine.X+64,engine.Y+80);
            engine.PointerMove(engine.X+180,engine.Y-120,4,4); window.Sync();
            checks["drag"]=engine.State==PetState.Dragging;
            engine.PointerUp();
            var dropWatch=Stopwatch.StartNew();
            while(engine.Y<engine.Floor && dropWatch.Elapsed<TimeSpan.FromSeconds(5)) await Task.Delay(100);
            checks["dropSettlesAtFloor"]=engine.Y==engine.Floor && engine.State!=PetState.Falling;
            window.Conceal(); await Task.Delay(250);
            checks["hiddenStopsTimer"]=!window.TimerRunning && !Native.IsWindowVisible(window.Handle);
            var focusBeforeReveal=Native.GetForegroundWindow();
            window.Reveal();
            checks["preservesForegroundWindow"]=Native.GetForegroundWindow()==focusBeforeReveal;
            await Task.Delay(250);
            checks["showRestoresTimer"]=window.TimerRunning && Native.IsWindowVisible(window.Handle);
            window.RefreshArea();
            checks["insideWorkArea"]=engine.X>=engine.Area.Left && engine.X<=engine.MaxX && engine.Y==engine.Floor;
            window.SpawnBall(); engine.Expressions.Show(Emotion.Happy,Emote.Heart); window.Sync();
            await Task.Delay(200);
            checks["ballIsVisible"]=Native.IsWindowVisible(window.BallWindow.Handle);
            checks["ballDoesNotActivate"]=(Native.GetWindowLongPtr(window.BallWindow.Handle,-20).ToInt64()&0x08000000)!=0;
            checks["emotePassThroughStyle"]=(Native.GetWindowLongPtr(window.EmoteWindow.Handle,-20).ToInt64()&0x20)!=0;
            Native.GetWindowRect(window.EmoteWindow.Handle,out var emoteRect);
            checks["emotePassesThrough"]=Native.WindowFromPoint(new Native.Point { X=(emoteRect.Left+emoteRect.Right)/2,Y=(emoteRect.Top+emoteRect.Bottom)/2 })!=window.EmoteWindow.Handle;
            window.TogglePaused(); var frozenBall=(engine.Ball.X,engine.Ball.Y,engine.IdlePlaySeconds);
            await Task.Delay(400);
            checks["pauseFreezesBallAndDeadline"]=frozenBall==(engine.Ball.X,engine.Ball.Y,engine.IdlePlaySeconds);
            window.TogglePaused(); engine.MenuOpen=true; frozenBall=(engine.Ball.X,engine.Ball.Y,engine.IdlePlaySeconds);
            await Task.Delay(300);
            checks["menuFreezesBallAndDeadline"]=frozenBall==(engine.Ball.X,engine.Ball.Y,engine.IdlePlaySeconds); engine.MenuOpen=false;
            window.ApplyFullscreen(true); var frozenPlay=engine.IdlePlaySeconds;
            await Task.Delay(350);
            checks["fullscreenHidesAllAndFreezes"]=!Native.IsWindowVisible(window.Handle)&&!Native.IsWindowVisible(window.BallWindow.Handle)&&!Native.IsWindowVisible(window.EmoteWindow.Handle)&&engine.IdlePlaySeconds==frozenPlay;
            window.ApplyFullscreen(false); await Task.Delay(150);
            checks["fullscreenRestoreIncludesBall"]=Native.IsWindowVisible(window.Handle)&&Native.IsWindowVisible(window.BallWindow.Handle)&&engine.Playing;
            window.ApplyFullscreen(true); window.Conceal(); window.ApplyFullscreen(false);
            checks["manualHiddenSurvivesFullscreenExit"]=!Native.IsWindowVisible(window.Handle)&&!engine.Playing;
            window.ApplyFullscreen(true); window.Reveal();
            checks["revealRespectsFullscreen"]=!Native.IsWindowVisible(window.Handle);
            window.ApplyFullscreen(false);
            var probe=new Window { Title="Ditto fullscreen geometry probe",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,
                ShowActivated=false,ShowInTaskbar=false,Background=System.Windows.Media.Brushes.Transparent,AllowsTransparency=true,
                Left=0,Top=0,Width=SystemParameters.PrimaryScreenWidth,Height=SystemParameters.PrimaryScreenHeight };
            probe.Show(); await Task.Delay(100);
            var probeHandle=new System.Windows.Interop.WindowInteropHelper(probe).Handle;
            checks["fullscreenGeometryDetected"]=Native.IsPrimaryFullscreen(probeHandle,true);
            checks["ownProcessExcluded"]=!Native.IsPrimaryFullscreen(probeHandle);
            probe.WindowState=WindowState.Maximized; await Task.Delay(100);
            checks["maximizedIsNotFullscreen"]=!Native.IsPrimaryFullscreen(probeHandle,true); probe.Close();
            window.SpawnBall(); window.ToggleSleep();
            checks["sleepDismissesToyWindow"]=!engine.Ball.Visible&&!Native.IsWindowVisible(window.BallWindow.Handle)&&!engine.Playing;
            window.ToggleSleep(); window.SpawnBall(); window.SetActivity(ActivityMode.Quiet);
            checks["quietEndsPlay"]=!engine.Playing&&!Native.IsWindowVisible(window.BallWindow.Handle);
            window.SetActivity(ActivityMode.Lively);
            foreach(PetState pose in Enum.GetValues<PetState>())
                foreach(Emotion emotion in Enum.GetValues<Emotion>())
                    for(int i=0;i<32;i++) Sprites.Get(pose,i/10.0,i%2==0,emotion,i%3-1,i%3-1);
            checks["spriteCacheBounded"]=Sprites.CacheCount<=256;
            var process=Process.GetCurrentProcess();
            process.Refresh();
            long initialMemory=process.PrivateMemorySize64;
            int initialHandles=process.HandleCount;
            TimeSpan initialCpu=process.TotalProcessorTime;
            var samples=new List<object>();
            bool baselineCaptured=false;
            if(args.Contains("--soak-test"))
            {
                int cycle=-1;
                window.DiagnosticFrame=_=>
                {
                    double t=watch.Elapsed.TotalSeconds; int next=(int)(t/10)%12;
                    if(next!=cycle)
                    {
                        cycle=next; engine.MenuOpen=false; engine.SetPaused(false);
                        window.ApplyFullscreen(false); if(window.VisibilityState.ManualHidden) window.Reveal();
                        if(engine.Sleeping) window.ToggleSleep();
                        switch(cycle)
                        {
                            case 0: window.SetActivity(ActivityMode.Lively); window.StartPlay(); break;
                            case 1: window.SpawnBall(); break;
                            case 2:
                                window.SpawnBall(); var b=engine.Ball;
                                engine.BallPointerDown(b.X+5,b.Y+5,t);
                                engine.BallPointerMove(b.X-130,b.Y-180,t+.15); engine.BallPointerUp(t+.16); break;
                            case 3: window.TogglePaused(); break;
                            case 4: window.ApplyFullscreen(true); break;
                            case 5: window.Reveal(); window.SetActivity(ActivityMode.Quiet); break;
                            case 6: window.SpawnBall(); engine.MenuOpen=true; break;
                            case 7: window.ToggleSleep(); break;
                            case 8:
                                window.EndPlay(); engine.Expressions.Show(Emotion.Happy,Emote.Heart); break;
                            case 9: window.SetActivity(ActivityMode.Lively); window.StartPlay(); break;
                            case 10:
                                window.SpawnBall(); engine.Expressions.Show(Emotion.Hurt,Emote.Sweat); break;
                            case 11: window.EndPlay(); window.SetSize(new[]{96,128,160}[(int)(t/120)%3]); break;
                        }
                    }
                    var a=engine.Area;
                    double x=a.Left+a.Width*(.5+.32*Math.Sin(t*.15)), y=a.Top+a.Height*(.4+.25*Math.Sin(t*.2));
                    if(cycle==8) { x=engine.X+engine.Size*(.5+.17*Math.Sin(t*8)); y=engine.Y+engine.Size*.5; }
                    engine.UpdateCursor(new CursorSnapshot(x,y,true));
                };
                while(watch.Elapsed<TimeSpan.FromMinutes(30))
                {
                    await Task.Delay(10000);
                    // The animation timer stops during fullscreen hiding; this driver also runs here to restore it.
                    window.DiagnosticFrame?.Invoke(watch.Elapsed.TotalSeconds);
                    process.Refresh();
                    // Compare against a warmed rendering cache, not first-frame initialization.
                    if (!baselineCaptured && watch.Elapsed.TotalSeconds >=600)
                    {
                        initialMemory=process.PrivateMemorySize64; initialHandles=process.HandleCount;
                        baselineCaptured=true;
                    }
                    samples.Add(new { seconds=watch.Elapsed.TotalSeconds, memory=process.PrivateMemorySize64, handles=process.HandleCount,
                        state=engine.State.ToString(), playing=engine.Playing, ball=engine.Ball.Visible, paused=engine.Paused, hidden=engine.Hidden, cachedSprites=Sprites.CacheCount });
                    File.WriteAllText(Path.Combine(output,"progress.json"),JsonSerializer.Serialize(new { elapsedSeconds=watch.Elapsed.TotalSeconds, samples },new JsonSerializerOptions { WriteIndented=true }));
                }
                checks["thirtyMinutesAlive"]=watch.Elapsed>=TimeSpan.FromMinutes(30);
                checks["boundedHandleGrowth"]=process.HandleCount-initialHandles<50;
                checks["boundedPrivateMemoryGrowth"]=process.PrivateMemorySize64-initialMemory<64*1024*1024;
                window.DiagnosticFrame=null;
            }
            process.Refresh();
            var result=new { passed=checks.Values.All(x=>x), elapsedSeconds=watch.Elapsed.TotalSeconds, scale=window.Scale, os=Environment.OSVersion.VersionString,
                checks, privateMemoryBytes=process.PrivateMemorySize64, initialMemoryBytes=initialMemory, handles=process.HandleCount,
                cpuSeconds=(process.TotalProcessorTime-initialCpu).TotalSeconds, samples };
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
            Application.Current.Shutdown(checks.Values.All(x=>x)?0:1);
        }
        catch(Exception ex)
        {
            File.WriteAllText(Path.Combine(output,"error.txt"),ex.ToString());
            Application.Current.Shutdown(1);
        }
    }
}
