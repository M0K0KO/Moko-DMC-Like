using UnityEngine;

public class ActionTimelineRunner
{
    private readonly CharacterMotor _motor;
    private readonly CombatContext _ctx;

    private ActionDefinition _def;
    private int _currentFrame;
    private bool _playing;
    private bool _justStarted;


    public int PlayId { get; private set; }
    public bool IsPlaying => _playing;
    public int CurrentFrame => _currentFrame;
    public ActionDefinition Current => _def;

    public ActionTimelineRunner(CharacterMotor motor, CombatContext ctx)
    {
        _motor = motor;
        _ctx = ctx;
    }

    public void StartAction(ActionDefinition def)
    {
        _def = def;
        _currentFrame = 0;
        _playing = true;
        _justStarted = true;
        PlayId++;
        _ctx.ActionState = ActionState.Attacking;
    }

    public void Advance()
    {
        if (_playing && !_justStarted)
            _currentFrame++;

        _ctx.CancelFlags = CancelTag.None;
        if (_playing)
        {
            foreach(var w in _def.CancelWindows)
            {
                if (_currentFrame >= w.StartFrame && _currentFrame <= w.EndFrame)
                    _ctx.CancelFlags |= w.AllowedInto;
            }
        }
    }

    public void Apply()
    {
        if (!_playing)
        {
            _justStarted = false;
            return;
        }

        Vector3 desiredLocal = Vector3.zero;
        foreach(var m in _def.MotionImpulses)
        {
            if (_currentFrame < m.StartFrame || _currentFrame > m.EndFrame) 
                continue;

            int span = Mathf.Max(m.EndFrame - m.StartFrame, 1);
            float t = (float)(_currentFrame - m.StartFrame) / span;

            desiredLocal += new Vector3(
                m.VX?.Evaluate(t) ?? 0f,
                m.VY?.Evaluate(t) ?? 0f,
                m.VZ?.Evaluate(t) ?? 0f);
        }

        Vector3 world = _motor.transform.rotation * desiredLocal;
        _motor.SetHorizontalVelocity(new Vector2(world.x, world.z));

        if (_currentFrame >= _def.TotalFrames)
        {
            _playing = false;
            _ctx.ActionState = ActionState.None;
        }
        _justStarted = false;
    }
}