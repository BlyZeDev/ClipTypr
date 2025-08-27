namespace ClipTypr.Common;

using System.Collections;

public sealed class CircularHashQueue<T> : IEnumerable<T> where T : IEquatable<T>
{
    private readonly LinkedList<T> _linkedList;
    private readonly Dictionary<T, LinkedListNode<T>> _map;

    public int Capacity { get; }

    public int Count => _linkedList.Count;

    public CircularHashQueue(int capacity, IEqualityComparer<T>? equalityComparer = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(capacity, 0, nameof(capacity));

        _linkedList = new LinkedList<T>();
        _map = new Dictionary<T, LinkedListNode<T>>(equalityComparer);

        Capacity = capacity;
    }

    public EnqueueResult Enqueue(T item)
    {
        var result = EnqueueResult.Success;

        if (_map.TryGetValue(item, out var node))
        {
            _linkedList.Remove(node);
            _map.Remove(item);

            result = EnqueueResult.RemovedDuplicate;
        }

        if (_linkedList.Count == Capacity)
        {
            _map.Remove(_linkedList.First!.Value);
            _linkedList.RemoveFirst();

            result = EnqueueResult.RemovedOldestEntry;
        }

        _map[item] = _linkedList.AddLast(item);
        return result;
    }

    public void Clear()
    {
        _linkedList.Clear();
        _map.Clear();
    }

    public IEnumerator<T> GetEnumerator() => _linkedList.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}