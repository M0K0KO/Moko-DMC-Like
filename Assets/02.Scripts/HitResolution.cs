
using System.Collections.Generic;

public class HitResolution
{
    private readonly List<HitEvent> _events;
    private readonly IHitstopReceiver _attacker;

    public event System.Action<HitInfo> OnHit;

    public HitResolution(List<HitEvent> sink, IHitstopReceiver attacker)
    {
        _events = sink;
        _attacker = attacker;
    }

    public void Tick()
    {
        if (_events.Count == 0)
            return;

        int maxStop = 0;
        foreach(var e in _events)
        {
            if (!e.Target.TakeHit(e.Info)) 
                continue;

            OnHit?.Invoke(e.Info);

            if (e.Info.HitstopFrames > maxStop)
                maxStop = e.Info.HitstopFrames;
        }

        _attacker.ApplyHitStop(maxStop);
        _events.Clear();
    }

}