#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
namespace EngineLayer.Util;

public class PriorityQueue<T> : IEnumerable<T>
{
    private readonly SortedSet<(double, T)> _sortedSet;
    private readonly IComparer<(double, T)> _comparer;
    private readonly int _maxCapacity;

    /// <summary>
    /// Constructs a priority queue with a maximum capacity
    /// </summary>
    /// <param name="maxCapacity">Maximum number of results to keep</param>
    /// <param name="comparer">Default comparer compares priority then hash code of T</param>
    public PriorityQueue(int maxCapacity = 128, IComparer<(double, T)>? comparer = null)
    {
        _comparer = comparer ?? Comparer<(double, T)>.Create((x, y) =>
        {
            int priorityComparison = x.Item1.CompareTo(y.Item1);
            if (priorityComparison != 0)
                return priorityComparison;
            if (x.Item2 is null && y.Item2 is null)
                return 0;
            if (x.Item2 is null)
                return -1;
            if (y.Item2 is null)
                return 1;
            if (x.Item2 is IComparable<T> comparable)
                return comparable.CompareTo(y.Item2);
            return x.Item2.GetHashCode().CompareTo(y.Item2.GetHashCode());
        });
        _sortedSet = new SortedSet<(double, T)>(_comparer);
        _maxCapacity = maxCapacity;
    }

    public void Enqueue(double priority, T item)
    {
        if (_sortedSet.Count >= _maxCapacity)
        {
            // Remove the item with the lowest priority (last item) if the queue is at max capacity
            _sortedSet.Remove(_sortedSet.Min);
        }
        _sortedSet.Add((priority, item));
    }

    public T? Dequeue()
    {
        var max = _sortedSet.Max;
        _sortedSet.Remove(max);
        return max.Item2;
    }

    public T? Peek()
    {
        return _sortedSet.Max.Item2;
    }
    
    public int Count => _sortedSet.Count;
    public IEnumerator<T> GetEnumerator()
    {
        foreach (var item in _sortedSet)
        {
            yield return item.Item2;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}