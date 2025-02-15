namespace EngineLayer
{
    internal struct ScanWithIndexAndNotchInfo(int notch, int scanIndex)
    {
        public int Notch = notch;
        public int ScanIndex = scanIndex;
    }
}