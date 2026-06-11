using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private float _speed = 20f;
    [SerializeField] private float _lifetime = 3f;
    [SerializeField] private float _radius = 0.25f;

    private LayerMask _targetMask;
    private int _damage, _hitstopFrames;
    private ReactionType _reaction;
    private Vector3 _launch;
    private float _age;

    public void Init(LayerMask mask, int dmg, int hitstop, ReactionType reaction, Vector3 launch)
    {
        _targetMask = mask; _damage = dmg; _hitstopFrames = hitstop; _reaction = reaction; _launch = launch;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        float step = _speed * dt;
        Vector3 prev = transform.position;

        // Sweep
        if (Physics.SphereCast(prev, _radius, transform.forward, out var rh, step, _targetMask, QueryTriggerInteraction.Collide))
        {
            var hb = rh.collider.GetComponent<Hurtbox>();
            if (hb != null && hb.Owner != null)
            {
                var info = new HitInfo
                {
                    Damage = _damage,
                    HitstopFrames = _hitstopFrames,
                    Reaction = _reaction,
                    Launch = transform.rotation * _launch,
                    HitPoint = rh.point
                };
                if (hb.Owner.TakeHit(info)) CombatFeedback.Raise(info);
                Destroy(gameObject);
                return;
            }
        }

        transform.position = prev + transform.forward * step;
        _age += dt;
        if (_age >= _lifetime) Destroy(gameObject);
    }
}