using UnityEngine;

public class HitboxGizmo : MonoBehaviour
{
    public ActionTimelineRunner Runner;

    private void OnDrawGizmos()
    {
        if (Runner == null || !Runner.IsPlaying || Runner.Current.HitWindows == null) 
            return;
        Gizmos.matrix = transform.localToWorldMatrix;
        int f = Runner.CurrentFrame;
        foreach (var w in Runner.Current.HitWindows)
        {
            bool active = f >= w.StartFrame && f <= w.EndFrame;
            Gizmos.color = active ? Color.red : new Color(1, 1, 1, 0.15f);
            Gizmos.DrawWireCube(w.BoxOffset, w.BoxSize);
        }
    }
}