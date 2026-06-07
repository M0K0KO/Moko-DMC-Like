using System;

public class ActionResolver
{
    private readonly InputBuffer _buffer;
    private readonly ActionTimelineRunner _runner;
    private readonly CombatContext _ctx;
    private readonly ActionDefinition _nerutralAttack;

    private const int Lenience = 6;

    public ActionResolver(InputBuffer b, ActionTimelineRunner r, CombatContext c, ActionDefinition neutral)
    {
        _buffer = b;
        _runner = r;
        _ctx = c;
        _nerutralAttack = neutral;
    }

    public void Tick(int currentFrame)
    {
        // 1) cancel : playing + cancel window is open + buffered Attack -> CANCEL!
        if(_runner.IsPlaying
            && (_ctx.CancelFlags & CancelTag.AnyAttack) != 0
            && _runner.Current.NextOnAttack != null
            && _buffer.TryConsume(InputId.Attack, currentFrame, Lenience))
        {
            _runner.StartAction(_runner.Current.NextOnAttack);
            return;
        }

        // 2) neutral start : not on action + buffered Attack -> FIRST ATTACK
        if (_ctx.ActionState == ActionState.None
            && _buffer.TryConsume(InputId.Attack, currentFrame, Lenience))
        {
            _runner.StartAction(_nerutralAttack);
        }
    }
}