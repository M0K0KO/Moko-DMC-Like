using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private int _maxHp = 100;
    private int _hp;
    public bool IsDead => _hp <= 0;
    public event Action Died;

    void Awake() => _hp = _maxHp;
    public void ApplyDamage(int dmg)
    {
        _hp -= dmg;
        if (_hp <= 0) Died?.Invoke();
    }
}