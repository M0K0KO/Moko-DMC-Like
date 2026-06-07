using UnityEngine;

public class Hurtbox : MonoBehaviour
{
    [SerializeField] private MonoBehaviour _ownerBehaviour;
    public IDamageable Owner => _ownerBehaviour as IDamageable;
    [SerializeField] private Vector3 _size = new Vector3(1, 2, 1);

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, _size);
    }
}