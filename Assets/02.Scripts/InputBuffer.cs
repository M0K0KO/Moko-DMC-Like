public struct InputEntry
{
    public InputId Id;
    public int FrameStamp;
    public bool Consumed;
}

public class InputBuffer
{
    private readonly InputEntry[] _ring = new InputEntry[8];
    private int _head;

    public void Push(InputId id, int frame)
    {
        _ring[_head] = new InputEntry
        {
            Id = id,
            FrameStamp = frame,
            Consumed = false
        };

        _head = (_head + 1) % _ring.Length;
    }

    public bool TryConsume(InputId id, int currentFrame, int lenience)
    {
        int bestIndex = -1;
        int bestStamp = int.MinValue;

        for (int i = 0; i < _ring.Length; i++)
        {
            ref InputEntry e = ref _ring[i];

            if (e.Id != id)
                continue;

            if (e.Consumed)
                continue;

            int age = currentFrame - e.FrameStamp;

            if (age < 0)
                continue;

            if (age > lenience)
                continue;

            if (e.FrameStamp > bestStamp)
            {
                bestStamp = e.FrameStamp;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
            return false;

        _ring[bestIndex].Consumed = true;
        return true;
    }
}