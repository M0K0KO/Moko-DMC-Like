using UnityEngine;
public class TargetProvider : MonoBehaviour
{
    [SerializeField] private float _maxRange = 6f;
    [SerializeField] private float _coneDeg = 120f;
    public ITargetable CurrentTarget { get; private set; }
    public ITargetable Pinned { get; set; }

    public void Tick()
    {
        if (Pinned != null && Pinned.IsValid) { CurrentTarget = Pinned; return; }
        CurrentTarget = TargetRegistry.FindBest(transform.position, transform.forward, _maxRange, _coneDeg);
    }
}