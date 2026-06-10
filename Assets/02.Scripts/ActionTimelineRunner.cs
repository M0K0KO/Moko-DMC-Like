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

    public void StartAction(ActionDefinition def, ActionState state)
    {
        _def = def;
        _currentFrame = 0;
        _playing = true;
        _justStarted = true;
        PlayId++;
        _ctx.ActionState = state;
    }

    public void Advance()
    {
        if (_playing && !_justStarted)
            _currentFrame++;

        _ctx.CancelFlags = CancelTag.None;
        _ctx.Invulnerable = false;
        if (_playing)
        {
            foreach(var w in _def.CancelWindows)
            {
                if (_currentFrame >= w.StartFrame && _currentFrame <= w.EndFrame)
                    _ctx.CancelFlags |= w.AllowedInto;
            }
            foreach (var iv in _def.InvulnWindows)
            {
                if (_currentFrame >= iv.StartFrame && _currentFrame <= iv.EndFrame)
                {
                    _ctx.Invulnerable = true;
                    break;
                }
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
        bool verticalDriven = false;
        float vy = 0;

        foreach (var m in _def.MotionImpulses)
        {
            if (_currentFrame < m.StartFrame || _currentFrame > m.EndFrame)
                continue;

            int span = Mathf.Max(m.EndFrame - m.StartFrame, 1);
            float t = (float)(_currentFrame - m.StartFrame) / span;

            desiredLocal.x += m.VX?.Evaluate(t) ?? 0f;
            desiredLocal.z += m.VZ?.Evaluate(t) ?? 0f;

            if (m.DrivesVertical)
            {
                vy += m.VY?.Evaluate(t) ?? 0f;
                verticalDriven = true;
            }
        }

        Vector3 world = _motor.transform.rotation * desiredLocal;
        bool reaction = _ctx.ActionState == ActionState.Hitstun || _ctx.ActionState == ActionState.Launched;
        if (!reaction)
            _motor.SetHorizontalVelocity(new Vector2(world.x, world.z));
        if (verticalDriven)
            _motor.SetVerticalVelocity(vy);

        if (_currentFrame >= _def.TotalFrames)
        {
            _playing = false;
            _ctx.ActionState = ActionState.None;
        }
        _justStarted = false;
    }

    public void Stop()
    {
        _playing = false;
        _justStarted = false;
        _currentFrame = 0;
        _ctx.ActionState = ActionState.None;
    }
}