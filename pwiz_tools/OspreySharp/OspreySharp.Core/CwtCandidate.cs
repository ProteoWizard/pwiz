namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// A peak candidate identified by continuous wavelet transform.
    /// Maps to osprey-core/src/types.rs CwtCandidate.
    /// </summary>
    public struct CwtCandidate
    {
        public double ApexRt { get; set; }
        public double StartRt { get; set; }
        public double EndRt { get; set; }
        public double Area { get; set; }
        public double Snr { get; set; }
        public double CoelutionScore { get; set; }
    }
}
