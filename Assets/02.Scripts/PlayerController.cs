using System;
using System.Collections.Generic;
using UnityEditor.ShaderGraph.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Composition Root, Possess all subsystems, call Tick() with fixed order
/// </summary>
[DefaultExecutionOrder(0)]
public class PlayerController : MonoBehaviour, IHitstopReceiver, IDamageable
{
    [SerializeField] CameraController _cameraController;
    [SerializeField] LockOnConfig _lockOnConfig = new();
    LockOnController _lockOn;

    [SerializeField] private LayerMask _enemyMask;

    [SerializeField] Weapon _melee;
    [SerializeField] Weapon _ranged;
    [SerializeField] ActionDefinition _dodgeDef;
    [SerializeField] ActionDefinition _launchedDef;
    [SerializeField] ActionDefinition _hitstunDef;

    public CombatContext Context => _ctx;
    public TargetProvider Targets => _targetProvider;

    private InputReader _inputReader;
    private InputBuffer _inputBuffer;
    private CharacterMotor _motor;
    private ActionTimelineRunner _runner;
    private ActionResolver _resolver;
    private Locomotion _locomotion;
    private AnimationDriver _driver;
    private HitResolution _hitResolution;
    private CombatContext _ctx;

    private TargetProvider _targetProvider;
    private int _lastPlayId;

    private Animator _animator;
    private Camera _cam;

    private int _currentFrame;
    private int _hitstopFrames;

    private float _savedAnimSpeed = 1f;

    private float _hp = 100f;

    private List<HitEvent> _hitEvents;
    private Hitbox _hitbox;

    private bool _debugPaused;
    private bool _stepOnce;

    private HitInfo _pendingHit;
    bool _hasPendingHit;

    private Vector3 _knockH;
    private const float GroundDrag = 50f;

    private void Awake()
    {
        // setup subsystems, _ctx
        _ctx = new CombatContext();
        _ctx.CurrentMelee = _melee;
        _ctx.CurrentRanged = _ranged;

        _inputReader = GetComponent<InputReader>();
        _inputBuffer = new InputBuffer();

        _motor = GetComponent<CharacterMotor>();

        _runner = new ActionTimelineRunner(_motor, _ctx);

        _resolver = new ActionResolver(_inputBuffer, _runner, _ctx, _dodgeDef);

        _cam = Camera.main;
        _locomotion = new Locomotion(_inputReader, _inputBuffer, _motor, _ctx, _cam);

        _animator = GetComponent<Animator>();
        _driver = new AnimationDriver(_animator, _runner, _motor, _ctx);

        _hitEvents = new List<HitEvent>();
        _hitbox = new Hitbox(transform, _runner, _hitEvents, _enemyMask);
        _hitResolution = new HitResolution(_hitEvents, this);

        _targetProvider = GetComponent<TargetProvider>();

        _lockOn = new LockOnController(Camera.main.transform, transform, _ctx, _targetProvider, _lockOnConfig);

        GetComponent<HitboxGizmo>().Runner = _runner;
    }

    private void Start()
    {
        Orchestrator.Instance.SetHitJuice(_hitResolution);
    }

    private void Update()
    {
        if (Mouse.current != null)
            _inputReader.AddLockSwitchDeltaX(Mouse.current.delta.ReadValue().x);

        if (Input.GetKeyDown(KeyCode.P)) _debugPaused = !_debugPaused;
        if (Input.GetKeyDown(KeyCode.RightBracket)) _stepOnce = true;
        if (Input.GetKeyDown(KeyCode.Alpha1)) Time.timeScale = 1f;
        if (Input.GetKeyDown(KeyCode.Alpha2)) Time.timeScale = 0.2f;
    }

    private void FixedUpdate()
    {
        if (_debugPaused && !_stepOnce) return;
        _stepOnce = false;

        if (_hitstopFrames > 0)
        {
            if (_animator.speed != 0f)
            {
                _savedAnimSpeed = _animator.speed;
                _animator.speed = 0f;
            }

            _hitstopFrames--;
            return;
        }

        if (_animator.speed == 0f)
        {
            _animator.speed = _savedAnimSpeed;
        }

        Tick();
        _currentFrame++;
    }

