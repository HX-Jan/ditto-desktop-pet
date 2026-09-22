using Ditto.Core;

int passed=0;
void Check(bool condition,string name) { if(!condition) throw new Exception("FAIL: "+name); Console.WriteLine("PASS: "+name); passed++; }
PetEngine Pet() { var p=new PetEngine(42); p.Configure(new WorkArea(0,0,1920,1040),128,500); return p; }
void Advance(PetEngine p,double seconds) { for(int i=0;i<(int)(seconds*30);i++) p.Tick(1.0/30); }
var pet=Pet();
Check(pet.Y==912,"starts at work area floor");
pet.PointerDown(550,950); pet.PointerMove(552,952,4,4); pet.PointerUp();
Check(pet.State==PetState.Reacting,"small movement remains click");
Advance(pet,1); Check(pet.State==PetState.Idle,"reaction returns to idle");
pet.PointerDown(pet.X+50,pet.Y+50); pet.PointerMove(pet.X+200,300,4,4);
Check(pet.State==PetState.Dragging,"drag threshold transitions state");
pet.PointerUp(); Check(pet.State==PetState.Falling,"drop becomes fall, not click");
Advance(pet,3); Check(pet.Y==pet.Floor && pet.State!=PetState.Falling,"fall lands inside work area");
pet.PointerDown(pet.X+50,pet.Y+50); pet.PointerMove(600,200,4,4); pet.PointerUp();
pet.PointerDown(pet.X+50,pet.Y+50); pet.PointerUp(); Advance(pet,3);
Check(pet.Y==pet.Floor,"clicking during fall cannot strand pet in midair");
pet.SetPaused(true); var frozen=(pet.X,pet.Y,pet.AnimationTime); Advance(pet,10);
Check(frozen==(pet.X,pet.Y,pet.AnimationTime),"pause freezes animation and physics");
pet.PointerDown(pet.X+50,pet.Y+50); pet.PointerMove(800,200,4,4);
Check(pet.State==PetState.Dragging && pet.Y<pet.Floor,"paused pet still draggable");
pet.PointerUp(); Check(pet.Y==pet.Floor && pet.State==PetState.Idle,"paused drop settles without animation");
pet.SetPaused(false); pet.SetSleeping(true); Advance(pet,30);
Check(pet.State==PetState.Sleeping,"sleep suppresses roaming");
pet.PointerDown(pet.X+30,pet.Y+50);pet.PointerUp();
Check(!pet.Sleeping && pet.State==PetState.Reacting,"click wakes sleeping pet");
pet.SetSleeping(true); pet.PointerDown(pet.X+30,pet.Y+50); pet.PointerMove(900,200,4,4); pet.PointerUp(); Advance(pet,3);
Check(pet.State==PetState.Sleeping && pet.Y==pet.Floor,"sleep resumes after dragged landing");
pet.SetSleeping(false); pet.Hidden=true; frozen=(pet.X,pet.Y,pet.AnimationTime); Advance(pet,10);
Check(frozen==(pet.X,pet.Y,pet.AnimationTime),"hidden freezes behavior"); pet.Hidden=false;
pet.PointerDown(pet.X+40,pet.Y+40); pet.PointerMove(-10000,-10000,4,4);
Check(pet.X==0 && pet.Y==0,"drag clamps top and left");
pet.PointerMove(10000,10000,4,4); Check(pet.X==pet.MaxX && pet.Y==pet.Floor,"drag clamps right and bottom");
pet.CancelPointer(); Check(!pet.PointerHeld && pet.State==PetState.Idle,"capture loss clears dragging");
pet.Configure(new WorkArea(40,20,300,200),160,9999);
Check(pet.X==180 && pet.Y==60,"display change clamps saved location");
foreach(double scale in new[]{1.0,1.25,1.5,2.0})
{
    pet.Configure(new WorkArea(0,0,1920/scale,1040/scale),128,500);
    Advance(pet,30*60);
    Check(pet.X>=0 && pet.X<=pet.MaxX && pet.Y==pet.Floor,$"simulated 30-minute roaming at {scale*100}% geometry");
}
pet.Configure(new WorkArea(0,0,80,80),128); Check(pet.X==0 && pet.Y==0,"tiny work area does not throw");
var dir=Path.Combine(Path.GetTempPath(),"DittoCoreTests-"+Guid.NewGuid()); Directory.CreateDirectory(dir);
var path=Path.Combine(dir,"settings.json");
try
{
    Check(Settings.Load(path)==new Settings(),"missing settings use defaults");
    var settings=new Settings(Size:160,X:876.5);
    Check(settings.Save(path) && Settings.Load(path)==settings,"settings round trip");
    foreach(string broken in new[]{"broken", "null", "{\"Version\":2}", "{\"Version\":1,\"Size\":999}", "{\"Version\":1,\"Size\":128,\"X\":1e999}"})
    { File.WriteAllText(path,broken); Check(Settings.Load(path)==new Settings(),"invalid settings recover: "+broken); }
    Check(!settings.Save(dir),"unwritable target returns false instead of crashing");
}
finally { Directory.Delete(dir,true); }
pet=Pet();
pet.UpdateCursor(new CursorSnapshot(1500,300,true)); Advance(pet,3);
Check(pet.X>500 && pet.Y==pet.Floor,"lively follows horizontally at floor");
pet.SetActivity(ActivityMode.Quiet); double quietX=pet.X; Advance(pet,3);
Check(pet.X==quietX,"quiet disables autonomous follow");
pet.SetActivity(ActivityMode.Lively); pet.UpdateCursor(new CursorSnapshot(9999,0,false)); quietX=pet.X; Advance(pet,3);
Check(pet.X==quietX,"cursor outside primary does not attract pet");
pet.StartPlay(); pet.UpdateCursor(new CursorSnapshot(1100,250,true)); Advance(pet,4);
Check(pet.Y<pet.Floor && pet.Playing,"play allows two dimensional chase");
pet.EndPlay(); Advance(pet,3); Check(pet.Y==pet.Floor && !pet.Playing,"end play returns to bottom");
pet=Pet(); pet.StartPlay();
for(int i=0;i<1860;i++) { pet.UpdateCursor(new CursorSnapshot(pet.X<960?1850:20,0,true)); pet.Tick(1.0/30); }
Check(!pet.Playing,"distant office cursor motion does not keep play alive");
pet=Pet(); pet.SpawnBall(); Advance(pet,61);
Check(!pet.Playing && !pet.Ball.Visible,"autonomous ball bumps do not reset 60 second timer");
pet=Pet(); pet.StartPlay(); Advance(pet,50);
pet.PointerDown(pet.X+50,pet.Y+50); pet.PointerUp(); Advance(pet,20);
Check(pet.Playing,"explicit interaction renews play deadline");
pet.SetPaused(true); double idle=pet.IdlePlaySeconds; Advance(pet,70);
Check(pet.Playing && pet.IdlePlaySeconds==idle,"pause freezes play deadline");
pet.SetPaused(false); pet.MenuOpen=true; Advance(pet,70);
Check(pet.Playing && pet.IdlePlaySeconds==idle,"menu freezes play deadline"); pet.MenuOpen=false;
pet.SpawnBall(); pet.SetSleeping(true);
Check(!pet.Playing && !pet.Ball.Visible && pet.State==PetState.Sleeping && pet.Y==pet.Floor,"sleep dismisses ball and grounds pet");
pet=Pet(); pet.SpawnBall(); pet.SetActivity(ActivityMode.Quiet);
Check(!pet.Playing && !pet.Ball.Visible,"switching quiet ends play");
pet=Pet();
for(int i=0;i<3;i++) { pet.PointerDown(pet.X+50,pet.Y+50); pet.PointerUp(); Advance(pet,.2); }
Check(pet.Expressions.Emotion==Emotion.Hurt && pet.Expressions.Icon==Emote.Sweat,"rapid clicks produce temporary hurt expression");
Advance(pet,3); Check(pet.Expressions.Emotion==Emotion.Neutral && pet.Expressions.Icon==Emote.None,"hurt recovers without permanent penalty");
pet=Pet(); pet.SetActivity(ActivityMode.Quiet);
pet.UpdateCursor(new CursorSnapshot(pet.X+64,pet.Y+64,true)); Advance(pet,2);
Check(pet.State!=PetState.Petting,"stationary head hover is not petting");
foreach(int offset in new[]{40,64,84,64,40,64,84}) { pet.UpdateCursor(new CursorSnapshot(pet.X+offset,pet.Y+64,true)); pet.Tick(.04); }
Check(pet.State==PetState.Petting && pet.Expressions.Icon==Emote.Heart,"reversed head strokes create affection");
quietX=pet.X; Advance(pet,1); Check(pet.X==quietX,"petting freezes body movement");
pet.PointerDown(pet.X+40,pet.Y+64); pet.PointerMove(pet.X+100,pet.Y-100,4,4);
Check(pet.State==PetState.Dragging,"drag overrides petting"); pet.PointerUp();
var recognizer=new PettingRecognizer(); bool triggered=false;
for(int i=0;i<10;i++) triggered|=recognizer.Observe(i%2==0?.2:-.2,true,true,.04);
Check(!triggered,"mouse button excludes stroke recognition");
var ball=new BallPhysics(); ball.Configure(new WorkArea(0,0,1000,700),128); ball.Spawn(300,300);
ball.PointerDown(310,310,0); ball.PointerMove(700,200,.01); ball.PointerUp(false,.02);
Check(ball.VX==900 && ball.VY==-900,"throw speed capped per axis");
for(int i=0;i<600;i++) ball.Tick(1.0/30);
Check(ball.Y==ball.Floor && ball.VX==0 && ball.VY==0,"ball bounces then settles with friction");
ball.PointerDown(ball.X+10,ball.Y+10,30); ball.PointerMove(-10000,-10000,30.1); ball.PointerUp(true,30.2);
Check(ball.X==0 && ball.Y==ball.Floor && ball.VX==0,"paused ball drag clamps and settles");
ball.Spawn(100,100); ball.PointerDown(110,110,40); ball.PointerMove(200,200,40.1); ball.PointerUp(false,41);
Check(ball.VX==0 && ball.VY==0,"holding before release does not throw stale velocity");
ball.Spawn(100,100); ball.PointerDown(110,110,50); ball.PointerMove(9999,9999,50.1); ball.PointerUp(false,50.12); ball.Tick(.05);
Check(ball.X<=ball.MaxX && ball.Y<=ball.Floor,"ball collision stays inside work area");
pet=Pet(); pet.SpawnBall(); var firstBall=pet.Ball; pet.SpawnBall();
Check(ReferenceEquals(firstBall,pet.Ball),"repeated spawn reuses one ball");
pet.Configure(new WorkArea(40,20,300,200),160,9999);
Check(pet.Ball.X>=40 && pet.Ball.X<=pet.Ball.MaxX && pet.Ball.Y<=pet.Ball.Floor,"layout change also clamps ball");
var visibility=new VisibilityPolicy { Fullscreen=true };
Check(visibility.Hidden,"fullscreen hides"); visibility.ManualHidden=true; visibility.Fullscreen=false;
Check(visibility.Hidden,"leaving fullscreen respects manual hide"); visibility.Fullscreen=true; visibility.ManualHidden=false;
Check(visibility.Hidden,"manual reveal still respects active fullscreen"); visibility.Fullscreen=false;
Check(!visibility.Hidden,"fullscreen exit restores when not manually hidden");
foreach(double scale in new[]{1.0,1.25,1.5,2.0})
{
    var scaled=Pet(); scaled.Configure(new WorkArea(0,0,1920/scale,1040/scale),160,200);
    bool inside=true;
    for(int i=0;i<9000;i++)
    {
        if(i%300==0) scaled.SpawnBall();
        scaled.UpdateCursor(new CursorSnapshot((i%600)/600.0*scaled.Area.Width,100,true));
        scaled.Tick(1.0/30);
        inside&=scaled.X>=0 && scaled.X<=scaled.MaxX && scaled.Y>=0 && scaled.Y<=scaled.Floor
            && scaled.Ball.X>=0 && scaled.Ball.X<=scaled.Ball.MaxX && scaled.Ball.Y>=0 && scaled.Ball.Y<=scaled.Ball.Floor;
    }
    Check(inside,$"pet and ball remain bounded at {scale*100}% simulated geometry");
}
foreach(var activity in new[]{ActivityMode.Lively,ActivityMode.Quiet})
{
    var companion=Pet(); companion.SetActivity(activity);
    PetState? previousAction=null; int actions=0; bool repeated=false;
    for(int i=0;i<30000;i++)
    {
        var before=companion.State; companion.Tick(1.0/30);
        if(companion.State!=before && companion.State is PetState.Stretching or PetState.Yawning or PetState.Jumping)
        {
            repeated|=previousAction==companion.State; previousAction=companion.State; actions++;
        }
    }
    Check(actions>=10 && !repeated,$"{activity} autonomous gestures do not repeat consecutively");
}
Directory.CreateDirectory(dir);
try
{
    File.WriteAllText(path,"{\"Version\":1,\"Size\":96,\"X\":345}");
    Check(Settings.Load(path)==new Settings(Size:96,X:345),"v1 settings migrate without losing size or position");
    var v2=new Settings(Size:160,X:123,Activity:ActivityMode.Quiet); v2.Save(path);
    Check(Settings.Load(path)==v2,"v2 activity preference round trips");
    File.WriteAllText(path,"{\"Version\":99,\"Size\":96,\"X\":345}");
    Check(Settings.Load(path)==new Settings(),"unknown settings version uses defaults");
}
finally { Directory.Delete(dir,true); }
Console.WriteLine($"{passed} checks passed.");
