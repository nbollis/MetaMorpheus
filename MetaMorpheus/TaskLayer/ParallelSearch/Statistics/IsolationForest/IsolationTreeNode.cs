namespace TaskLayer.ParallelSearch.Statistics.IsolationForest;

/// <summary>
/// One node in an isolation tree.
/// </summary>
internal sealed class IsolationTreeNode
{
    public bool IsExternal { get; }
    public int Size { get; }

    public int SplitFeatureIndex { get; }
    public double SplitValue { get; }

    public IsolationTreeNode? Left { get; }
    public IsolationTreeNode? Right { get; }

    private IsolationTreeNode(int size)
    {
        IsExternal = true;
        Size = size;
        SplitFeatureIndex = -1;
        SplitValue = double.NaN;
    }

    private IsolationTreeNode(
        int size,
        int splitFeatureIndex,
        double splitValue,
        IsolationTreeNode left,
        IsolationTreeNode right)
    {
        IsExternal = false;
        Size = size;
        SplitFeatureIndex = splitFeatureIndex;
        SplitValue = splitValue;
        Left = left;
        Right = right;
    }

    public static IsolationTreeNode External(int size)
    {
        return new IsolationTreeNode(size);
    }

    public static IsolationTreeNode Internal(
        int size,
        int splitFeatureIndex,
        double splitValue,
        IsolationTreeNode left,
        IsolationTreeNode right)
    {
        return new IsolationTreeNode(size, splitFeatureIndex, splitValue, left, right);
    }
}
