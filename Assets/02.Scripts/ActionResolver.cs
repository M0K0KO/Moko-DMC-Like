using System;

public class ActionResolver
{
    private readonly InputBuffer _buffer;
    private readonly ActionTimelineRunner _runner;
    private readonly CombatContext _ctx;
    private readonly ActionDefinition _dodge;
    private const int Lenience = 6;

    public ActionResolver(InputBuffer b, ActionTimelineRunner r, CombatContext c, ActionDefinition dodge)
    {
        _buffer = b;
        _runner = r;
        _ctx = c;
        _dodge = dodge;
    }

    public void Tick(int currentFrame)
    {
        // 1) cancel : playing + cancel window is open + buffered Attack -> CANCEL!
        if (_runner.IsPlaying && (_ctx.CancelFlags & CancelTag.AnyAttack) != 0)
        {
            if (TryScan(_ctx.CurrentMelee, currentFrame, cancelPhase: true)) return;
            if (TryScan(_ctx.CurrentRanged, currentFrame, cancelPhase: true)) return;
        }

        if (_runner.IsPlaying
            && _ctx.LocoState == LocoState.Grounded
            && (_ctx.CancelFlags & CancelTag.Dodge) != 0
            && _buffer.TryConsume(InputId.Dodge, currentFrame, Lenience))
        {
            _runner.StartAction(_dodge, ActionState.Dodging);
            return;
        }

        // 2) neutral start : not on action + buffered Attack -> FIRST ATTACK
        if (_ctx.ActionState == ActionState.None)
        {
            // 2a) dodge (grounded)
            if (_ctx.LocoState == LocoState.Grounded
                && _buffer.TryConsume(InputId.Dodge, currentFrame, Lenience))
            {
                _runner.StartAction(_dodge, ActionState.Dodging);
                return;
            }

            if (TryScan(_ctx.CurrentMelee, currentFrame, cancelPhase: false)) return;
            if (TryScan(_ctx.CurrentRanged, currentFrame, cancelPhase: false)) return;
        }
    }

    private bool TryScan(Weapon weapon, int frame, bool cancelPhase)
    {
        var ms = weapon != null ? weapon.MoveSet : null;
        var entries = ms != null ? ms.Entries : null;
        if (entries == null) return false;

        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (cancelPhase)
            {
                if (e.FromMove == null) continue;
                if (_runner.Current != e.FromMove) continue;
            }
            else
            {
                if (e.FromMove != null) continue;
            }
            if (!MatchLoco(e.Loco)) continue;
            if (!MatchLock(e.Lock)) continue;
            if (!MatchDir(e.Dir)) continue;
            if (_buffer.TryConsume(e.Trigger, frame, Lenience))
            {
                _runner.StartAction(e.Result, ActionState.Attacking);
                return true;
            }
        }
        return false;
    }
    private bool MatchDir(DirCondition dir)
    {
        // ÀÏ´Ü
        return dir == DirCondition.Any;
    }

    private bool MatchLock(LockCondition @lock)
    {
        switch(@lock)
        {
            case LockCondition.Locked:
                return _ctx.IsLockedOn;
            case LockCondition.Unlocked:
                return !_ctx.IsLockedOn;
            default:
                return true;
        }
    }

    private bool MatchLoco(LocoCondition loco)
    {
        switch (loco)
        {
            case LocoCondition.Grounded: return _ctx.LocoState == LocoState.Grounded;
            case LocoCondition.Airborne: return _ctx.LocoState == LocoState.Airborne;
            default: return true; // Any
        }
    }
}