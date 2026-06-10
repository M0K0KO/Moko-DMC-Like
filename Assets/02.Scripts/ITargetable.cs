
using UnityEngine;

public interface ITargetable
{
    Transform AimPoint { get; }
    Vector3 Position { get; }
    bool IsValid { get; }
}