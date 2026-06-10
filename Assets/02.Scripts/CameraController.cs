using Unity.Cinemachine;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [SerializeField] CinemachineCamera freeLookCam;
    [SerializeField] CinemachineCamera lockedCam;
    [SerializeField] CinemachineInputAxisController freeLookInput;
    [SerializeField] int lockedPriority = 20;

    private CombatContext ctx;
    private TargetProvider targets;

    private void Start()
    {
        ctx = player.Context;
        targets = player.Targets;
    }

    public void UpdateLockOnCamera()
    {
        bool active = ctx.IsLockedOn && targets.CurrentTarget != null;

        if (active)
            lockedCam.LookAt = GetAim(targets.CurrentTarget);

        lockedCam.Priority = active ? lockedPriority : 0;
        if (freeLookInput) freeLookInput.enabled = !ctx.IsLockedOn;
    }

    static Transform GetAim(ITargetable t) => t.AimPoint;
}