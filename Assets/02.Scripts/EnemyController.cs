using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class EnemyController : MonoBehaviour, IDamageable, IHitstopReceiver, ITargetable
{
    [SerializeField] private LayerMask _playerMask;
    [SerializeField] Transform _aimPoint;

    [SerializeField] private Transform _target;
    [SerializeField] ActionDefinition _attackDef;
    [SerializeField] EnemyBrainConfig _brainCfg;


    public Transform AimPoint => _aimPoint;
    public Vector3 Position => transform.position;
    public bool IsValid => isActiveAndEnabled;


    private Animator _animator;
    private CharacterMotor _motor;
    private EnemyReaction _reaction;
    private AnimationDriver _animDriver;
    private Health _health;
    private int _hitstopFrames;

    private ActionTimelineRunner _attackRunner;
    private List<HitEvent> _hitEvents;
    private Hitbox _hitbox;
    private HitResolution _hitResolution;
    private EnemyAttackBrain _brain;

    private void OnEnable()
    {
        TargetRegistry.Register(this);
    }

    private void OnDisable()
    {
        TargetRegistry.Unregister(this);
    }


    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _motor = GetComponent<CharacterMotor>();
        _reaction = GetComponent<EnemyReaction>();
        _health = GetComponent<Health>();


        var ctx = new CombatContext();
        _attackRunner = new ActionTimelineRunner(_motor, ctx);
        _animDriver = new AnimationDriver(_animator, _attackRunner, _motor, ctx);

        _hitEvents = new List<HitEvent>();
        _hitbox = new Hitbox(transform, _attackRunner, _hitEvents, _playerMask);
        _hitResolution = new HitResolution(_hitEvents, attacker: this);
        _brain = new EnemyAttackBrain(transform, _target, _attackRunner, _attackDef, _motor, _brainCfg, _target.GetComponent<AttackTokenPool>());

        GetComponent<HitboxGizmo>().Runner = _attackRunner;
    }

    public bool TakeHit(in HitInfo info)
    {
        Debug.Log("HIT!");

        _health.ApplyDamage(info.Damage);
        _attackRunner.Stop();
        _reaction.OnHit(info);
        _hitstopFrames = info.HitstopFrames;

        return true;
    }

    private void FixedUpdate()
    {
        if (_hitstopFrames > 0) 
        { 
            _hitstopFrames--;
            _animator.speed = 0f;
            return;
        }
        _animator.speed = 1f;

        _reaction.Tick(Time.fixedDeltaTime);


        if (_reaction.State == EnemyState.Idle)
        {
            _brain.Tick(Time.fixedDeltaTime);
            _attackRunner.Advance();
            _attackRunner.Apply();
        }

        _motor.Tick(Time.fixedDeltaTime);
        _hitbox.Tick();
        _hitResolution.Tick();

        _animDriver.Tick();
        _animDriver.Apply();
    }

    public void ApplyHitStop(int frames)
    {
        _hitstopFrames = Mathf.Max(_hitstopFrames, frames);
    }
}