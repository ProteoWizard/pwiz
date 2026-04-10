namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Boundaries and metrics for an extracted ion chromatogram peak.
    /// Maps to osprey-core/src/types.rs XICPeakBounds.
    /// </summary>
    public class XICPeakBounds
    {
        public double ApexRt { get; set; }
        public double ApexIntensity { get; set; }
        public double StartRt { get; set; }
        public double EndRt { get; set; }
        public double Area { get; set; }
        public double SignalToNoise { get; set; }
        public int ApexIndex { get; set; }
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
    }
}
