using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ditto.Core;

namespace Ditto.Desktop;

/// <summary>Hand-plotted pixel art. Quantized poses and a bounded cache avoid coordinate-based growth.</summary>
internal static class Sprites
{
    private readonly record struct Key(PetState State,int Frame,bool Left,Emotion Emotion,int GazeX,int GazeY);
    private static readonly Dictionary<Key,BitmapSource> Cache=new();
    private static readonly Queue<Key> Order=new();
    private static readonly Dictionary<Emote,BitmapSource> Icons=new();
    private static BitmapSource? ball;
    internal static int CacheCount => Cache.Count;
    private static readonly (double X,double Y)[] Shape=
    [(2,28),(2,26),(3,23),(5,21),(6,17),(7,14),(9,12),(11,12),(13,14),
     (15,15),(18,14),(20,12),(22,12),(24,14),(25,18),(26,21),
     (28,23),(30,26),(30,28),(28,30),(24,31),(20,30),(16,31),(12,30),(8,31),(4,30)];

    internal static BitmapSource Get(PetState state,double time,bool left=false,Emotion emotion=Emotion.Neutral,int gazeX=0,int gazeY=0)
    {
        int count=state==PetState.Idle?32:16;
        int frame=(int)(time*10)%count;
        gazeX=Math.Clamp(gazeX,-1,1); gazeY=Math.Clamp(gazeY,-1,1);
        var key=new Key(state,frame,left,emotion,gazeX,gazeY);
        if(Cache.TryGetValue(key,out var cached)) return cached;
        double phase=frame*Math.PI/8;
        double sx=1,sy=1,lift=0,lean=0;
        switch(state)
        {
            case PetState.Idle: sy=1+Math.Sin(phase/2)*.025; break;
            case PetState.Walking: sx=1+Math.Sin(phase)*.04; sy=1-Math.Sin(phase)*.07; lean=Math.Sin(phase)*.65; break;
            case PetState.Reacting:
            case PetState.Landing:
                double bounce=Math.Sin(Math.Min(frame,6)*Math.PI/6);
                sx=1+bounce*.07; sy=1-bounce*.24; break;
            case PetState.Dragging: sx=.78; sy=1.24; break;
            case PetState.Falling: sx=.88; sy=1.12; break;
            case PetState.Sleeping: sx=1.03; sy=.72+Math.Sin(phase)*.02; break;
            case PetState.Petting: sx=1.03; sy=.89+Math.Sin(phase)*.025; emotion=Emotion.Happy; break;
            case PetState.Stretching: sx=1-.08*Math.Sin(phase); sy=1+.16*Math.Sin(phase); break;
            case PetState.Yawning: sy=.93; break;
            case PetState.Jumping: lift=7*Math.Sin(Math.PI*Math.Min(frame,15)/15); sy=1+.08*Math.Sin(phase); break;
        }
        var polygon=new (double X,double Y)[Shape.Length];
        for(int i=0;i<Shape.Length;i++) polygon[i]=(16+(Shape[i].X-16)*sx+lean,31+(Shape[i].Y-31)*sy-lift);
        bool[,] filled=new bool[32,32];
        for(int y=0;y<32;y++) for(int x=0;x<32;x++) filled[x,y]=Inside(x+.5,y+.5,polygon);
        var canvas=new PixelCanvas(32);
        void Pixel(int x,int y,uint color) => canvas.Dot(left?31-x:x,y,color);
        for(int y=0;y<32;y++) for(int x=0;x<32;x++)
        {
            if(!filled[x,y]) continue;
            bool edge=x==0||x==31||y==0||y==31||!filled[Math.Max(0,x-1),y]||!filled[Math.Min(31,x+1),y]||!filled[x,Math.Max(0,y-1)]||!filled[x,Math.Min(31,y+1)];
            Pixel(x,y,edge?0x70508Bu:y>27-lift?0xB38CD5u:0xD0A8EBu);
            if(!edge && x<12 && y<22-lift && !filled[Math.Max(0,x-2),y]) Pixel(x,y,0xEAD1FB);
        }
        void Dot(double x,double y,uint color=0x51345F) =>
            Pixel((int)Math.Round(16+(x-16)*sx+lean),(int)Math.Round(31+(y-31)*sy-lift),color);
        // Compensate before mirroring so gaze always points toward the actual cursor.
        int gx=left?-gazeX:gazeX, gy=gazeY;
        bool closed=state is PetState.Sleeping or PetState.Petting || emotion is Emotion.Happy or Emotion.Affection;
        if(closed)
        {
            foreach(int xx in new[]{10,20}) { Dot(xx,21); Dot(xx+1,20); Dot(xx+2,21); }
        }
        else if(emotion==Emotion.Hurt)
        {
            Dot(10,20); Dot(11,21); Dot(12,21); Dot(20,21); Dot(21,21); Dot(22,20);
        }
        else if(state==PetState.Idle && frame is 27 or 28)
        {
            for(int i=0;i<3;i++) { Dot(10+i,21); Dot(20+i,21); }
        }
        else
        {
            foreach(int xx in new[]{11,21}) { Dot(xx+gx,20+gy); Dot(xx+gx,21+gy); }
        }
        if(state==PetState.Yawning || emotion==Emotion.Surprised)
        {
            for(int y=23;y<=25;y++) { Dot(15,y); Dot(17,y); }
            Dot(16,22); Dot(16,26);
        }
        else if(emotion==Emotion.Hurt) { Dot(14,25); Dot(15,24); Dot(16,24); Dot(17,24); Dot(18,25); }
        else { Dot(14,23); Dot(15,24); Dot(16,24); Dot(17,24); Dot(18,23); }
        Dot(8,23,0xEEB7D8); Dot(9,23,0xEEB7D8); Dot(23,23,0xEEB7D8); Dot(24,23,0xEEB7D8);
        if(state==PetState.Sleeping)
        {
            int yy=4-frame/6;
            for(int i=0;i<4;i++) { Pixel(23+i,yy,0xAB8BCD); Pixel(26-i,yy+i,0xAB8BCD); Pixel(23+i,yy+3,0xAB8BCD); }
        }
        var bitmap=canvas.Bitmap();
        if(Cache.Count>=256) Cache.Remove(Order.Dequeue());
        Cache.Add(key,bitmap); Order.Enqueue(key); return bitmap;
    }
    internal static BitmapSource Ball()
    {
        if(ball!=null) return ball;
        var c=new PixelCanvas(16);
        for(int y=0;y<16;y++) for(int x=0;x<16;x++)
        {
            double r=Math.Sqrt((x-7.5)*(x-7.5)+(y-7.5)*(y-7.5));
            if(r<7) c.Dot(x,y,r>5.8?0x866342u:y<7?0xFFDEA0u:0xF1AA72u);
        }
        c.Dot(4,4,0xFFF6D5); c.Dot(5,4,0xFFF6D5); c.Dot(4,5,0xFFF6D5);
        ball=c.Bitmap(); return ball;
    }
    internal static BitmapSource Icon(Emote icon)
    {
        if(Icons.TryGetValue(icon,out var found)) return found;
        var c=new PixelCanvas(16);
        string[] rows=icon switch
        {
            Emote.Heart => ["................","................","...###...###....","..#####.#####...","..###########...","..###########...","...#########....","....#######.....",".....#####......","......###.......",".......#........"],
            Emote.Question => ["................",".....#####......","....##...##.....",".........##.....","........##......",".......##.......",".......##.......","................",".......##.......",".......##......."],
            Emote.Sweat => ["................","........#.......",".......###......","......#####.....",".....#######....",".....#######....",".....#######....","......#####.....",".......###......"],
            _ => []
        };
        uint color=icon==Emote.Heart?0xF59ABE:icon==Emote.Sweat?0x8FD6F2u:0xFFDC96u;
        for(int y=0;y<rows.Length;y++) for(int x=0;x<rows[y].Length;x++) if(rows[y][x]=='#') c.Dot(x,y,color);
        found=c.Bitmap(); Icons.Add(icon,found); return found;
    }
    private sealed class PixelCanvas(int size)
    {
        private readonly byte[] pixels=new byte[size*size*4];
        internal void Dot(int x,int y,uint color)
        {
            if(x<0||x>=size||y<0||y>=size) return;
            int p=(y*size+x)*4; pixels[p]=(byte)color; pixels[p+1]=(byte)(color>>8); pixels[p+2]=(byte)(color>>16); pixels[p+3]=255;
        }
        internal BitmapSource Bitmap()
        { var result=BitmapSource.Create(size,size,96,96,PixelFormats.Bgra32,null,pixels,size*4); result.Freeze(); return result; }
    }
    private static bool Inside(double x,double y,(double X,double Y)[] points)
    {
        bool inside=false;
        for(int i=0,j=points.Length-1;i<points.Length;j=i++)
            if((points[i].Y>y)!=(points[j].Y>y)&&x<(points[j].X-points[i].X)*(y-points[i].Y)/(points[j].Y-points[i].Y)+points[i].X) inside=!inside;
        return inside;
    }
}
