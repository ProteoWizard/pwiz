namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Inter-replicate peak reconciliation configuration.
    /// Maps to osprey-core/src/config.rs ReconciliationConfig.
    /// </summary>
    public class ReconciliationConfig
    {
        /// <summary>Enable inter-replicate peak reconciliation (default: true for multi-file).</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Number of CWT candidate peaks to store per precursor.</summary>
        public int TopNPeaks { get; set; } = 5;

        /// <summary>FDR threshold for selecting consensus peptides.</summary>
        public double ConsensusFdr { get; set; } = 0.01;
    }
}
