using UnityEngine;

public class Locomotion
{
    private readonly InputReader _input;
    private readonly CharacterMotor _motor;
    private readonly CombatContext _ctx;
    private const float Speed = 6f;
    private const float TurnLerp = 0.2f;

    public Locomotion(InputReader i, CharacterMotor m, CombatContext c)
    {
        _input = i;
        _motor = m;
        _ctx = c;
    }

    public void Tick()
    {
        if (_ctx.ActionState != ActionState.None)
            return;

        Vector2 mv = _input.MoveValue;
        Vector3 dir = new Vector3(mv.x, 0, mv.y) * Speed;

        _motor.SetHorizontalVelocity(new Vector2(dir.x, dir.z));

        if (dir.sqrMagnitude > 0.01f)
        {
            var t = _motor.transform;
            Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
            t.rotation = Quaternion.Slerp(t.rotation, target, TurnLerp);
        }
    }
}