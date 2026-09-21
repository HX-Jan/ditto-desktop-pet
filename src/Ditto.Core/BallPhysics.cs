namespace Ditto.Core;

public sealed class BallPhysics
{
    private WorkArea area;
    private double offsetX, offsetY, previousX, previousY, previousTime, lastMovementTime;
    public bool Visible { get; private set; }
    public bool Dragging { get; private set; }
    public double X { get; private set; }
    public double Y { get; private set; }
    public double VX { get; private set; }
    public double VY { get; private set; }
    public double Diameter { get; private set; } = 28;
    public double Floor => Math.Max(area.Top, area.Top + area.Height - Diameter);
    public double MaxX => Math.Max(area.Left, area.Left + area.Width - Diameter);
    public void Configure(WorkArea bounds, double petSize)
    { area = bounds; Diameter = petSize / 4; Dragging = false; VX = VY = 0; Clamp(); }
    public void Spawn(double x, double y)
    { Visible = true; Dragging = false; X = x; Y = y; VX = VY = 0; Clamp(); }
    public void Dismiss() { Visible = Dragging = false; VX = VY = 0; }
    public void PointerDown(double x, double y, double seconds)
    {
        if (!Visible) return;
        Dragging = true; offsetX = x-X; offsetY = y-Y;
        previousX=X; previousY=Y; previousTime=lastMovementTime=seconds; VX=VY=0;
    }
    public void PointerMove(double x, double y, double seconds)
    {
        if (!Dragging) return;
        X=x-offsetX; Y=y-offsetY; Clamp();
        double dt=seconds-previousTime;
        if (dt >= .008)
        {
            VX=Math.Clamp((X-previousX)/dt,-900,900); VY=Math.Clamp((Y-previousY)/dt,-900,900);
            if (Math.Abs(X-previousX)+Math.Abs(Y-previousY)>0.5) lastMovementTime=seconds;
            previousX=X; previousY=Y; previousTime=seconds;
        }
    }
    public void PointerUp(bool paused, double seconds)
    {
        if (!Dragging) return;
        Dragging=false;
        if (paused) { Y=Floor; VX=VY=0; }
        else if(seconds-lastMovementTime>.15) VX=VY=0;
    }
    public void CancelDrag() { if(Dragging) { Dragging=false; VX=VY=0; } }
    public void Nudge(double direction) { if(Visible && !Dragging) { VX=direction*240; VY=-300; } }
    public void Tick(double seconds)
    {
        if(!Visible || Dragging) return;
        double dt=Math.Clamp(seconds,0,.05);
        VY=Math.Min(900,VY+850*dt); X+=VX*dt; Y+=VY*dt;
        if(X<area.Left) { X=area.Left; VX=Math.Abs(VX)*.65; }
        if(X>MaxX) { X=MaxX; VX=-Math.Abs(VX)*.65; }
        if(Y<area.Top) { Y=area.Top; VY=Math.Abs(VY)*.55; }
        if(Y>=Floor)
        {
            Y=Floor; VY=Math.Abs(VY)<70?0:-Math.Abs(VY)*.55;
            VX*=Math.Exp(-2.7*dt); if(Math.Abs(VX)<3) VX=0;
        }
    }
    private void Clamp() { X=Math.Clamp(X,area.Left,MaxX); Y=Math.Clamp(Y,area.Top,Floor); }
}
