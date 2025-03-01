#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
namespace EngineLayer.Util;

/// <summary>
/// Represents a priority queue with a fixed maximum capacity. 
/// The queue maintains DISTINCT elements in sorted order based on their priority then by the comparer.
/// Default comparer compares priority, then a CompareTo if T is of type IComparable, then hash code of T and ranks in descending order.
/// </summary>
/// <typeparam name="T">The type of elements in the priority queue.</typeparam>
public class PriorityQueue<T> : IEnumerable<T>
{
    private readonly SortedSet<(double, T)> _sortedSet;
    private readonly int _maxCapacity;

    /// <summary>
    /// Gets the number of elements in the priority queue.
    /// </summary>
    public int Count => _sortedSet.Count;

    /// <summary>
    /// Constructs a priority queue with a maximum capacity
    /// </summary>
    /// <param name="maxCapacity">Maximum number of results to keep</param>
    /// <param name="comparer">Default comparer compares priority then hash code of T</param>
    public PriorityQueue(int maxCapacity = 128, IComparer<(double, T)>? comparer = null)
    {
        IComparer<(double, T)> comparer1 = comparer ?? Comparer<(double, T)>.Create((x, y) =>
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
        _sortedSet = new SortedSet<(double, T)>(comparer1);
        _maxCapacity = maxCapacity;
    }

    /// <summary>
    /// Adds an element to the priority queue with a specified priority.
    /// If the queue is at maximum capacity, the element with the lowest priority is removed.
    /// </summary>
    /// <param name="priority">The priority of the element to be added.</param>
    /// <param name="item">The element to be added to the queue.</param>
    public void Enqueue(double priority, T item)
    {
        if (_sortedSet.Count >= _maxCapacity)
        {
            // Remove the item with the lowest priority (last item) if the queue is at max capacity
            _sortedSet.Remove(_sortedSet.Min);
        }
        _sortedSet.Add((priority, item));
    }

    /// <summary>
    /// Removes and returns the element with the highest priority from the queue.
    /// </summary>
    /// <returns>The element with the highest priority, or null if the queue is empty.</returns>
    public T? Dequeue()
    {
        var max = _sortedSet.Max;
        _sortedSet.Remove(max);
        return max.Item2;
    }

    /// <summary>
    /// Removes and returns the element with the highest priority from the queue.
    /// </summary>
    /// <returns>The element with the highest priority and its priority, or null if the queue is empty.</returns>
    public (double, T)? DequeueWithPriority()
    {
        var max = _sortedSet.Max;
        // if queue is empty, max will be the default anonymous type (0, null)
        if (EqualityComparer<(double, T)>.Default.Equals(max, default((double, T))))
            return null;

        _sortedSet.Remove(max);
        return max;
    }

    /// <summary>
    /// Returns the element with the highest priority without removing it from the queue.
    /// </summary>
    /// <returns>The element with the highest priority, or null if the queue is empty.</returns>
    public T? Peek()
    {
        return _sortedSet.Max.Item2;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the elements of the priority queue in priority order.
    /// </summary>
    /// <returns>An enumerator for the priority queue.</returns>
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