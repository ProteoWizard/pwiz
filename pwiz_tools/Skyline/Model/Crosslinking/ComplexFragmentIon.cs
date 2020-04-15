using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class ComplexFragmentIon : Immutable
    {
        public ComplexFragmentIon(Transition transition, TransitionLosses transitionLosses)
        {
            Transition = transition;
            Children = ImmutableSortedList<ModificationSite, ComplexFragmentIon>.EMPTY;
        }

        public Transition Transition { get; private set; }

        public TransitionLosses TransitionLosses { get; private set; }
        public ImmutableSortedList<ModificationSite, ComplexFragmentIon> Children { get; private set; }

        public IsotopeLabelType LabelType
        {
            get { return Transition.Group.LabelType; }
        }

        public ComplexFragmentIon ChangeChildren(IEnumerable<KeyValuePair<ModificationSite, ComplexFragmentIon>> children)
        {
            return ChangeProp(ImClone(this), im => im.Children = ImmutableSortedList.FromValues(children));
        }

        public MoleculeMassOffset GetNeutralFormula(SrmSettings settings, TransitionGroupDocNode transitionGroup,
            ExplicitMods explicitMods)
        {
            var result = transitionGroup.GetFragmentFormula(settings, explicitMods, Transition, TransitionLosses);
            foreach (var crosslinkMod in explicitMods.CrosslinkMods)
            {
                result = result.Plus(GetCrosslinkFormula(settings, crosslinkMod));
            }

            return result;
        }

        public MoleculeMassOffset GetCrosslinkFormula(SrmSettings settings, CrosslinkMod crosslinkMod)
        {
            var children = GetChildrenAtSite(crosslinkMod.ModificationSite).ToList();
            if (children.Count == 0)
            {
                return MoleculeMassOffset.EMPTY;
            }

            if (children.Count != crosslinkMod.LinkedPeptides.Count)
            {
                throw new ArgumentException();
            }
            var result =
                crosslinkMod.CrosslinkerDef.FormulaMass.GetMoleculeMassOffset(GetMassType(settings));
            for (int iChild = 0; iChild < children.Count; iChild++)
            {
                var linkedPeptide = crosslinkMod.LinkedPeptides[iChild];
                var childFragmentIon = children[iChild];
                var childTransitionGroup = linkedPeptide.GetTransitionGroupDocNode(settings, LabelType, Adduct.SINGLY_PROTONATED);
                var childFormula =
                    childFragmentIon.GetNeutralFormula(settings, childTransitionGroup, linkedPeptide.ExplicitMods);
                result = result.Plus(childFormula);
            }

            return result;
        }

        private MassType GetMassType(SrmSettings settings)
        {
            return settings.TransitionSettings.Prediction.FragmentMassType;
        }

        private IEnumerable<ComplexFragmentIon> GetChildrenAtSite(ModificationSite site)
        {
            return Children.Where(child => child.Key.Equals(site)).Select(child => child.Value);
        }
    }
}
