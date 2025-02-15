namespace EngineLayer
{
    public struct AllowedIntervalWithNotch(double minimumValue, double maximumValue, int notch)
    {
        public double Minimum = minimumValue;
        public double Maximum = maximumValue;
        public int Notch = notch;
    }
}