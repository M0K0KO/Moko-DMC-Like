using UnityEngine;

[System.Serializable]
public struct EnemyBrainConfig
{
    public float attackRange;
    public float attackCooldown;
    public float preferredDist;
    public float approachSpeed;
    public float strafeSpeed;
    public float radialGain;
    public float maxSpeed;
    public float accel;
    public float turnRate;
    public float strafeFlipMin; 
    public float strafeFlipMax;
}

public class EnemyAttackBrain
{
    private readonly Transform _self;
    private readonly Transform _target;
    private readonly ActionTimelineRunner _runner;
    private readonly ActionDefinition _attackDef;
    private readonly CharacterMotor _motor;
    private readonly EnemyBrainConfig _cfg;
    private readonly AttackTokenPool _pool;

    private float _cooldownTimer;
    private Vector3 _vel;
    private float _strafeDir = 1f;
    private float _flipTimer;
    private bool _hasToken;

    public EnemyAttackBrain(Transform self, Transform target, ActionTimelineRunner runner,
                            ActionDefinition attackDef, CharacterMotor motor, EnemyBrainConfig cfg, AttackTokenPool pool)
    {
        _self = self; _target = target; _runner = runner;
        _attackDef = attackDef; _motor = motor; _cfg = cfg;
        _pool = pool;
        ResetFlipTimer();
    }

    private bool CanAttack => _cooldownTimer <= 0f;

    public void Tick(float dt)
    {
        bool playingOurAttack = _runner.IsPlaying && _runner.Current == _attackDef;
        if (_hasToken && !playingOurAttack)
        {
            _pool.Release(_self);
            _hasToken = false;
        }

        if (_runner.IsPlaying)
            return;

        if (_cooldownTimer > 0f)
            _cooldownTimer -= dt;

        Vector3 toTarget = _target.position - _self.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;
        Vector3 dir = dist > Mathf.Epsilon ? toTarget / dist : _self.forward;
        bool inRange = dist <= _cfg.attackRange;

        if (CanAttack && inRange && _pool.TryAcquire(_self))
        {
            SnapFace(dir);
            _vel = Vector3.zero;
            _runner.StartAction(_attackDef, ActionState.Attacking);
            _cooldownTimer = _cfg.attackCooldown;
            _hasToken = true;
            return;
        }

        Vector3 desired;
        if (CanAttack)
        {
            desired = dir * _cfg.approachSpeed;
        }
        else
        {
            TickStrafeFlip(dt);
            Vector3 radial = dir * (dist - _cfg.preferredDist) * _cfg.radialGain;
            Vector3 tangent = Vector3.Cross(Vector3.up, dir) * (_cfg.strafeSpeed * _strafeDir);
            desired = Vector3.ClampMagnitude(radial + tangent, _cfg.maxSpeed);
        }

        _vel = Vector3.MoveTowards(_vel, desired, _cfg.accel * dt);
        _motor.SetHorizontalVelocity(_vel);
        SmoothFace(dir, dt);
    }

    private void TickStrafeFlip(float dt)
    {
        _flipTimer -= dt;
        if (_flipTimer <= 0f) { _strafeDir = -_strafeDir; ResetFlipTimer(); }
    }
    private void ResetFlipTimer() => _flipTimer = Random.Range(_cfg.strafeFlipMin, _cfg.strafeFlipMax);

    private void SmoothFace(Vector3 dir, float dt)
    {
        if (dir.sqrMagnitude < Mathf.Epsilon) return;
        _self.rotation = Quaternion.RotateTowards(_self.rotation, Quaternion.LookRotation(dir), _cfg.turnRate * dt);
    }
    private void SnapFace(Vector3 dir)
    {
        if (dir.sqrMagnitude < Mathf.Epsilon) return;
        _self.rotation = Quaternion.LookRotation(dir);
    }
}