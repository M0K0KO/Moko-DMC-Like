using Unity.Cinemachine;
using UnityEngine;

public class HitJuice : MonoBehaviour
{
    [SerializeField] private GameObject _sparkPrefab;
    [SerializeField] private AudioSource _audio;
    [SerializeField] private AudioClip _hitClip;
    [SerializeField] private CinemachineImpulseSource _impulse;

    public void OnHit(HitInfo info)
    {
        if (_sparkPrefab) Instantiate(_sparkPrefab, info.HitPoint, Quaternion.identity);
        if (_audio && _hitClip) _audio.PlayOneShot(_hitClip);
        if (_impulse) _impulse.GenerateImpulse();
    }
}