using UnityEngine;

public class Locomotion
{
    private readonly InputReader _input;
    private readonly InputBuffer _inputBuffer;
    private readonly CharacterMotor _motor;
    private readonly CombatContext _ctx;
    private readonly Camera _cam;
    private const float Speed = 6f;
    private const float JumpSpeed = 6f;
    private const float TurnLerp = 0.2f;

    private const int JumpLenience = 6;

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
        if (_ctx.ActionState != ActionState.None)
            return;

        if (_motor.IsGrounded)
        {
            Vector2 mv = _input.MoveValue;
            Vector3 dir = new Vector3(mv.x, 0, mv.y);

            if (mv.sqrMagnitude > 0.0001f && _cam != null)
            {
                Vector3 camForward = _cam.transform.forward;
                Vector3 camRight = _cam.transform.right;

                camForward.y = 0f;
                camRight.y = 0f;

                camForward.Normalize();
                camRight.Normalize();

                dir = camRight * mv.x + camForward * mv.y;

                if (dir.sqrMagnitude > 1f)
                    dir.Normalize();
            }

            _motor.SetHorizontalVelocity(new Vector2(dir.x, dir.z) * Speed);

            if (dir.sqrMagnitude > 0.01f)
            {
                var t = _motor.transform;
                Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
                t.rotation = Quaternion.Slerp(t.rotation, target, TurnLerp);
            }

            if (_inputBuffer.TryConsume(InputId.Jump, currentFrame, JumpLenience))
                _motor.SetVerticalVelocity(JumpSpeed);
        }
    }
}