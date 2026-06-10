using UnityEngine;

[System.Serializable]
public class LockOnConfig
{
    public float acquireCone = 80f;
    public float acquireRange = 20f;
    public float releaseRange = 25f;
    public float flickThreshold = 50f;
    public float rearmThreshold = 10f;
}

public class LockOnController
{
    readonly Transform cam;
    readonly Transform self;
    readonly CombatContext ctx;
    readonly TargetProvider targets;
    readonly LockOnConfig cfg;

    private bool flickArmed = true;

    public LockOnController(Transform cam, Transform self, CombatContext ctx, TargetProvider targets, LockOnConfig cfg)
    {
        this.cam = cam;
        this.self = self;
        this.ctx = ctx;
        this.targets = targets;
        this.cfg = cfg;
    }

    public void Tick(bool lockPressed, float switchDeltaX)
    {
        if (lockPressed)
        {
            if (ctx.IsLockedOn)
                Release();
            else
                TryAcquire();
        }

        if (ctx.IsLockedOn)
        {
            HandleSwitch(switchDeltaX);
            ValidateOrRetarget();
        }
        else flickArmed = true;
    }

    void TryAcquire()
    {
        var t = TargetRegistry.FindBest(self.position, self.forward, cfg.acquireCone, cfg.acquireRange);
        if (t == null)
            return;

        targets.Pinned = t;
        ctx.IsLockedOn = true;
    }

    void ValidateOrRetarget()
    {
        var p = targets.Pinned;
        if (p == null || !p.IsValid)
        {
            var next = TargetRegistry.FindBest(self.position, self.forward, cfg.acquireCone, cfg.acquireRange);
            if (next != null)
                targets.Pinned = next;
            else
                Release();

            return;
        }

        if (DistXZ(self.position, p.Position) > cfg.releaseRange)
            Release();
    }

    void Release()
    {
        targets.Pinned = null;
        ctx.IsLockedOn = false;
    }

    void HandleSwitch(float dx)
    {
        if (flickArmed && Mathf.Abs(dx) >= cfg.flickThreshold)
        {
            SwitchTarget(dx > 0f ? 1 : -1);
            flickArmed = false;
        }
        else if (!flickArmed && Mathf.Abs(dx) < cfg.rearmThreshold)
        {
            flickArmed = true;
        }
    }

    void SwitchTarget(int dir)
    {
        var current = targets.Pinned;
        if (current == null) return;
        Vector3 camRight = cam.right;
        Vector3 cp = current.Position;

        ITargetable best = null;
        float bestOffset = float.MaxValue;
        foreach (var t in TargetRegistry.All)
        {
            if (t == current || !t.IsValid) continue;
            float screenX = Vector3.Dot(t.Position - cp, camRight);
            if (Mathf.Sign(screenX) != dir) continue;
            float offset = Mathf.Abs(screenX);
            if (offset < bestOffset) { bestOffset = offset; best = t; }
        }
        if (best != null) targets.Pinned = best;
    }

    static float DistXZ(Vector3 a, Vector3 b)
    {
        a.y = b.y = 0f;
        return Vector3.Distance(a, b);
    }
}