    void Tick()
    {
        // 1) InputBuffer : Queueing accumulated input edges into buffer
        if (_inputReader.ConsumeAttackEdge())
        {
            _inputBuffer.Push(InputId.Attack, _currentFrame);
        }
        if (_inputReader.ConsumeJumpEdge())
        {
            _inputBuffer.Push(InputId.Jump, _currentFrame);
        }
        if (_inputReader.ConsumeLauncherEdge())
        {
            _inputBuffer.Push(InputId.Launcher, _currentFrame);
        }
        if (_inputReader.ConsumeDodgeEdge())
        {
            _inputBuffer.Push(InputId.Dodge, _currentFrame);
        }

        // 2) Runner.Advance : currentFrame++ & CancelFlags state update
        _runner.Advance();

        // 3) ActionResolver : start/cancel judging, frame0 + justStarted if it's new action
        _resolver.Tick(_currentFrame);

        // 3.1) LockOnController
        _lockOn.Tick(_inputReader.ConsumeLockOn(), _inputReader.ConsumeLockSwitch());

        // 3.5) TargetProvider : Target update, target facing
        _targetProvider.Tick();
        _ctx.CurrentTarget = _targetProvider.CurrentTarget;
        if(_runner.PlayId != _lastPlayId)
        {
            _lastPlayId = _runner.PlayId;
            if (_ctx.ActionState == ActionState.Attacking && _ctx.LocoState == LocoState.Grounded)
                FaceTargetOnAttack();
        }

        // 4) Runner.Apply : fire a frame one-shot (MotionImpulse / Hitbox / Exit)
        _runner.Apply();
        if (_ctx.ActionState == ActionState.Hitstun)
        {
            _knockH = Vector3.MoveTowards(_knockH, Vector3.zero, GroundDrag * Time.fixedDeltaTime);
            _motor.SetHorizontalVelocity(new Vector2(_knockH.x, _knockH.z));
        }

        // 5) Locomotion.Tick : ActionState Gate
        _locomotion.Tick(_currentFrame);

        // 6) CharacterMotor.Tick : Move Once
        _motor.Tick(Time.fixedDeltaTime);

        // 7) Hitbox.Tick : OverlapBox Query -> Fire Hit Event
        _hitbox.Tick();

        // 8) HitResolution : Damage, Launch, Hitstop Application
        _hitResolution.Tick();

        // 9) AnimationDriver.Apply : Frame / Parameter update
        _driver.Tick();
        _driver.Apply();
    }

    private void FaceTargetOnAttack()
    {
        var tgt = _targetProvider.CurrentTarget;
        if (tgt == null || !tgt.IsValid) return;
        Vector3 to = tgt.Position - transform.position; to.y = 0f;
        if (to.sqrMagnitude < 1e-4f) return;
        transform.rotation = Quaternion.LookRotation(to, Vector3.up);
    }

    private void LateUpdate()
    {
        // target lock on, camera follow (after the transform has been determined)
        _cameraController.UpdateLockOnCamera();
    }


    private GUIStyle _style;
    void OnGUI()
    {
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label);
            _style.fontSize = 24;
            _style.normal.textColor = Color.white;
        }

        var r = _runner; var c = _ctx; var m = _motor; var l = _locomotion;
        string s =
            $"Invuln      : {c.Invulnerable}\n" +
            $"ActionState : {c.ActionState}\n" +
            $"LocoState   : {(l.State)}\n" +
            $"Action      : {(r.IsPlaying ? r.Current.name : "-")}\n" +
            $"Frame       : {r.CurrentFrame} / {(r.IsPlaying ? r.Current.TotalFrames : 0)}\n" +
            $"CancelFlags : {c.CancelFlags}\n" +
            $"Grounded    : {m.IsGrounded}\n" +
            $"Velocity    : {m.Velocity}\n" +
            $"timeScale   : {Time.timeScale}\n" +
            $"IsLockedOn  : {_ctx.IsLockedOn}\n" +
            $"Pinned      : {(_targetProvider.Pinned as Component)?.name ?? "-"}\n" +
            $"Current     : {(_targetProvider.CurrentTarget as Component)?.name ?? "-"}";

        GUI.Label(new Rect(10, 10, 800, 600), s, _style);
    }

    public void ApplyHitStop(int frames)
    {
        _hitstopFrames = Mathf.Max(_hitstopFrames, frames);
    }

    public bool TakeHit(in HitInfo info)
    {
        if (_ctx.Invulnerable)
        {
            Debug.Log("INVULNERABLE!");
            return false;
        }

        _hp -= info.Damage;
        _hitstopFrames = Mathf.Max(_hitstopFrames, info.HitstopFrames);
        _knockH = new Vector3(info.Launch.x, 0f, info.Launch.z);

        if (info.Reaction == ReactionType.Launched)
        {
            _runner.StartAction(_launchedDef, ActionState.Launched);
            _motor.SetVerticalVelocity(info.Launch.y);
        }
        else
        {
            _runner.StartAction(_hitstunDef, ActionState.Hitstun);
        }
        return true;
    }
}
