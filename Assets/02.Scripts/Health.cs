using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private int _hp = 100;
    [SerializeField] private Animator _animator;
    private const int FlinchFrames = 15;

    private int _hitStop;
    private int _flinch;

    public void TakeHit(in HitInfo info)
    {
        _hp -= info.Damage;
        _hitStop = info.HitstopFrames;
        _flinch = FlinchFrames;
        Debug.Log($"[Enemy] dmg = {info.Damage} hp = {_hp} histop = {_hitStop}");

        // TODO : Directional Hit

        _animator.CrossFadeInFixedTime("Hit_F", 0f, 0);
    }

    private void FixedUpdate()
    {
        if (_hitStop > 0)
        {
            if (_animator)
                _animator.speed = 0f;
            _hitStop--;
            return;
        }

        if (_animator)
        {
            _animator.speed = 1f;
        }

        if (_flinch > 0)
        {
            _flinch--;
            if (_flinch == 0)
            {
                _animator.CrossFadeInFixedTime("Idle", 0.2f, 0);
            }
        }
    }
}