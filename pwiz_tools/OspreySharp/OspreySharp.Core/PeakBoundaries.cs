namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Quality metrics for a chromatographic peak. Maps to osprey-core/src/types.rs PeakQuality.
    /// </summary>
    public class PeakQuality
    {
        public double SignalToNoise { get; set; }
        public double Symmetry { get; set; }
        public double Fwhm { get; set; }
    }

    /// <summary>
    /// Defines the boundaries and metrics of an integrated chromatographic peak.
    /// Maps to osprey-core/src/types.rs PeakBoundaries.
    /// </summary>
    public class PeakBoundaries
    {
        public double StartRt { get; set; }
        public double EndRt { get; set; }
        public double ApexRt { get; set; }
        public double ApexCoefficient { get; set; }
        public double IntegratedArea { get; set; }
        public PeakQuality PeakQuality { get; set; }
    }
}
