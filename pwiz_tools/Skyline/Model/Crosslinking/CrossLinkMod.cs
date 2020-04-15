using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class CrosslinkMod : Immutable
    {
        public CrosslinkMod(int indexAa, CrosslinkerDef crosslinkerDef, IEnumerable<LinkedPeptide> linkedPeptides)
        {
            IndexAa = indexAa;
            CrosslinkerDef = crosslinkerDef;
            LinkedPeptides = ImmutableList.ValueOf(linkedPeptides);
        }

        public int IndexAa { get; private set; }

        public CrosslinkerDef CrosslinkerDef { get; private set; }

        public ImmutableList<LinkedPeptide> LinkedPeptides { get; private set; }

        public MoleculeMassOffset GetNeutralFormula(SrmSettings settings, IsotopeLabelType labelType)
        {
            var massType = settings.TransitionSettings.Prediction.PrecursorMassType;
            MoleculeMassOffset moleculeMassOffset = CrosslinkerDef.FormulaMass.GetMoleculeMassOffset(massType);
            foreach (var linkedPeptide in LinkedPeptides)
            {
                moleculeMassOffset = moleculeMassOffset.Plus(linkedPeptide.GetNeutralFormula(settings, labelType));
            }

            return moleculeMassOffset;
        }

        public ModificationSite ModificationSite
        {
            get
            {
                return new ModificationSite(IndexAa, CrosslinkerDef.Name);
            }
        }

        public IEnumerable<IList<ComplexFragmentIon>> ListComplexIonPermutations(SrmSettings settings,
            int maxFragmentationEvents)
        {
            var result = ImmutableList.Singleton(ImmutableList.Empty<ComplexFragmentIon>());
            foreach (var linkedPeptide in LinkedPeptides)
            {

            }
            var linkedTransitionGroupedDocNodes = ImmutableList.ValueOf(LinkedPeptides.Select(linkedPeptide =>
                linkedPeptide.GetTransitionGroupDocNode(settings, IsotopeLabelType.light, Adduct.SINGLY_PROTONATED)));

            var queue = new Queue<IList<ComplexFragmentIon>>();
            queue.Enqueue(ImmutableList.Empty<ComplexFragmentIon>());
            while (queue.Count > 0)
            {
                var next = queue.Dequeue();
                int eventCount = next.Sum(item => item.GetFragmentationEventCount());
                var linkedPeptide = LinkedPeptides[next.Count];
                foreach (var complexFragmentIon in linkedPeptide.ListComplexFragmentIons(settings,
                    linkedTransitionGroupedDocNodes[next.Count], maxFragmentationEvents - eventCount))
                {
                    var newList = ImmutableList.ValueOf(next.Append(complexFragmentIon));
                    if (newList.Count == LinkedPeptides.Count)
                    {
                        yield return newList;
                    }
                    else
                    {
                        queue.Enqueue(newList);
                    }
                }
            }
        }


    }
}
