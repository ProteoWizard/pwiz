using System.Collections.Generic;
using System.Linq;
using EnvDTE;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class LinkedPeptide : Immutable
    {
        public LinkedPeptide(Peptide peptide, int indexAa, ExplicitMods explicitMods)
        {
            Peptide = peptide;
            IndexAa = indexAa;
            ExplicitMods = explicitMods;
        }

        public Peptide Peptide { get; private set; }
        public int IndexAa { get; private set; }

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
            // var linkedFragmentIonLists = new List<KeyValuePair<ModificationSite, IList<ComplexFragmentIon>>>();
            // foreach (var crosslinkMod in ExplicitMods.CrosslinkMods)
            // {
            //     foreach (var linkedPeptide in crosslinkMod.LinkedPeptides)
            //     {
            //         linkedFragmentIonLists.Add(new KeyValuePair<ModificationSite, IList<ComplexFragmentIon>>(
            //             crosslinkMod.ModificationSite,
            //             ImmutableList.ValueOf(linkedPeptide.ListComplexFragmentIons(settings, maxFragmentEventCount))));
            //
            //     }
            // }

            IEnumerable<ComplexFragmentIon> result = ListSimpleFragmentIons(settings);
            return ExplicitMods.PermuteComplexFragmentIons(settings, maxFragmentEventCount, result);
            foreach (var crosslinkMod in ExplicitMods.CrosslinkMods)
            {
                foreach (var linkedPeptide in crosslinkMod.LinkedPeptides)
                {
                    var linkedFragmentIonList = ImmutableList.ValueOf(linkedPeptide.ListComplexFragmentIons(settings, maxFragmentEventCount));
                    result = result.SelectMany(cfi => PermuteFragmentIon(settings, maxFragmentEventCount, cfi,
                        crosslinkMod.ModificationSite, linkedFragmentIonList));
                }
            }

            return result;
        }

        public IEnumerable<ComplexFragmentIon> PermuteFragmentIons(SrmSettings settings, int maxCleavageEvents,
            ModificationSite modificationSite, IEnumerable<ComplexFragmentIon> fragmentIons)
        {
            var linkedFragmentIonList = ImmutableList.ValueOf(ListComplexFragmentIons(settings, maxCleavageEvents));
            return fragmentIons.SelectMany(cfi =>
                PermuteFragmentIon(settings, maxCleavageEvents, cfi, modificationSite, linkedFragmentIonList));

        }
        private IEnumerable<ComplexFragmentIon> PermuteFragmentIon(SrmSettings settings, 
            int maxCleavageEvents,
            ComplexFragmentIon fragmentIon,
            ModificationSite modificationSite,
            IList<ComplexFragmentIon> linkedFragmentIons)
        {
            if (!fragmentIon.IncludesAaIndex(modificationSite.AaIndex))
            {
                yield return fragmentIon;
                yield break;
            }
            int fragmentCountRemaining = maxCleavageEvents - fragmentIon.GetFragmentationEventCount();
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
            foreach (var transition in transitionGroupDocNode.TransitionGroup.GetTransitions(settings,
                transitionGroupDocNode, ExplicitMods, transitionGroupDocNode.PrecursorMz,
                transitionGroupDocNode.IsotopeDist, null, null, true))
            {
                yield return new ComplexFragmentIon(transition.Transition, transition.Losses);
            }
        }

        public IEnumerable<ComplexFragmentIon> PermuteComplexFragmentIonForCrosslink(
            SrmSettings settings,
            ComplexFragmentIon complexFragmentIon,
            CrosslinkMod crossLinkMod,
            int maxFragmentEventCount)
        {
            if (!complexFragmentIon.IncludesAaIndex(crossLinkMod.IndexAa))
            {
                return new []{complexFragmentIon};
            }

            var modificationSite = crossLinkMod.ModificationSite;
            return crossLinkMod.ListComplexIonPermutations(settings,
                    maxFragmentEventCount)
                .Select(children => complexFragmentIon.ChangeChildren(children.Select(child =>
                    new KeyValuePair<ModificationSite, ComplexFragmentIon>(modificationSite, child))));
        }
    }
}
