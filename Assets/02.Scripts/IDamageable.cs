using UnityEngine;

public interface IDamageable
{
    void TakeHit(in HitInfo info);
}

public struct HitInfo
{
    public int Damage, HitstopFrames;
    public Vector3 Launch, HitPoint;
}

public struct HitEvent
{
    public IDamageable Target;
    public HitInfo Info;
}