using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class ModifiedPeptide : Immutable
    {
        public ModifiedPeptide(Peptide peptide, ExplicitMods explicitMods)
        {
            Peptide = peptide;
            ExplicitMods = explicitMods;
        }

        public Peptide Peptide { get; private set; }
        public ExplicitMods ExplicitMods { get; private set; }

        protected bool Equals(ModifiedPeptide other)
        {
            return Equals(Peptide, other.Peptide) && Equals(ExplicitMods, other.ExplicitMods);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ModifiedPeptide) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Peptide.GetHashCode() * 397) ^ (ExplicitMods != null ? ExplicitMods.GetHashCode() : 0);
            }
        }

        public ModifiedSequence GetModifiedSequence(SrmSettings settings, IsotopeLabelType labelType)
        {
            return ModifiedSequence.GetModifiedSequence(settings, Peptide.Sequence, ExplicitMods, labelType);
        }
    }
}
