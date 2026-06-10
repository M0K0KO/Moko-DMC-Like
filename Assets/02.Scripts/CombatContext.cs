public enum ActionState
{
    None,
    Attacking,
    Dodging,
    Launched,
    Hitstun
}

public class CombatContext
{
    public ActionState ActionState = ActionState.None;
    public LocoState LocoState = LocoState.Grounded;
    public CancelTag CancelFlags = CancelTag.None;
    public bool Invulnerable = false;

    public bool IsLockedOn = false;
    public ITargetable CurrentTarget;

    public Weapon CurrentMelee;
    public Weapon CurrentRanged;
}