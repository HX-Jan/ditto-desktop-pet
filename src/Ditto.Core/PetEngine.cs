namespace Ditto.Core;

public enum PetState { Idle, Walking, Reacting, Dragging, Falling, Landing, Sleeping, Petting, Stretching, Yawning, Jumping }
public readonly record struct WorkArea(double Left, double Top, double Width, double Height);

/// <summary>All coordinates are DIP. Behavior, expressions and ball physics are independent.</summary>
public sealed class PetEngine
{
    private readonly Random random;
    private readonly PettingRecognizer petting = new();
    private double stateTime, velocityY, time, nextAction=15, lastClick=-10, lastBump=-10;
    private double pressX, pressY, offsetX, offsetY, previousCursorX, previousCursorY, lastInteraction;
    private double pettingCooldown, stillHeadTime;
    private bool pointerDown, hasCursor;
    private int rapidClicks;
    private CursorSnapshot cursor;
    public PetEngine(int seed=0) => random=seed==0?new Random():new Random(seed);
    public PetState State { get; private set; }=PetState.Idle;
    public ActivityMode Activity { get; private set; }=ActivityMode.Lively;
    public Expressions Expressions { get; }=new();
    public BallPhysics Ball { get; }=new();
    public bool Paused { get; private set; }
    public bool Sleeping { get; private set; }
    public bool Hidden { get; set; }
    public bool MenuOpen { get; set; }
    public bool Playing { get; private set; }
    public bool FacingLeft { get; private set; }
    public int GazeX { get; private set; }
    public int GazeY { get; private set; }
    public double X { get; private set; }
    public double Y { get; private set; }
    public double Size { get; private set; }=128;
    public double AnimationTime { get; private set; }
    public double IdlePlaySeconds => time-lastInteraction;
    public WorkArea Area { get; private set; }
    public double Floor => Math.Max(Area.Top,Area.Top+Area.Height-Size);
    public double MaxX => Math.Max(Area.Left,Area.Left+Area.Width-Size);
    public bool PointerHeld => pointerDown;
    public bool Frozen => Paused || Hidden || MenuOpen;

