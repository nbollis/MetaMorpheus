#nullable enable

using System.Collections.Generic;

namespace TaskLayer.ParallelSearch.FdrAlignment;

public interface IBaselineFdrAlignmentService<TItem>
{
    void BuildBaselineCache(IEnumerable<TItem> baselineItems);

    AlignmentStats ApplyBaseline(IList<TItem> items);
}
