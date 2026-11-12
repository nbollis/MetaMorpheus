#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TaskLayer.ParallelSearch.Twins;

/// <summary>
/// A read-only view of the union of two string sets, without copying either. The baseline's peptide set is
/// large and shared by every twin, while each twin adds only its own target's peptides, so copying the baseline
/// for every twin would cost memory for nothing. Membership is the operation the entrapment generator uses; the
/// set-comparison members are supported but enumerate.
/// </summary>
public sealed class UnionReadOnlySet : IReadOnlySet<string>
{
    private readonly IReadOnlySet<string> _first;
    private readonly IReadOnlySet<string> _second;
    private readonly Lazy<HashSet<string>> _materialized;

    public UnionReadOnlySet(IReadOnlySet<string> first, IReadOnlySet<string> second)
    {
        _first = first ?? throw new ArgumentNullException(nameof(first));
        _second = second ?? throw new ArgumentNullException(nameof(second));
        _materialized = new Lazy<HashSet<string>>(() => new HashSet<string>(_first.Concat(_second)));
    }

    public bool Contains(string item) => _first.Contains(item) || _second.Contains(item);

    public int Count => _first.Count + _second.Count(item => !_first.Contains(item));

    public IEnumerator<string> GetEnumerator() =>
        _first.Concat(_second.Where(item => !_first.Contains(item))).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool IsProperSubsetOf(IEnumerable<string> other) => _materialized.Value.IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<string> other) => _materialized.Value.IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<string> other) => _materialized.Value.IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<string> other) => other.All(Contains);
    public bool Overlaps(IEnumerable<string> other) => other.Any(Contains);
    public bool SetEquals(IEnumerable<string> other) => _materialized.Value.SetEquals(other);
}
