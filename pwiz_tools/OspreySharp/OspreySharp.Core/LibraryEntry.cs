using System.Collections.Generic;

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Represents a single entry in a spectral library.
    /// Maps to osprey-core/src/types.rs in the Rust implementation.
    /// </summary>
    public class LibraryEntry
    {
        public uint Id { get; set; }
        public string Sequence { get; set; }
        public string ModifiedSequence { get; set; }
        public List<Modification> Modifications { get; set; }
        public byte Charge { get; set; }
        public double PrecursorMz { get; set; }
        public double RetentionTime { get; set; }
        public bool RtCalibrated { get; set; }
        public List<LibraryFragment> Fragments { get; set; }
        public List<string> ProteinIds { get; set; }
        public List<string> GeneNames { get; set; }
        public bool IsDecoy { get; set; }

        public LibraryEntry(uint id, string sequence, string modifiedSequence,
            byte charge, double precursorMz, double retentionTime)
        {
            Id = id;
            Sequence = sequence;
            ModifiedSequence = modifiedSequence;
            Charge = charge;
            PrecursorMz = precursorMz;
            RetentionTime = retentionTime;
            Modifications = new List<Modification>();
            Fragments = new List<LibraryFragment>();
            ProteinIds = new List<string>();
            GeneNames = new List<string>();
        }
    }
}
