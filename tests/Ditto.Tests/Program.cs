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
Console.WriteLine($"{passed} checks passed.");
