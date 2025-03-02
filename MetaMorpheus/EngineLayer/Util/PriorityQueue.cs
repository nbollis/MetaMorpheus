#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace EngineLayer.Util;

/// <summary>
/// Represents a priority queue with a fixed maximum capacity. 
/// The queue maintains DISTINCT elements in sorted order based on their priority then by the comparer.
/// Default comparer compares priority, then a CompareTo if T is of type IComparable, then hash code of T and ranks in descending order.
/// </summary>
/// <typeparam name="T">The type of elements in the priority queue.</typeparam>
public class PriorityQueue<T> : IEnumerable<T>
{
    private readonly int _maxCapacity;
    protected readonly SortedSet<(double, T)> SortedSet;
    protected readonly IComparer<(double, T)> Comparer;
    protected readonly IComparer<T> InternalComparer;

    /// <summary>
    /// Gets the number of elements in the priority queue.
    /// </summary>
    public int Count => SortedSet.Count;

    /// <summary>
    /// Constructs a priority queue with a maximum capacity
    /// </summary>
    /// <param name="maxCapacity">Maximum number of results to keep</param>
    /// <param name="comparer">Default comparer compares priority then hash code of T</param>
    public PriorityQueue(int maxCapacity = 128, IComparer<T>? comparer = null) 
    {
        InternalComparer = comparer ?? Comparer<T>.Default;
        Comparer = Comparer<(double, T)>.Create((x, y) =>
        {
            int priorityComparison = x.Item1.CompareTo(y.Item1);
            if (priorityComparison != 0)
                return -priorityComparison; // higher priority is better
            if (x.Item2 is null && y.Item2 is null)
                return 0;
            if (x.Item2 is null)
                return 1;
            if (y.Item2 is null)
                return 1;
            return InternalComparer.Compare(x.Item2, y.Item2);
        });
        SortedSet = new SortedSet<(double, T)>(Comparer);
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
        if (SortedSet.Count >= _maxCapacity)
        {
            // Remove the item with the lowest priority (last item) if the queue is at max capacity
            SortedSet.Remove(SortedSet.Max);
        }
        SortedSet.Add((priority, item));
    }

    /// <summary>
    /// Removes and returns the element with the highest priority from the queue.
    /// </summary>
    /// <returns>The element with the highest priority, or the default if queue is empty.</returns>
    public T? Dequeue()
    {
        var highestPriority = SortedSet.Min; // if queue is empty, max will be the default anonymous type (0, null)
        if (EqualityComparer<(double, T)>.Default.Equals(highestPriority, default((double, T))))
            return default;

        SortedSet.Remove(highestPriority);
        return highestPriority.Item2;
    }

    /// <summary>
    /// Removes and returns the element with the highest priority from the queue.
    /// </summary>
    /// <returns>The element with the highest priority and its priority, or default if the queue is empty.</returns>
    public (double, T)? DequeueWithPriority()
    {
        var highestPriority = SortedSet.Min;
        // if queue is empty, max will be the default anonymous type (0, null)
        if (EqualityComparer<(double, T)>.Default.Equals(highestPriority, default((double, T))))
            return default;

        SortedSet.Remove(highestPriority);
        return highestPriority;
    }

    /// <summary>
    /// Returns the element with the highest priority without removing it from the queue.
    /// </summary>
    /// <returns>The element with the highest priority, or null if the queue is empty.</returns>
    public T? Peek()
    {
        return SortedSet.Min.Item2;
    }

    public List<T> ToList() => SortedSet.Select(x => x.Item2).ToList();

    public List<(double, T)> ToListWithPriority() => SortedSet.ToList();

    public T[] ToArray() => SortedSet.Select(x => x.Item2).ToArray();

    public (double, T)[] ToArrayWithPriority() => SortedSet.ToArray();

    /// <summary>
    /// Returns an enumerator that iterates through the elements of the priority queue in priority order.
    /// </summary>
    /// <returns>An enumerator for the priority queue.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        foreach (var item in SortedSet)
        {
            yield return item.Item2;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}