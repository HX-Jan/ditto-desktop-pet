namespace Ditto.Core;

public enum PetState { Idle, Walking, Reacting, Dragging, Falling, Landing, Sleeping }
public readonly record struct WorkArea(double Left, double Top, double Width, double Height);

/// <summary>Coordinates and distances are device independent pixels. No desktop dependencies.</summary>
public sealed class PetEngine
{
    private readonly Random random;
    private double stateTime;
    private double nextDecision = 3;
    private double velocityY;
    private bool pointerDown;
    private double pressX, pressY, offsetX, offsetY;
    public PetEngine(int seed = 0) => random = seed == 0 ? new Random() : new Random(seed);
    public PetState State { get; private set; } = PetState.Idle;
    public bool Paused { get; private set; }
    public bool Sleeping { get; private set; }
    public bool Hidden { get; set; }
    public bool FacingLeft { get; private set; }
    public double X { get; private set; }
    public double Y { get; private set; }
    public double Size { get; private set; } = 128;
    public double AnimationTime { get; private set; }
    public WorkArea Area { get; private set; }
    public double Floor => Math.Max(Area.Top, Area.Top + Area.Height - Size);
    public double MaxX => Math.Max(Area.Left, Area.Left + Area.Width - Size);
    public bool PointerHeld => pointerDown;

    public void Configure(WorkArea area, double size, double? x = null)
    {
        Area = area;
        Size = size is 96 or 128 or 160 ? size : 128;
        X = Math.Clamp(x ?? X, area.Left, MaxX);
        Y = Floor;
        pointerDown = false;
        SetState(Sleeping ? PetState.Sleeping : PetState.Idle);
    }

    public void SetPaused(bool paused) { Paused = paused; }
    public void SetSleeping(bool sleeping)
    {
        Sleeping = sleeping;
        pointerDown = false;
        SetState(sleeping ? PetState.Sleeping : PetState.Idle);
        if (!sleeping && Y < Floor) SetState(PetState.Falling);
    }
    public void PointerDown(double x, double y)
    {
        pointerDown = true;
        pressX = x; pressY = y; offsetX = x - X; offsetY = y - Y;
    }
    public void PointerMove(double x, double y, double thresholdX, double thresholdY)
    {
        if (!pointerDown) return;
        if (State != PetState.Dragging &&
            (Math.Abs(x - pressX) >= thresholdX || Math.Abs(y - pressY) >= thresholdY))
            SetState(PetState.Dragging);
        if (State != PetState.Dragging) return;
        X = Math.Clamp(x - offsetX, Area.Left, MaxX);
        Y = Math.Clamp(y - offsetY, Area.Top, Floor);
    }
    public void PointerUp()
    {
        if (!pointerDown) return;
        pointerDown = false;
        if (State == PetState.Dragging)
        {
            velocityY = 0;
            // Pausing freezes physics too; a paused drop settles immediately.
            if (Paused) { Y = Floor; SetState(Sleeping ? PetState.Sleeping : PetState.Idle); }
            else SetState(Y < Floor ? PetState.Falling : PetState.Landing);
        }
        else if (!Paused)
        {
            Sleeping = false;
            SetState(PetState.Reacting);
        }
    }
    public void CancelPointer()
    {
        if (!pointerDown) return;
        pointerDown = false;
        if (State == PetState.Dragging)
        {
            Y = Floor;
            SetState(Sleeping ? PetState.Sleeping : PetState.Idle);
        }
    }
    public void Tick(double seconds)
    {
        if (Paused || Hidden) return;
        double dt = Math.Clamp(seconds, 0, 0.05);
        AnimationTime += dt;
        if (pointerDown || State == PetState.Dragging) return;
        stateTime += dt;
        switch (State)
        {
            case PetState.Falling:
                velocityY += 1000 * dt;
                Y = Math.Min(Floor, Y + velocityY * dt);
                if (Y >= Floor) SetState(PetState.Landing);
                break;
            case PetState.Reacting:
            case PetState.Landing:
                if (stateTime >= 0.65)
                    SetState(Y < Floor ? PetState.Falling : Sleeping ? PetState.Sleeping : PetState.Idle);
                break;
            case PetState.Sleeping: break;
            case PetState.Idle:
                if (stateTime >= nextDecision)
                {
                    FacingLeft = random.Next(2) == 0;
                    nextDecision = 2 + random.NextDouble() * 4;
                    SetState(PetState.Walking);
                }
                break;
            case PetState.Walking:
                X += (FacingLeft ? -1 : 1) * 24 * dt;
                if (X <= Area.Left) { X = Area.Left; FacingLeft = false; }
                if (X >= MaxX) { X = MaxX; FacingLeft = true; }
                if (stateTime >= nextDecision)
                {
                    nextDecision = 3 + random.NextDouble() * 6;
                    SetState(PetState.Idle);
                }
                break;
        }
    }
    private void SetState(PetState state)
    {
        State = state;
        stateTime = 0;
        AnimationTime = 0;
    }
}
