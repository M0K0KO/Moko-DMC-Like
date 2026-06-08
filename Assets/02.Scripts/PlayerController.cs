using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Composition Root, Possess all subsystems, call Tick() with fixed order
/// </summary>
[DefaultExecutionOrder(0)]
public class PlayerController : MonoBehaviour, IHitstopReceiver
{
    [SerializeField] private LayerMask _hurtboxMask;

    [SerializeField] ActionDefinition _neutralDef;

    private InputReader _inputReader;
    private InputBuffer _inputBuffer;
    private CharacterMotor _motor;
    private ActionTimelineRunner _runner;
    private ActionResolver _resolver;
    private Locomotion _locomotion;
    private AnimationDriver _driver;
    private HitResolution _hitResolution;
    private CombatContext _ctx;

    private Animator _animator;
    private Camera _cam;

    private int _currentFrame;
    private int _hitstopFrames;

    private float _savedAnimSpeed = 1f;

    private List<HitEvent> _hitEvents;
    private Hitbox _hitbox;

    private const int BufferLenience = 6;

    private bool _debugPaused;
    private bool _stepOnce;

    private void Awake()
    {
        // setup subsystems, _ctx
        _ctx = new CombatContext();

        _inputReader = GetComponent<InputReader>();
        _inputBuffer = new InputBuffer();

        _motor = GetComponent<CharacterMotor>();

        _runner = new ActionTimelineRunner(_motor, _ctx);

        _resolver = new ActionResolver(_inputBuffer, _runner, _ctx, _neutralDef);

        _cam = Camera.main;
        _locomotion = new Locomotion(_inputReader, _inputBuffer, _motor, _ctx, _cam);

        _animator = GetComponent<Animator>();
        _driver = new AnimationDriver(_animator, _runner);

        _hitEvents = new List<HitEvent>();
        _hitbox = new Hitbox(transform, _runner, _hurtboxMask, _hitEvents);
        _hitResolution = new HitResolution(_hitEvents, this);

        GetComponent<HitboxGizmo>().Runner = _runner;

    }

    private void Update()
    {
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

        // 2) Runner.Advance : currentFrame++ & CancelFlags state update
        _runner.Advance();

        // 3) ActionResolver : start/cancel judging, frame0 + justStarted if it's new action
        _resolver.Tick(_currentFrame);

        // 4) Runner.Apply : fire a frame one-shot (MotionImpulse / Hitbox / Exit)
        _runner.Apply();

        // 5) Locomotion.Tick : ActionState Gate
        _locomotion.Tick(_currentFrame);

        // 6) CharacterMotor.Tick : Move Once
        _motor.Tick(Time.fixedDeltaTime);

        // 7) Hitbox.Tick : OverlapBox Query -> Fire Hit Event
        _hitbox.Tick();

        // 8) HitResolution : Damage, Launch, Hitstop Application
        _hitResolution.Tick();

        // 9) AnimationDriver.Apply : Frame / Parameter update
        _driver.Apply();
    }

    private void LateUpdate()
    {
        // target lock on, camera follow (after the transform has been determined)
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

        var r = _runner; var c = _ctx; var m = _motor;
        string s =
            $"ActionState : {c.ActionState}\n" +
            $"Action      : {(r.IsPlaying ? r.Current.name : "-")}\n" +
            $"Frame       : {r.CurrentFrame} / {(r.IsPlaying ? r.Current.TotalFrames : 0)}\n" +
            $"CancelFlags : {c.CancelFlags}\n" +
            $"Grounded    : {m.IsGrounded}\n" +
            $"Velocity    : {m.Velocity}\n" +
            $"timeScale   : {Time.timeScale}";
        GUI.Label(new Rect(10, 10, 800, 400), s, _style);
    }

    public void ApplyHitStop(int frames)
    {
        _hitstopFrames = Mathf.Max(_hitstopFrames, frames);
    }
}
