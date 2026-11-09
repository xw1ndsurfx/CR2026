namespace Intersect.Client.Entities;

internal sealed class NpcSoundTrigger
{
    private bool _inside;
    private bool _dead;
    private long _nextApproach;

    internal bool Approach(double distance, int radius, long now)
    {
        if (_dead || radius <= 0) return false;
        if (distance > radius + 1) _inside = false;
        if (_inside || distance > radius || now < _nextApproach) return false;
        _inside = true;
        _nextApproach = now + 5000;
        return true;
    }

    internal bool Death()
    {
        if (_dead) return false;
        _dead = true;
        return true;
    }
}
