using System.Collections.Generic;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class PeptideStructure : Immutable
    {
        public PeptideStructure(IEnumerable<ModifiedPeptide> peptides, IEnumerable<CrosslinkModification> crosslinks)
        {
            Peptides = ImmutableList.ValueOf(peptides);
            Crosslinks = ImmutableList.ValueOfOrEmpty(crosslinks);
        }

        public Peptide PrimaryPeptide
        {
            get { return Peptides[0].Peptide; }
        }
        public ImmutableList<ModifiedPeptide> Peptides { get; private set; }
        public ImmutableList<CrosslinkModification> Crosslinks { get; private set; }

        public static PeptideStructure SinglePeptide(Peptide peptide, ExplicitMods explicitMods)
        {
            return new PeptideStructure(ImmutableList.Singleton(new ModifiedPeptide(peptide, explicitMods)), null);
        }

        protected bool Equals(PeptideStructure other)
        {
            return Peptides.Equals(other.Peptides) && Crosslinks.Equals(other.Crosslinks);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PeptideStructure) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Peptides.GetHashCode() * 397) ^ Crosslinks.GetHashCode();
            }
        }

        public bool HasCrosslinks
        {
            get
            {
                return Crosslinks.Count > 0;
            }
        }
    }
}