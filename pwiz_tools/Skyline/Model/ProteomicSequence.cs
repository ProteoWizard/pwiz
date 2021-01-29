using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model
{
    public abstract class ProteomicSequence
    {
        public static ProteomicSequence GetProteomicSequence(SrmSettings settings, Peptide peptide,
            ExplicitMods explicitMods, IsotopeLabelType labelType)
        {
            if (explicitMods == null || !explicitMods.HasCrosslinks)
            {
                return ModifiedSequence.GetModifiedSequence(settings, peptide.Sequence, explicitMods, labelType);
            }

            return CrosslinkedSequence.GetCrosslinkedSequence(settings, explicitMods.GetPeptideStructure(), labelType);
        }

        public static ProteomicSequence GetProteomicSequence(SrmSettings settings, PeptideDocNode peptideDocNode, IsotopeLabelType labelType)
        {
            return GetProteomicSequence(settings, peptideDocNode.Peptide, peptideDocNode.ExplicitMods, labelType);
        }

        public abstract string MonoisotopicMasses { get; }
        public abstract string AverageMasses { get; }
        public abstract string ThreeLetterCodes { get; }
        public abstract string FullNames { get; }
        public abstract string UnimodIds { get; }
        public abstract string FormatDefault();
    }
}