    public void Configure(WorkArea area,double size,double? x=null)
    {
        Area=area; Size=size is 96 or 128 or 160?size:128;
        X=Math.Clamp(x??X,area.Left,MaxX); Y=Floor;
        pointerDown=false; petting.Reset(); hasCursor=false;
        Ball.Configure(area,Size); SetState(Sleeping?PetState.Sleeping:PetState.Idle);
    }
    public void SetActivity(ActivityMode activity)
    {
        Activity=activity;
        if(activity==ActivityMode.Quiet) EndPlay();
        nextAction=time+(activity==ActivityMode.Quiet?35:15);
    }
    public void SetPaused(bool paused) => Paused=paused;
    public void SetSleeping(bool sleeping)
    {
        Sleeping=sleeping; EndPlay(); CancelPointer(); Expressions.Clear();
        if(sleeping) Y=Floor;
        SetState(sleeping?PetState.Sleeping:Y<Floor?PetState.Falling:PetState.Idle);
    }
    public void StartPlay()
    {
        if(Hidden) return;
        Sleeping=false; Playing=true; lastInteraction=time;
        if(State==PetState.Sleeping) SetState(PetState.Idle);
    }
    public void SpawnBall()
    { StartPlay(); if(!Playing) return; Ball.Spawn(X+Size+12,Y+Size*.6); lastInteraction=time; }
    public void EndPlay()
    {
        Playing=false; Ball.Dismiss();
        if(pointerDown) return;
        if(Paused) Y=Floor;
        if(!Sleeping && State!=PetState.Dragging) SetState(Y<Floor?PetState.Falling:PetState.Idle);
    }
    public void UpdateCursor(CursorSnapshot sample) => cursor=sample;
    public void BallPointerDown(double x,double y,double seconds)
    { Ball.PointerDown(x,y,seconds); lastInteraction=time; }
    public void BallPointerMove(double x,double y,double seconds)
    { if(Ball.Dragging) { Ball.PointerMove(x,y,seconds); lastInteraction=time; } }
    public void BallPointerUp(double seconds) { Ball.PointerUp(Paused,seconds); lastInteraction=time; }
    public void PointerDown(double x,double y)
    {
        pointerDown=true; pressX=x; pressY=y; offsetX=x-X; offsetY=y-Y;
        petting.Reset(); lastInteraction=time;
    }
    public void PointerMove(double x,double y,double thresholdX,double thresholdY)
    {
        if(!pointerDown) return;
        if(State!=PetState.Dragging && (Math.Abs(x-pressX)>=thresholdX || Math.Abs(y-pressY)>=thresholdY))
        { SetState(PetState.Dragging); if(!Paused) Expressions.Show(Emotion.Surprised,Emote.Question); }
        if(State!=PetState.Dragging) return;
        X=Math.Clamp(x-offsetX,Area.Left,MaxX); Y=Math.Clamp(y-offsetY,Area.Top,Floor); lastInteraction=time;
    }
    public void PointerUp()
    {
        if(!pointerDown) return;
        pointerDown=false; lastInteraction=time;
        if(State==PetState.Dragging)
        {
            velocityY=0;
            if(Paused) { Y=Floor; SetState(Sleeping?PetState.Sleeping:PetState.Idle); }
            else SetState(Y<Floor?PetState.Falling:PetState.Landing);
        }
        else if(!Paused)
        {
            Sleeping=false; rapidClicks=time-lastClick<.7?rapidClicks+1:1; lastClick=time;
            Expressions.Show(rapidClicks>=3?Emotion.Hurt:Emotion.Happy,rapidClicks>=3?Emote.Sweat:Emote.None);
            SetState(PetState.Reacting);
        }
    }
    public void CancelPointer()
    {
        pointerDown=false; petting.Reset();
        if(State==PetState.Dragging) { Y=Floor; SetState(Sleeping?PetState.Sleeping:PetState.Idle); }
    }
    public void Tick(double seconds)
    {
        if(Frozen) { hasCursor=false; return; }
        double dt=Math.Clamp(seconds,0,.05); time+=dt; AnimationTime+=dt; stateTime+=dt;
        Expressions.Tick(dt); Ball.Tick(dt);
        ObserveCursor(dt);
        if(Playing && time-lastInteraction>=60 && !pointerDown && !Ball.Dragging) EndPlay();
        if(pointerDown || State==PetState.Dragging) return;
        switch(State)
        {
            case PetState.Falling:
                velocityY+=1000*dt; Y=Math.Min(Floor,Y+velocityY*dt);
                if(Y>=Floor) SetState(PetState.Landing);
                return;
            case PetState.Reacting:
            case PetState.Landing:
                if(stateTime>=.65) SetState(Sleeping?PetState.Sleeping:Y<Floor&&!Playing?PetState.Falling:PetState.Idle);
                return;
            case PetState.Petting:
                if(stateTime>=1.6) SetState(PetState.Idle);
                return;
            case PetState.Stretching:
            case PetState.Yawning:
            case PetState.Jumping:
                if(stateTime>=1.6) SetState(PetState.Idle);
                return;
            case PetState.Sleeping: return;
        }
        bool headMotion=cursor.InPrimary && !cursor.ButtonsDown && InHead(cursor.X,cursor.Y) && stillHeadTime<.8;
        if(headMotion) { SetMotion(false); return; }
        if(Ball.Visible)
        {
            MoveToward(Ball.X+Ball.Diameter/2,Ball.Y+Ball.Diameter/2,Size*.33,160,dt,true);
            double distance=Distance(X+Size/2,Y+Size*.72,Ball.X+Ball.Diameter/2,Ball.Y+Ball.Diameter/2);
            if(distance<Size*.52 && time-lastBump>=1.5 && !Ball.Dragging)
            {
                double direction=Ball.X+Ball.Diameter/2>=X+Size/2?1:-1;
                Ball.Nudge(direction); lastBump=time; Expressions.Show(Emotion.Happy,Emote.Heart);
            }
        }
        else if(cursor.InPrimary && (Playing || Activity==ActivityMode.Lively))
            MoveToward(cursor.X,cursor.Y,Size,Playing?155:65,dt,Playing);
        else SetMotion(false);
        if(State==PetState.Idle && time>=nextAction)
        {
            if(Activity==ActivityMode.Lively)
            {
                SetState(new[]{PetState.Stretching,PetState.Jumping,PetState.Yawning}[random.Next(3)]);
                Expressions.Show(Emotion.Affection,random.Next(2)==0?Emote.Heart:Emote.Question);
            }
            else SetState(random.Next(2)==0?PetState.Stretching:PetState.Yawning);
            nextAction=time+(Activity==ActivityMode.Lively?12+random.NextDouble()*12:35+random.NextDouble()*25);
        }
    }
    private void ObserveCursor(double dt)
    {
        double dx=hasCursor?cursor.X-previousCursorX:0, dy=hasCursor?cursor.Y-previousCursorY:0;
        hasCursor=true; previousCursorX=cursor.X; previousCursorY=cursor.Y;
        bool moved=Math.Abs(dx)+Math.Abs(dy)>=.5;
        stillHeadTime=moved?0:stillHeadTime+dt;
        if(cursor.InPrimary)
        {
            GazeX=Math.Abs(cursor.X-X-Size/2)<Size*.15?0:Math.Sign(cursor.X-X-Size/2);
            GazeY=Math.Abs(cursor.Y-Y-Size*.6)<Size*.15?0:Math.Sign(cursor.Y-Y-Size*.6);
            if(moved && Distance(cursor.X,cursor.Y,X+Size/2,Y+Size/2)<Size*1.5) lastInteraction=time;
        }
        else GazeX=GazeY=0;
        bool eligible=cursor.InPrimary && !Sleeping && !pointerDown && State is PetState.Idle or PetState.Walking or PetState.Petting;
        if(petting.Observe(dx/Size,eligible&&InHead(cursor.X,cursor.Y),cursor.ButtonsDown,dt) && time>=pettingCooldown)
        {
            SetState(PetState.Petting); Expressions.Show(Emotion.Happy,Emote.Heart);
            lastInteraction=time; pettingCooldown=time+1.5;
        }
    }
    public bool InHead(double x,double y) => x>=X+Size*.22 && x<=X+Size*.78 && y>=Y+Size*.38 && y<=Y+Size*.64;
    private void MoveToward(double targetX,double targetY,double gap,double speed,double dt,bool vertical)
    {
        double cx=X+Size/2, cy=Y+Size*.72;
        double dx=targetX-cx, dy=vertical?targetY-cy:0;
        double distance=Math.Sqrt(dx*dx+dy*dy);
        if(distance<=gap+3) { SetMotion(false); return; }
        double step=Math.Min(speed*dt,distance-gap);
        double oldX=X, oldY=Y;
        X=Math.Clamp(X+dx/distance*step,Area.Left,MaxX);
        if(vertical) Y=Math.Clamp(Y+dy/distance*step,Area.Top,Floor);
        if(Math.Abs(dx)>1) FacingLeft=dx<0;
        SetMotion(Math.Abs(X-oldX)+Math.Abs(Y-oldY)>.001);
    }
    private void SetMotion(bool walking)
    { var next=walking?PetState.Walking:PetState.Idle; if(State!=next) SetState(next); }
    private void SetState(PetState state)
    { State=state; stateTime=0; AnimationTime=0; if(state==PetState.Falling) velocityY=0; }
    private static double Distance(double x,double y,double tx,double ty) => Math.Sqrt((x-tx)*(x-tx)+(y-ty)*(y-ty));
}
