using UnityEngine;

public class AnimationDriver
{
    private readonly Animator _animator;
    private readonly ActionTimelineRunner _runner;
    private readonly CharacterMotor _motor;
    private readonly CombatContext _ctx;

    private ActionDefinition _lastSeen;

    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
    private static readonly int LocomotionHash = Animator.StringToHash("Locomotion");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int FallHash = Animator.StringToHash("Fall");
    private static readonly int LandHash = Animator.StringToHash("Land");

    private const float SpeedDamp = 0.1f;
    private int _current;

    public AnimationDriver(Animator a, ActionTimelineRunner r, CharacterMotor m, CombatContext c)
    {
        _animator = a; 
        _runner = r;
        _motor = m;
        _ctx = c;
    }

    public void Tick()
    {
        if (_ctx.ActionState != ActionState.None)
        {
            _current = 0;
            return;
        }

        Vector3 vLocal = _motor.transform.InverseTransformDirection(
                             new Vector3(_motor.Velocity.x, 0f, _motor.Velocity.z));
        _animator.SetFloat(MoveXHash, vLocal.x, SpeedDamp, Time.fixedDeltaTime);
        _animator.SetFloat(MoveZHash, vLocal.z, SpeedDamp, Time.fixedDeltaTime);

        int target = SelectState();
        if (target != _current)
        {
            _animator.CrossFadeInFixedTime(target, 0.1f, 0);
            _current = target;
        }
    }
    private int SelectState()
    {
        switch (_ctx.LocoState)
        {
            case LocoState.Landing: return LandHash;
            case LocoState.Airborne: return _motor.VerticalVelocity > 0f ? JumpHash : FallHash;
            default: return LocomotionHash;
        }
    }

    public void Apply()
    {
        ActionDefinition cur = _runner.IsPlaying ? _runner.Current : null;
        if (cur != _lastSeen)
        {
            if (cur != null && cur.Clip != null)
            {
                float clipFrames = cur.Clip.length * 60f;
                _animator.speed = clipFrames / Mathf.Max(cur.TotalFrames, 1);
                _animator.CrossFadeInFixedTime(cur.Clip.name, 0.02f, 0);
            }
            else
            {
                _animator.speed = 1f;
            }

            _lastSeen = cur;
        }
    }
}