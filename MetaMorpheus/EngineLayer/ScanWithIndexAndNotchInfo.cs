namespace EngineLayer
{
    internal readonly struct ScanWithIndexAndNotchInfo(int notch, int scanIndex)
    {
        public readonly int Notch = notch;
        public readonly int ScanIndex = scanIndex;
    }
}