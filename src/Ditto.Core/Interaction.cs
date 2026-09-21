namespace Ditto.Core;

public enum ActivityMode { Lively, Quiet }
public enum Emotion { Neutral, Happy, Hurt, Surprised, Affection }
public enum Emote { None, Heart, Question, Sweat }
public readonly record struct CursorSnapshot(double X, double Y, bool InPrimary, bool ButtonsDown = false);

public sealed class Expressions
{
    private double remaining;
    public Emotion Emotion { get; private set; }
    public Emote Icon { get; private set; }
    public void Show(Emotion emotion, Emote icon, double seconds = 2)
    { Emotion = emotion; Icon = icon; remaining = seconds; }
    public void Tick(double dt) { if ((remaining -= dt) <= 0) Clear(); }
    public void Clear() { remaining = 0; Emotion = Emotion.Neutral; Icon = Emote.None; }
}

public sealed class PettingRecognizer
{
    private double travel, elapsed;
    private int direction, reversals;
    public bool Observe(double dx, bool inHead, bool buttons, double dt)
    {
        if (!inHead || buttons || (elapsed += dt) > 1.4) { Reset(); return false; }
        if (Math.Abs(dx) < .01) return false;
        int next = Math.Sign(dx);
        if (direction != 0 && next != direction) reversals++;
        direction = next; travel += Math.Abs(dx);
        if (reversals >= 2 && travel >= .3) { Reset(); return true; }
        return false;
    }
    public void Reset() { travel = elapsed = 0; direction = reversals = 0; }
}

public sealed class VisibilityPolicy
{
    public bool ManualHidden { get; set; }
    public bool Fullscreen { get; set; }
    public bool Hidden => ManualHidden || Fullscreen;
}
