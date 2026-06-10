using UnityEngine;

public class Orchestrator : MonoBehaviour
{
    public static Orchestrator Instance;

    [SerializeField] private HitJuice juice;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void SetHitJuice(HitResolution res)
    {
        res.OnHit += juice.OnHit;
    }
}