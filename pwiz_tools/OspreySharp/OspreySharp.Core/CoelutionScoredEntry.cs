using System.Collections.Generic;

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// A fully scored DIA coelution search result, combining library identity,
    /// peak boundaries, feature scores, and fragment data.
    /// Maps to osprey-core/src/types.rs CoelutionScoredEntry.
    /// </summary>
    public class CoelutionScoredEntry
    {
        public uint EntryId { get; set; }
        public bool IsDecoy { get; set; }
        public string Sequence { get; set; }
        public string ModifiedSequence { get; set; }
        public byte Charge { get; set; }
        public double PrecursorMz { get; set; }
        public List<string> ProteinIds { get; set; }
        public uint ScanNumber { get; set; }
        public double ApexRt { get; set; }
        public XICPeakBounds PeakBounds { get; set; }
        public CoelutionFeatureSet Features { get; set; }
        public double[] FragmentMzs { get; set; }
        public float[] FragmentIntensities { get; set; }
        public List<CwtCandidate> CwtCandidates { get; set; }
        public string FileName { get; set; }

        public CoelutionScoredEntry()
        {
            ProteinIds = new List<string>();
            FragmentMzs = new double[0];
            FragmentIntensities = new float[0];
            CwtCandidates = new List<CwtCandidate>();
            PeakBounds = new XICPeakBounds();
            Features = new CoelutionFeatureSet();
        }
    }
}
