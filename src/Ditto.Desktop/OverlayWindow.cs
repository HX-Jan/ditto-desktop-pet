using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace Ditto.Desktop;

internal sealed class OverlayWindow : Window
{
    private readonly Image image=new() { Stretch=Stretch.Fill, SnapsToDevicePixels=true };
    private readonly bool passThrough;
    private HwndSource? source;
    internal IntPtr Handle { get; private set; }
    internal OverlayWindow(Window owner, string title, bool transparent)
    {
        owner.SourceInitialized+=(_,_)=>Owner=owner; Title=title; passThrough=transparent;
        Width=Height=32; ShowActivated=false; ShowInTaskbar=false; Focusable=false;
        Topmost=true; AllowsTransparency=true; WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize;
        Background=Brushes.Transparent; UseLayoutRounding=true;
        RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.NearestNeighbor); Content=image;
        SourceInitialized+=(_,_)=>
        {
            Handle=new WindowInteropHelper(this).Handle; Native.SetPetStyles(Handle,passThrough);
            source=HwndSource.FromHwnd(Handle); source.AddHook(WndProc);
        };
        Closed+=(_,_)=> { source?.RemoveHook(WndProc); source=null; };
    }
    internal void Present(ImageSource picture,double x,double y,double size)
    {
        image.Source=picture; Left=x; Top=y; Width=Height=size;
        if(!IsVisible) Show();
    }
    private IntPtr WndProc(IntPtr hwnd,int msg,IntPtr w,IntPtr l,ref bool handled)
    {
        if(msg==0x21) { handled=true; return new IntPtr(3); }
        if(passThrough && msg==0x84) { handled=true; return new IntPtr(-1); }
        return IntPtr.Zero;
    }
}
