using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace Ditto.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        bool test = Array.Exists(args, a => a is "--smoke-test" or "--soak-test" or "--export-demo");
        string suffix = test ? ".Test." + Environment.ProcessId : "";
        using var mutex = new Mutex(true, @"Local\DittoDesktopPet" + suffix, out bool first);
        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\DittoDesktopPet.Show" + suffix);
        if (!first) { showEvent.Set(); return; }
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) =>
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DittoDesktopPet");
            try { Directory.CreateDirectory(dir); File.AppendAllText(Path.Combine(dir,"error.log"),DateTime.Now+" "+e.Exception+Environment.NewLine); } catch (IOException) { }
            MessageBox.Show("桌宠遇到了问题，请重新启动。错误记录位于 %LOCALAPPDATA%\\DittoDesktopPet\\error.log。", "百变怪桌宠");
            e.Handled = true; app.Shutdown(1);
        };
        var window = new PetWindow(test);
        using var stop = new CancellationTokenSource();
        var listener = new Thread(() =>
        {
            while (!stop.IsCancellationRequested)
                if (showEvent.WaitOne(250) && !stop.IsCancellationRequested)
                    app.Dispatcher.BeginInvoke(window.Reveal);
        }) { IsBackground = true, Name = "Show existing pet" };
        listener.Start();
        app.Startup += (_,_) =>
        {
            window.Show();
            if (test) app.Dispatcher.BeginInvoke(() => Diagnostics.Start(window,args),DispatcherPriority.ApplicationIdle);
        };
        app.Exit += (_,_) => { stop.Cancel(); window.Dispose(); };
        app.Run();
        stop.Cancel(); listener.Join(1000);
        mutex.ReleaseMutex();
    }
}
