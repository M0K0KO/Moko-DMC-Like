using UnityEngine;

public static class CombatFeedback
{
    public static event System.Action<HitInfo> OnHit;
    public static void Raise(in HitInfo info) => OnHit?.Invoke(info);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => OnHit = null;
}