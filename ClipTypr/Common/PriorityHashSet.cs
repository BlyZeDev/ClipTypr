namespace ClipTypr.Common;

using System.Collections;

public sealed class PriorityHashSet<TElement> : IEnumerable<TElement> where TElement : IEquatable<TElement>
{
    private readonly PriorityQueue<TElement, long> _queue;
    private readonly HashSet<TElement> _elements;

    public int Count => _elements.Count;

    public PriorityHashSet()
    {
        _queue = new PriorityQueue<TElement, long>();
        _elements = [];
    }

    public bool Enqueue(TElement element)
    {
        if (_elements.Add(element))
        {
            _queue.Enqueue(element, GetCurrentTicks());
            return true;
        }

        return false;
    }

    public TElement? Dequeue()
    {
        while (_queue.Count > 0)
        {
            var oldest = _queue.Dequeue();
            if (_elements.Remove(oldest)) return oldest;
        }

        return default;
    }

    public bool Remove(TElement element) => _elements.Remove(element);

    public void Clear()
    {
        _queue.Clear();
        _elements.Clear();
    }

    public IEnumerator<TElement> GetEnumerator() => _elements.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static long GetCurrentTicks() => Environment.TickCount64;
}