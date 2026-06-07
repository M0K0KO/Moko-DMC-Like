using UnityEngine;

public enum ActionType
{
    None,
    Attack,
}

[System.Serializable]
public struct MotionImpulse
{
    public int StartFrame;
    public int EndFrame;
    public AnimationCurve VX;
    public AnimationCurve VY;
    public AnimationCurve VZ;
}

[System.Serializable]
public struct HitWindow
{
    public int StartFrame, EndFrame;
    public Vector3 BoxOffset, BoxSize;
    public int Damage, HitstopFrames;
    public Vector3 Launch;
}

[System.Serializable]
public struct CancelWindow 
{
    public int StartFrame, EndFrame; 
    public CancelTag AllowedInto; 
}

[System.Flags]
public enum CancelTag 
{ 
    None = 0, 
    AnyAttack = 1, 
    Dodge = 2 
}

[CreateAssetMenu(menuName = "Moko/Action Definition")]
public class ActionDefinition : ScriptableObject
{
    public ActionType Type = ActionType.Attack;
    public bool AllowGrounded = true;
    public bool AllowAir = false;
    public AnimationClip Clip;
    public int TotalFrames = 30;
    public ActionDefinition NextOnAttack;

    public HitWindow[] HitWindows;
    public CancelWindow[] CancelWindows;
    public MotionImpulse[] MotionImpulses;
}