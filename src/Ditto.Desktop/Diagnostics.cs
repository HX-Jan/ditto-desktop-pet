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
            if(args.Contains("--export-demo"))
            {
                foreach(PetState state in Enum.GetValues<PetState>())
                    for(int frame=0;frame<32;frame++)
                    {
                        var encoder=new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(Sprites.Get(state,frame/8.0)));
                        using var file=File.Create(Path.Combine(output,$"{state}-{frame:00}.png")); encoder.Save(file);
                    }
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
            var process=Process.GetCurrentProcess();
            process.Refresh();
            long initialMemory=process.PrivateMemorySize64;
            int initialHandles=process.HandleCount;
            TimeSpan initialCpu=process.TotalProcessorTime;
            var samples=new List<object>();
            if(args.Contains("--soak-test"))
            {
                while(watch.Elapsed<TimeSpan.FromMinutes(30))
                {
                    await Task.Delay(10000);
                    process.Refresh();
                    // Compare against a warmed rendering cache, not first-frame initialization.
                    if (watch.Elapsed.TotalSeconds is >=120 and <131)
                    { initialMemory=process.PrivateMemorySize64; initialHandles=process.HandleCount; }
                    samples.Add(new { seconds=watch.Elapsed.TotalSeconds, memory=process.PrivateMemorySize64, handles=process.HandleCount, state=engine.State.ToString() });
                    File.WriteAllText(Path.Combine(output,"progress.json"),JsonSerializer.Serialize(new { elapsedSeconds=watch.Elapsed.TotalSeconds, samples },new JsonSerializerOptions { WriteIndented=true }));
                }
                checks["thirtyMinutesAlive"]=watch.Elapsed>=TimeSpan.FromMinutes(30);
                checks["boundedHandleGrowth"]=process.HandleCount-initialHandles<50;
                checks["boundedPrivateMemoryGrowth"]=process.PrivateMemorySize64-initialMemory<64*1024*1024;
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
