using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ditto.Core;

namespace Ditto.Desktop;

/// <summary>Original 32px pixel drawing. All animation frames share this hand-plotted silhouette.</summary>
internal static class Sprites
{
    private static readonly Dictionary<(PetState, int, bool), BitmapSource> Cache = new();
    private static readonly (double X, double Y)[] Shape =
    [ (3,29), (2,27), (3,24), (5,21), (6,17), (7,13), (9,11), (11,12),
      (13,15), (17,15), (20,11), (22,10), (24,11), (25,14), (25,18),
      (27,21), (29,25), (29,28), (27,30), (22,31), (18,30), (13,31), (8,30), (5,31) ];

    public static BitmapSource Get(PetState state, double time, bool left = false)
    {
        int frame = (int)(time * 8) % (state is PetState.Idle ? 32 : 8);
        var key = (state, frame, left);
        if (Cache.TryGetValue(key, out var found)) return found;
        double phase = frame * Math.PI / 4;
        double sx = 1, sy = 1;
        switch (state)
        {
            case PetState.Idle: sy = 1 + Math.Sin(phase / 4) * .035; break;
            case PetState.Walking: sx = 1 + Math.Sin(phase) * .045; sy = 1 - Math.Sin(phase) * .075; break;
            case PetState.Reacting:
            case PetState.Landing:
                double bounce = Math.Sin(Math.Min(frame, 5) * Math.PI / 5);
                sx = 1 + bounce * .1; sy = 1 - bounce * .25; break;
            case PetState.Dragging: sx = .78; sy = 1.22; break;
            case PetState.Falling: sx = .9; sy = 1.1; break;
            case PetState.Sleeping: sx = 1.04; sy = .72 + Math.Sin(phase) * .025; break;
        }
        var polygon = new (double X, double Y)[Shape.Length];
        for (int i = 0; i < Shape.Length; i++)
            polygon[i] = (16 + (Shape[i].X - 16) * sx, 31 + (Shape[i].Y - 31) * sy);
        bool[,] filled = new bool[32,32];
        for (int y=0; y<32; y++) for (int x=0; x<32; x++) filled[x,y] = Inside(x+.5,y+.5,polygon);
        byte[] pixels = new byte[32*32*4];
        void Pixel(int x,int y, uint color)
        {
            if (x<0 || x>31 || y<0 || y>31) return;
            if (left) x=31-x;
            int p=(y*32+x)*4;
            pixels[p]=(byte)color; pixels[p+1]=(byte)(color>>8); pixels[p+2]=(byte)(color>>16); pixels[p+3]=255;
        }
        for (int y=0; y<32; y++) for (int x=0; x<32; x++)
        {
            if (!filled[x,y]) continue;
            bool edge=x==0 || x==31 || y==0 || y==31 || !filled[Math.Max(0,x-1),y] || !filled[Math.Min(31,x+1),y] || !filled[x,Math.Max(0,y-1)] || !filled[x,Math.Min(31,y+1)];
            Pixel(x,y,edge ? 0x654581u : y>27 ? 0xAC79CFu : 0xCAA0EBu);
            if (!edge && x<11 && y<22 && filled[x-1,y] && !filled[Math.Max(0,x-2),y]) Pixel(x,y,0xE6C6FA);
        }
        void Dot(double x,double y,uint color) => Pixel((int)Math.Round(16+(x-16)*sx),(int)Math.Round(31+(y-31)*sy),color);
        bool eyesClosed=state==PetState.Sleeping || state==PetState.Idle && frame is 27 or 28;
        if (eyesClosed)
        {
            for(int i=0;i<3;i++) { Dot(10+i,21,0x483454); Dot(20+i,21,0x483454); }
        }
        else { Dot(11,20,0x483454); Dot(11,21,0x483454); Dot(21,20,0x483454); Dot(21,21,0x483454); }
        Dot(14,23,0x634072); Dot(15,24,0x634072); Dot(16,24,0x634072); Dot(17,24,0x634072); Dot(18,23,0x634072);
        Dot(8,23,0xE7ACD5); Dot(9,23,0xE7ACD5); Dot(23,23,0xE7ACD5); Dot(24,23,0xE7ACD5);
        if(state==PetState.Sleeping)
        {
            int yy=5-frame/3;
            for(int i=0;i<4;i++) { Pixel(23+i,yy,0x967AB5); Pixel(26-i,yy+i,0x967AB5); Pixel(23+i,yy+3,0x967AB5); }
        }
        var bitmap=BitmapSource.Create(32,32,96,96,PixelFormats.Bgra32,null,pixels,128);
        bitmap.Freeze(); Cache[key]=bitmap; return bitmap;
    }
    private static bool Inside(double x,double y,(double X,double Y)[] points)
    {
        bool inside=false;
        for(int i=0,j=points.Length-1;i<points.Length;j=i++)
            if ((points[i].Y>y)!=(points[j].Y>y) && x<(points[j].X-points[i].X)*(y-points[i].Y)/(points[j].Y-points[i].Y)+points[i].X) inside=!inside;
        return inside;
    }
}
