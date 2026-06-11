public class WeaponLoadout
{
    private readonly CombatContext _ctx;
    private readonly Weapon[] _melee;
    private readonly Weapon[] _ranged;
    private int _meleeIdx, _rangedIdx;

    public WeaponLoadout(CombatContext ctx, Weapon[] melee, Weapon[] ranged)
    {
        _ctx = ctx;
        _melee = melee;
        _ranged = ranged;
        Apply();
    }

    private void Apply()
    {
        _ctx.CurrentMelee  = (_melee  != null && _melee.Length  > 0) ? _melee[_meleeIdx]   : null;
        _ctx.CurrentRanged = (_ranged != null && _ranged.Length > 0) ? _ranged[_rangedIdx] : null;
    }

    public bool CycleMelee()
    {
        if (_melee == null || _melee.Length < 2) return false;
        _meleeIdx = (_meleeIdx + 1) % _melee.Length; Apply(); return true;
    }
    public bool CycleRanged()
    {
        if (_ranged == null || _ranged.Length < 2) return false;
        _rangedIdx = (_rangedIdx + 1) % _ranged.Length; Apply(); return true;
    }
}