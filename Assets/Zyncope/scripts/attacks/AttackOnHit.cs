using UnityEngine;

namespace ZyncopeVFXAttackOnHit
{
    public class AttackOnHit : MonoBehaviour
    {
        public enum HitType
        {
            ground,
            air
        }


        public GameObject onHitObj;
        public float durationToSpawnOnHit;
        public HitType onHitType;
    }
}