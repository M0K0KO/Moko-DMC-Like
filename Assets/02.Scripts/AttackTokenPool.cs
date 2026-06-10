using System.Collections.Generic;
using UnityEngine;

public class AttackTokenPool : MonoBehaviour
{
    [SerializeField] int totalTokens = 1;
    readonly HashSet<Component> holders = new();

    public bool TryAcquire(Component requester)
    {
        holders.RemoveWhere(h => h == null);
        if (holders.Contains(requester)) return true;
        if (holders.Count >= totalTokens) return false;

        holders.Add(requester);
        return true;
    }

    public void Release(Component requester) => holders.Remove(requester);
}