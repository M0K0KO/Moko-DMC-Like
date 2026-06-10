using System.Collections.Generic;
using UnityEngine;
public static class TargetRegistry
{
    private static readonly List<ITargetable> _targets = new List<ITargetable>();

    public static List<ITargetable> All => _targets;

    public static void Register(ITargetable t) { if (t != null && !_targets.Contains(t)) _targets.Add(t); }
    public static void Unregister(ITargetable t) => _targets.Remove(t);

    public static ITargetable FindBest(Vector3 origin, Vector3 forward, float maxRange, float coneDeg)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-4f)
            return null;
        forward.Normalize();
        float cosHalf = Mathf.Cos(coneDeg * 0.5f * Mathf.Deg2Rad);
        ITargetable best = null;
        float bestSqr = maxRange * maxRange;
        for (int i = 0; i < _targets.Count; i++)
        {
            var t = _targets[i];
            if (t == null || !t.IsValid) 
                continue;
            Vector3 to = t.Position - origin;
            to.y = 0f;
            float sqr = to.sqrMagnitude;
            if (sqr > bestSqr || sqr < 1e-4f)
                continue;
            if (Vector3.Dot(forward, to.normalized) < cosHalf)
                continue;
            bestSqr = sqr; best = t;
        }
        return best;
    }
}