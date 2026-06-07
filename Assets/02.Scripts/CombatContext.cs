public enum ActionState
{
    None,
    Attacking,
}

public class CombatContext
{
    public ActionState ActionState = ActionState.None;
    public CancelTag CancelFlags = CancelTag.None;
}