using UnityEngine;

public class RangedFirer
{
    private readonly ActionTimelineRunner _runner;
    private readonly Transform _owner;
    private readonly LayerMask _targetMask;
    private int _firedPlayId = -1;
    private int _firedMask;

    public RangedFirer(ActionTimelineRunner runner, Transform owner, LayerMask targetMask)
    {
        _runner = runner;
        _owner = owner;
        _targetMask = targetMask;
    }

    public void Tick(int currentFrame)
    {
        if (!_runner.IsPlaying || _runner.Current == null) 
            return;

        var fires = _runner.Current.RangedFires;

        if (fires == null || fires.Length == 0) 
            return;

        if (_runner.PlayId != _firedPlayId)
        { 
            _firedPlayId = _runner.PlayId;
            _firedMask = 0; 
        }

        for (int i = 0; i < fires.Length && i < 32; i++)
        {
            int bit = 1 << i;
            if ((_firedMask & bit) != 0) continue;
            if (currentFrame < fires[i].Frame) continue;
            Fire(fires[i]);
            _firedMask |= bit;
        }
    }

    private void Fire(RangedFire f)
    {
        Vector3 origin = _owner.position + _owner.rotation * f.LocalOffset;
        Quaternion rot = _owner.rotation;

        if (f.Mode == RangedMode.Hitscan) FireHitscan(f, origin, rot);
        else FireProjectile(f, origin, rot);
    }

    private void FireHitscan(RangedFire f, Vector3 origin, Quaternion rot)
    {
        Vector3 dir = rot * Vector3.forward;
        bool hit = f.CastRadius > 0f
            ? Physics.SphereCast(origin, f.CastRadius, dir, out var rh, f.Range, _targetMask, QueryTriggerInteraction.Collide)
            : Physics.Raycast(origin, dir, out rh, f.Range, _targetMask, QueryTriggerInteraction.Collide);

        Debug.Log($"[HITSCAN] Hit : {hit}");
        if (hit)
            Debug.Log($"[HITSCAN] hit={rh.collider.name} " +
                      $"layer={LayerMask.LayerToName(rh.collider.gameObject.layer)} " +
                      $"hurtbox={(rh.collider.GetComponent<Hurtbox>() != null)}");


        if (!hit) return;

        var hb = rh.collider.GetComponent<Hurtbox>();
        if (hb == null || hb.Owner == null) return;

        var info = new HitInfo
        {
            Damage = f.Damage,
            HitstopFrames = f.HitstopFrames,
            Reaction = f.Reaction,
            Launch = rot * f.Launch,
            HitPoint = rh.point
        };
        if (hb.Owner.TakeHit(info)) CombatFeedback.Raise(info);
    }

    private void FireProjectile(RangedFire f, Vector3 origin, Quaternion rot)
    {
        if (f.Prefab == null) return;
        var p = Object.Instantiate(f.Prefab, origin, rot);
        p.Init(_targetMask, f.Damage, f.HitstopFrames, f.Reaction, f.Launch);
    }
}