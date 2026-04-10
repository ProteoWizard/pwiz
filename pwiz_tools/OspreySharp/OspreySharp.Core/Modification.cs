namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// A post-translational modification on a peptide. Maps to osprey-core/src/types.rs Modification.
    /// </summary>
    public class Modification
    {
        /// <summary>
        /// Zero-indexed position in the peptide sequence.
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        /// Unimod identifier, if known.
        /// </summary>
        public int? UnimodId { get; set; }

        /// <summary>
        /// Mass shift caused by this modification.
        /// </summary>
        public double MassDelta { get; set; }

        /// <summary>
        /// Optional human-readable name.
        /// </summary>
        public string Name { get; set; }
    }
}
