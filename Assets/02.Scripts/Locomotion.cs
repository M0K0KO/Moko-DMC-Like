using UnityEngine;

public enum LocoState
{
    Grounded,
    Airborne,
    Landing,
}

public class Locomotion
{
    private readonly InputReader _input;
    private readonly InputBuffer _inputBuffer;
    private readonly CharacterMotor _motor;
    private readonly CombatContext _ctx;
    private readonly Camera _cam;
    private const float Speed = 6f;
    private const float JumpSpeed = 10f;
    private const int LandingFrames = 6;
    private const float TurnLerp = 0.2f;

    private const float LockTurnLerp = 0.3f;

    private const int JumpLenience = 6;
    private int _landingEndFrame;

    public LocoState State { get; private set; } = LocoState.Grounded;


    public Locomotion(InputReader i, InputBuffer b, CharacterMotor m, CombatContext c, Camera cam)
    {
        _input = i;
        _inputBuffer = b;
        _motor = m;
        _ctx = c;
        _cam = cam;
    }

    public void Tick(int currentFrame)
    {
        UpdateState(currentFrame);
        _ctx.LocoState = State;

        if (_ctx.ActionState != ActionState.None)
            return;

        switch (State)
        {
            case LocoState.Grounded:
                GroundMove();
                if (_inputBuffer.TryConsume(InputId.Jump, currentFrame, JumpLenience))
                {
                    _motor.SetVerticalVelocity(JumpSpeed);
                    State = LocoState.Airborne;
                }
                break;

            case LocoState.Airborne:
                // air control
                break;

            case LocoState.Landing:
                _motor.SetHorizontalVelocity(Vector2.zero); // plant delay
                break;
        }
    }

    private void UpdateState(int frame)
    {
        switch (State)
        {
            case LocoState.Grounded:
                if (!_motor.IsGrounded) State = LocoState.Airborne;
                break;

            case LocoState.Airborne:
                if (_motor.IsGrounded)
                {
                    State = LocoState.Landing;
                    _landingEndFrame = frame + LandingFrames;
                }
                break;

            case LocoState.Landing:
                if (!_motor.IsGrounded) State = LocoState.Airborne;
                else if (frame >= _landingEndFrame) State = LocoState.Grounded;
                break;
        }
    }

    private void GroundMove()
    {
        if (_ctx.IsLockedOn && _ctx.CurrentTarget != null)
        {
            LockedGroundMove();
            return;
        }

        Vector2 mv = _input.MoveValue;
        Vector3 dir = new Vector3(mv.x, 0, mv.y);
        if (mv.sqrMagnitude > 0.0001f && _cam != null)
        {
            Vector3 camForward = _cam.transform.forward;
            Vector3 camRight = _cam.transform.right;
            camForward.y = 0f; camRight.y = 0f;
            camForward.Normalize(); camRight.Normalize();
            dir = camRight * mv.x + camForward * mv.y;
            if (dir.sqrMagnitude > 1f) dir.Normalize();
        }
        _motor.SetHorizontalVelocity(new Vector2(dir.x, dir.z) * Speed);
        if (dir.sqrMagnitude > 0.01f)
        {
            var t = _motor.transform;
            Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
            t.rotation = Quaternion.Slerp(t.rotation, target, TurnLerp);
        }
    }

    private void LockedGroundMove()
    {
        Vector2 mv = _input.MoveValue;
        Transform t = _motor.transform;

        Vector3 to = _ctx.CurrentTarget.Position - t.position;
        to.y = 0f;
        float dist = to.magnitude;
        if (dist < 0.05f) { _motor.SetHorizontalVelocity(Vector2.zero); return; }

        Vector3 fwd = to / dist;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);

        Quaternion target = Quaternion.LookRotation(fwd, Vector3.up);
        t.rotation = Quaternion.Slerp(t.rotation, target, LockTurnLerp);

        Vector3 vel = right * (mv.x * Speed) + fwd * (mv.y * Speed);
        _motor.SetHorizontalVelocity(new Vector2(vel.x, vel.z));
    }
}