using UnityEngine;

public interface IDamageable
{
    bool TakeHit(in HitInfo info);
}

public enum ReactionType
{
    Flinch,
    Launched,
    //Knockback
}

public struct HitInfo
{
    public int Damage, HitstopFrames;
    public ReactionType Reaction;
    public Vector3 Launch, HitPoint;
}

public struct HitEvent
{
    public IDamageable Target;
    public HitInfo Info;
}