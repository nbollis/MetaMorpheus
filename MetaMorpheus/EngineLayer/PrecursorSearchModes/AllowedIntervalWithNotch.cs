namespace EngineLayer
{
    public readonly struct AllowedIntervalWithNotch(double minimumValue, double maximumValue, int notch)
    {
        public readonly double Minimum = minimumValue;
        public readonly double Maximum = maximumValue;
        public readonly int Notch = notch;
    }
}