using UnityEngine;

public class CharacterMotor : MonoBehaviour
{
    [SerializeField] private CapsuleCollider _capsule;
    [SerializeField] private LayerMask _collisionMask;

    private const float Skin = 0.05f;
    private const float Gravity = -25f;
    private const float GroundProbe = 0.1f;
    private const int MaxBounces = 3;
    private const int MaxDepenetrationIters = 4;

    private readonly Collider[] _overlaps = new Collider[32];

    private Vector3 _velocity;
    public bool IsGrounded { get; private set; }
    public Vector3 Velocity => _velocity;

    public void SetHorizontalVelocity(Vector2 h) 
    {
        _velocity.x = h.x;
        _velocity.z = h.y;
    }

    public void Tick(float dt)
    {
        Vector3 pos = transform.position;

        // 0) Depenetration
        pos = Depenetrate(pos);

        // 1) Horizontal Pass, Collide -> Slide
        Vector3 horizontal = new Vector3(_velocity.x, 0, _velocity.z) * dt;

        pos = CollideAndSlide(pos, horizontal);

        // 2) Vertical Pass, Gravity, Ground Snap
        _velocity.y += Gravity * dt;
        pos = MoveVertical(pos, dt);

        transform.position = pos;
    }

    private Vector3 Depenetrate(Vector3 pos)
    {
        for (int iter = 0; iter < MaxDepenetrationIters; iter++)
        {
            GetCapsule(pos, out Vector3 p0, out Vector3 p1, out float radius);

            int count = Physics.OverlapCapsuleNonAlloc(
                p0,
                p1,
                radius,
                _overlaps,
                _collisionMask,
                QueryTriggerInteraction.Ignore);

            bool moved = false;

            for (int i = 0; i < count; i++)
            {
                Collider other = _overlaps[i];

                bool penetrating = Physics.ComputePenetration(
                    _capsule,
                    pos,
                    transform.rotation,
                    other,
                    other.transform.position,
                    other.transform.rotation,
                    out Vector3 dir,
                    out float dist);

                if (!penetrating)
                    continue;

                pos += dir * (dist + 0.001f);
                moved = true;
            }

            if (!moved)
                break;
        }

        return pos;
    }

    private Vector3 CollideAndSlide(Vector3 pos, Vector3 horizontal)
    {
        Vector3 remaining = horizontal;

        for (int bounce = 0; bounce < MaxBounces; bounce++)
        {
            float distance = remaining.magnitude;
            if (distance <= 0.0001f)
                break;

            Vector3 dir = remaining / distance;

            if (!CastCapsule(pos, dir, distance + Skin, out RaycastHit hit))
            {
                pos += remaining;
                break;
            }

            float moveDistance = Mathf.Max(hit.distance - Skin, 0f);
            pos += dir * moveDistance;

            float leftover = distance - moveDistance;
            Vector3 leftoverMove = dir * leftover;

            remaining = Vector3.ProjectOnPlane(leftoverMove, hit.normal);
            remaining.y = 0f;
        }

        return pos;
    }

    private Vector3 MoveVertical(Vector3 pos, float dt)
    {
        IsGrounded = false;
        float dy = _velocity.y * dt;

        if (Mathf.Abs(dy) > 0.0001f)
        {
            Vector3 dir = dy > 0f ? Vector3.up : Vector3.down;
            float distance = Mathf.Abs(dy);
            if (CastCapsule(pos, dir, distance + Skin, out RaycastHit hit))
            {
                float moveDistance = Mathf.Max(hit.distance - Skin, 0f);
                pos += dir * moveDistance;

                if (dir == Vector3.down)
                    IsGrounded = true;

                _velocity.y = 0f;
            }
            else
            {
                pos += dir * distance;
            }
        }

        if (_velocity.y <= 0f)
        {
            if (CastCapsule(pos, Vector3.down, GroundProbe + Skin, out RaycastHit groundHit))
            {
                float snapDistance = Mathf.Max(groundHit.distance - Skin, 0f);

                pos += Vector3.down * snapDistance;
                IsGrounded = true;
                _velocity.y = 0f;
            }
        }

        return pos;
    }

    private bool CastCapsule(Vector3 pos, Vector3 dir, float distance, out RaycastHit hit)
    {
        GetCapsule(pos, out Vector3 p0, out Vector3 p1, out float radius);

        return Physics.CapsuleCast(
            p0,
            p1,
            Mathf.Max(radius - Skin, 0.001f),
            dir,
            out hit,
            distance,
            _collisionMask,
            QueryTriggerInteraction.Ignore);
    }

    private void GetCapsule(
    Vector3 worldPos,
    out Vector3 p0,
    out Vector3 p1,
    out float radius)
    {
        Transform t = transform;
        Vector3 scale = t.lossyScale;

        Vector3 localAxis = _capsule.direction switch
        {
            0 => Vector3.right,
            1 => Vector3.up,
            2 => Vector3.forward,
            _ => Vector3.up
        };

        Vector3 worldAxis = t.rotation * localAxis;

        float axisScale;
        float radiusScale;

        if (_capsule.direction == 0)
        {
            axisScale = Mathf.Abs(scale.x);
            radiusScale = Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        }
        else if (_capsule.direction == 1)
        {
            axisScale = Mathf.Abs(scale.y);
            radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        }
        else
        {
            axisScale = Mathf.Abs(scale.z);
            radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        }

        radius = _capsule.radius * radiusScale;

        float height = Mathf.Max(_capsule.height * axisScale, radius * 2f);
        float halfSegment = Mathf.Max((height * 0.5f) - radius, 0f);

        Vector3 center = worldPos + t.rotation * Vector3.Scale(_capsule.center, scale);

        p0 = center + worldAxis * halfSegment;
        p1 = center - worldAxis * halfSegment;
    }
}