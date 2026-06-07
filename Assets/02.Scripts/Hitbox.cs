using System.Collections.Generic;
using UnityEngine;

public class Hitbox
{
    private readonly Transform _attacker;
    private readonly ActionTimelineRunner _runner;
    private readonly LayerMask _hurtboxMask;
    private readonly List<HitEvent> _events;
    private readonly Collider[] _results = new Collider[16];
    private readonly HashSet<int> _alreadyHit = new HashSet<int>();
    private int _instanceKey = -1;

    public Hitbox(Transform attacker, ActionTimelineRunner runner, LayerMask mask, List<HitEvent> sink)
    {
        _attacker = attacker;
        _runner = runner;
        _hurtboxMask = mask;
        _events = sink;
    }

    public void Tick()
    {
        if (!_runner.IsPlaying)
            return;
        var def = _runner.Current;
        int frame = _runner.CurrentFrame;

        for(int wi = 0; wi < def.HitWindows.Length; wi++)
        {
            var w = def.HitWindows[wi];
            if (frame < w.StartFrame || frame > w.EndFrame)
                continue;

            int key = _runner.PlayId * 100 + wi;
            if (key != _instanceKey)
            {
                _alreadyHit.Clear();
                _instanceKey = key;
            }

            Vector3 center = _attacker.position + _attacker.rotation * w.BoxOffset;
            Vector3 half = w.BoxSize * 0.5f;
            int n = Physics.OverlapBoxNonAlloc(center, half, _results, _attacker.rotation, _hurtboxMask, QueryTriggerInteraction.Collide);


            for (int i = 0; i < n; i++)
            {
                var hb = _results[i].GetComponent<Hurtbox>();
                if (hb == null || hb.Owner == null)
                    continue;
                int id = _results[i].GetInstanceID();
                if (!_alreadyHit.Add(id)) continue;

                _events.Add(new HitEvent
                {
                    Target = hb.Owner,
                    Info = new HitInfo
                    {
                        Damage = w.Damage,
                        HitstopFrames = w.HitstopFrames,
                        Launch = _attacker.rotation * w.Launch,
                        HitPoint = center
                    }
                });

                Debug.Log($"[Hitbox] HIT @frame {frame} (window {wi})");
            }
        }
    }
}