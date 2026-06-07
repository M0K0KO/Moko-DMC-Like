using UnityEngine;

public class AnimationDriver
{
    private readonly Animator _animator;
    private readonly ActionTimelineRunner _runner;
    private ActionDefinition _lastSeen;

    public AnimationDriver(Animator a, ActionTimelineRunner r)
    {
        _animator = a; 
        _runner = r;
    }

    public void Apply()
    {
        ActionDefinition cur = _runner.IsPlaying ? _runner.Current : null;
        if (cur != _lastSeen)
        {
            if (cur != null && cur.Clip != null)
            {
                float clipFrames = cur.Clip.length * 60f;
                _animator.speed = clipFrames / Mathf.Max(cur.TotalFrames, 1);
                _animator.CrossFadeInFixedTime(cur.Clip.name, 0.02f, 0);
            }
            else
            {
                _animator.speed = 1f;
                _animator.CrossFadeInFixedTime("Idle", 0.05f);
            }

            _lastSeen = cur;
        }
    }
}