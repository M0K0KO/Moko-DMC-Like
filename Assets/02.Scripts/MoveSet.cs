using UnityEngine;

public enum DirCondition
{
    Any, Neutral, Forward, Back, Left, Right
}

public enum LocoCondition
{
    Any, Grounded, Airborne
}

public enum LockCondition
{
    Any, Locked, Unlocked
}

[System.Serializable]
public class MoveEntry
{
    public InputId Trigger = InputId.Attack;
    public DirCondition Dir = DirCondition.Any;
    public LocoCondition Loco = LocoCondition.Any;
    public LockCondition Lock = LockCondition.Any;
    public ActionDefinition FromMove; // null = neutral, combo link
    public ActionDefinition Result; // action to perform
}

[CreateAssetMenu(menuName = "Moko/Move Set")]
public class MoveSet : ScriptableObject
{
    public MoveEntry[] Entries;
}