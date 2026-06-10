using UnityEngine;

public enum EnemyState
{
    Idle,
    Flinch,
    Launched,
    Knockoff
}

public class EnemyReaction : MonoBehaviour
{
    [SerializeField] private CharacterMotor _motor;
    [SerializeField] private Animator _animator;
    [SerializeField] private float _juggleGravityScale = 0.5f;
    [SerializeField] private int _flinchFrames = 12;
    [SerializeField] private int _knockoffFrames = 30;

    private EnemyState _state = EnemyState.Idle;
    private float _juggleVy;
    private int _timer;


    private Vector3 _knockH;
    const float GroundDrag = 50f;
    const float AirDrag = 6f;

    public EnemyState State => _state;

    public void OnHit(in HitInfo info)
    {
        if (info.Reaction == ReactionType.Launched)
        {
            _knockH = new Vector3(info.Launch.x, 0f, info.Launch.z);
            _juggleVy = info.Launch.y;
            _state = EnemyState.Launched;
            _animator.CrossFadeInFixedTime("Hit_Air_2", 0f, 0);
        }
        else
        {
            if (_state == EnemyState.Launched) return;
            _knockH = new Vector3(info.Launch.x, 0f, info.Launch.z);
            _state = EnemyState.Flinch;
            _timer = _flinchFrames;
            _animator.CrossFadeInFixedTime("Hit_F", 0f, 0);
        }
    }

    public void Tick(float dt)
    {
        switch (_state)
        {
            case EnemyState.Flinch:
                if (--_timer <= 0) { _state = EnemyState.Idle; _animator.CrossFadeInFixedTime("Locomotion", 0.2f, 0); }
                break;

            case EnemyState.Launched:
                _juggleVy += (CharacterMotor.Gravity * _juggleGravityScale) * dt;
                _motor.SetVerticalVelocity(_juggleVy);
                if (_motor.IsGrounded && _juggleVy <= 0f)
                {
                    _state = EnemyState.Knockoff;
                    _timer = _knockoffFrames;
                    _animator.CrossFadeInFixedTime("Hit_Heavy_End", 0f, 0);
                }
                break;

            case EnemyState.Knockoff:
                if (--_timer <= 0) { _state = EnemyState.Idle; _animator.CrossFadeInFixedTime("Locomotion", 0.2f, 0); }
                break;
        }

        if (_state != EnemyState.Idle)
        {
            float drag = (_state == EnemyState.Launched) ? AirDrag : GroundDrag;
            _knockH = Vector3.MoveTowards(_knockH, Vector3.zero, drag * dt);
            _motor.SetHorizontalVelocity(_knockH);
        }
    }
}