using System.Collections.Generic;
using System.Linq;
using System.Xml;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class LinkedPeptide : Immutable
    {
        public LinkedPeptide(Peptide peptide, int aaIndex, ExplicitMods explicitMods)
        {
            Peptide = peptide;
            AaIndex = aaIndex;
            ExplicitMods = explicitMods;
        }

        public Peptide Peptide { get; private set; }
        public int AaIndex { get; private set; }

        public ExplicitMods ExplicitMods { get; private set; }

        public TransitionGroup GetTransitionGroup(IsotopeLabelType labelType, Adduct adduct)
        {
            return new TransitionGroup(Peptide, adduct, labelType);
        }

        public TransitionGroupDocNode GetTransitionGroupDocNode(SrmSettings settings, IsotopeLabelType labelType, Adduct adduct) {
            var transitionGroup = GetTransitionGroup(labelType, adduct);
            var transitionGroupDocNode = new TransitionGroupDocNode(transitionGroup, Annotations.EMPTY, settings, ExplicitMods, null, ExplicitTransitionGroupValues.EMPTY, null, null, false);
            return transitionGroupDocNode;
        }

        public MoleculeMassOffset GetNeutralFormula(SrmSettings settings, IsotopeLabelType labelType)
        {
            var transitionGroupDocNode = GetTransitionGroupDocNode(settings, labelType, Adduct.SINGLY_PROTONATED);
            return transitionGroupDocNode.GetNeutralFormula(settings, ExplicitMods);
        }

        public IEnumerable<ComplexFragmentIon> ListComplexFragmentIons(SrmSettings settings, int maxFragmentEventCount)
        {
            IEnumerable<ComplexFragmentIon> result = ListSimpleFragmentIons(settings);
            return ExplicitMods.PermuteComplexFragmentIons(settings, maxFragmentEventCount, result);
        }

        public IEnumerable<ComplexFragmentIon> PermuteFragmentIons(SrmSettings settings, int maxFragmentationCount,
            ModificationSite modificationSite, IEnumerable<ComplexFragmentIon> fragmentIons)
        {
            var linkedFragmentIonList = ImmutableList.ValueOf(ListComplexFragmentIons(settings, maxFragmentationCount));
            return fragmentIons.SelectMany(cfi =>
                PermuteFragmentIon(settings, maxFragmentationCount, cfi, modificationSite, linkedFragmentIonList));

        }
        private IEnumerable<ComplexFragmentIon> PermuteFragmentIon(SrmSettings settings, 
            int maxFragmentationCount,
            ComplexFragmentIon fragmentIon,
            ModificationSite modificationSite,
            IList<ComplexFragmentIon> linkedFragmentIons)
        {
            if (!fragmentIon.IncludesAaIndex(modificationSite.AaIndex))
            {
                yield return fragmentIon;
                yield break;
            }
            int fragmentCountRemaining = maxFragmentationCount - fragmentIon.GetFragmentationEventCount();
            foreach (var linkedFragmentIon in linkedFragmentIons)
            {
                if (linkedFragmentIon.GetFragmentationEventCount() > fragmentCountRemaining)
                {
                    continue;
                }

                yield return fragmentIon.ChangeChildren(fragmentIon.Children.Append(
                    new KeyValuePair<ModificationSite, ComplexFragmentIon>(modificationSite, linkedFragmentIon)));
            }
        }

        public IEnumerable<ComplexFragmentIon> ListSimpleFragmentIons(SrmSettings settings)
        {
            var transitionGroupDocNode =
                GetTransitionGroupDocNode(settings, IsotopeLabelType.light, Adduct.SINGLY_PROTONATED);
            foreach (var transitionDocNode in transitionGroupDocNode.TransitionGroup.GetTransitions(settings,
                transitionGroupDocNode, ExplicitMods, transitionGroupDocNode.PrecursorMz,
                transitionGroupDocNode.IsotopeDist, null, null, true))
            {
                yield return new ComplexFragmentIon(transitionDocNode.Transition, transitionDocNode.Losses)
                    .ChangeAdduct(Adduct.EMPTY);
            }
        }

        private enum EL
        {
            linked_peptide,
        }

        private enum ATTR
        {
            sequence,
            aa_index
        }
        public void WriteToXml(DocumentWriter documentWriter, XmlWriter writer)
        {
            writer.WriteStartElement(EL.linked_peptide);
            writer.WriteAttribute(ATTR.sequence, Peptide.Sequence);
            if (!Equals(ExplicitMods, ExplicitMods.EMPTY))
            {
                documentWriter.WriteExplicitMods(writer, Peptide.Sequence, ExplicitMods);
            }
            writer.WriteEndElement();
        }

        public static LinkedPeptide ReadFromXml(DocumentReader documentReader, XmlReader reader)
        {
            if (!reader.IsStartElement(EL.linked_peptide))
            {
                return null;
            }

            var sequence = reader.GetAttribute(ATTR.sequence);
            int aaIndex = reader.GetIntAttribute(ATTR.aa_index);
            var peptide = new Peptide(sequence);
            var explicitMods = ExplicitMods.EMPTY;
            if (reader.IsEmptyElement)
            {
                reader.Read();
            }
            else
            {
                reader.ReadStartElement();
                explicitMods = documentReader.ReadExplicitMods(reader, peptide);
                reader.ReadEndElement();
            }
            return new LinkedPeptide(peptide, aaIndex, explicitMods);
        }
    }
}
