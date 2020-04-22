using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
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

        [CanBeNull]
        public ExplicitMods ExplicitMods { get; private set; }

        public LinkedPeptide ChangeExplicitMods(ExplicitMods explicitMods)
        {
            return ChangeProp(ImClone(this), im => im.ExplicitMods = explicitMods);
        }

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
            result = PermuteComplexFragmentIons(ExplicitMods, settings, maxFragmentEventCount, result);
            return result;
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
            if (fragmentIon.IsOrphan && !fragmentIon.IsEmptyOrphan
                || !fragmentIon.IncludesAaIndex(modificationSite.IndexAa))
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

                if (fragmentIon.IsOrphan)
                {
                    if (linkedFragmentIon.IncludesAaIndex(IndexAa))
                    {
                        continue;
                    }
                }
                else
                {
                    if (!linkedFragmentIon.IncludesAaIndex(IndexAa))
                    {
                        continue;
                    }
                }

                yield return fragmentIon.AddChild(modificationSite, linkedFragmentIon);
            }
        }

        public IEnumerable<ComplexFragmentIon> ListSimpleFragmentIons(SrmSettings settings)
        {
            var transitionGroupDocNode =
                GetTransitionGroupDocNode(settings, IsotopeLabelType.light, Adduct.SINGLY_PROTONATED);
            yield return ComplexFragmentIon.NewOrphanFragmentIon(transitionGroupDocNode.TransitionGroup, ExplicitMods, Adduct.SINGLY_PROTONATED);
            foreach (var transitionDocNode in transitionGroupDocNode.TransitionGroup.GetTransitions(settings,
                transitionGroupDocNode, ExplicitMods, transitionGroupDocNode.PrecursorMz,
                transitionGroupDocNode.IsotopeDist, null, null, true))
            {
                if (transitionDocNode.Transition.MassIndex != 0)
                {
                    continue;
                }
                yield return new ComplexFragmentIon(transitionDocNode.Transition, transitionDocNode.Losses);
            }
        }

        public LinkedPeptide ChangeGlobalMods(IList<StaticMod> staticMods, IList<StaticMod> heavyMods,
            IList<IsotopeLabelType> heavyLabelTypes)
        {
            if (null == ExplicitMods)
            {
                return this;
            }

            var newExplicitMods = ExplicitMods.ChangeGlobalMods(staticMods, heavyMods, heavyLabelTypes);
            if (ReferenceEquals(newExplicitMods, ExplicitMods))
            {
                return this;
            }

            return ChangeExplicitMods(newExplicitMods);
        }

        protected bool Equals(LinkedPeptide other)
        {
            return Equals(Peptide, other.Peptide) && IndexAa == other.IndexAa &&
                   Equals(ExplicitMods, other.ExplicitMods);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((LinkedPeptide) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Peptide != null ? Peptide.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ IndexAa;
                hashCode = (hashCode * 397) ^ (ExplicitMods != null ? ExplicitMods.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static IEnumerable<ComplexFragmentIon> PermuteComplexFragmentIons(
            [CanBeNull] ExplicitMods mods, 
            SrmSettings settings, int maxFragmentationCount, IEnumerable<ComplexFragmentIon> complexFragmentIons)
        {
            var result = complexFragmentIons;
            if (mods != null)
            {
                foreach (var crosslinkMod in mods.Crosslinks)
                {
                    result = crosslinkMod.Value.PermuteFragmentIons(settings, maxFragmentationCount,
                        crosslinkMod.Key, result);
                }
            }

            return result.Where(cfi => !cfi.IsEmptyOrphan);
        }

        public ComplexFragmentIon MakeComplexFragmentIon(IsotopeLabelType labelType, ComplexFragmentIonName complexFragmentIonName)
        {
            var transitionGroup = GetTransitionGroup(labelType, Adduct.SINGLY_PROTONATED);
            Transition transition;
            if (complexFragmentIonName.IonType == IonType.precursor)
            {
                transition = new Transition(transitionGroup, complexFragmentIonName.IonType, Peptide.Length - 1, 0, Adduct.SINGLY_PROTONATED);
            }
            else
            {
                transition = new Transition(transitionGroup, complexFragmentIonName.IonType,
                    Transition.OrdinalToOffset(complexFragmentIonName.IonType, complexFragmentIonName.Ordinal, Peptide.Length), 
                    0, Adduct.SINGLY_PROTONATED);
            }
            // TODO: losses
            var result = new ComplexFragmentIon(transition, null, complexFragmentIonName.IsOrphan);
            if (ExplicitMods != null)
            {
                foreach (var child in complexFragmentIonName.Children)
                {
                    LinkedPeptide linkedPeptide;
                    if (!ExplicitMods.Crosslinks.TryGetValue(child.Item1, out linkedPeptide))
                    {
                        throw new InvalidOperationException(@"No crosslink at " + child.Item1);
                    }
                    result = result.AddChild(child.Item1,
                        linkedPeptide.MakeComplexFragmentIon(labelType, child.Item2));
                }
            }

            return result;
        }
    }
}
