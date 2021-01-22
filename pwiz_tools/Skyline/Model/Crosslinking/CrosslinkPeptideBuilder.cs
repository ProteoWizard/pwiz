using System;
using System.Collections.Generic;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class CrosslinkPeptideBuilder
    {
        private IDictionary<Tuple<IonType, int>, MoleculeMassOffset> _fragmentedMolecules =
            new Dictionary<Tuple<IonType, int>, MoleculeMassOffset>();

        public CrosslinkPeptideBuilder(SrmSettings settings, Peptide peptide, ExplicitMods explicitMods, IsotopeLabelType labelType)
        {
            Settings = settings;
            Peptide = peptide;
            ExplicitMods = explicitMods;
            LabelType = labelType;
        }

        public SrmSettings Settings { get; }
        public Peptide Peptide { get; }
        public ExplicitMods ExplicitMods { get; }
        public IsotopeLabelType LabelType { get; }

        /// <summary>
        /// Returns the chemical formula for this fragment and none of its children.
        /// </summary>
        public MoleculeMassOffset GetFragmentFormula(Transition transition)
        {
            if (ComplexFragmentIon.IsEmptyTransition(transition))
            {
                return MoleculeMassOffset.EMPTY;
            }

            var key = Tuple.Create(transition.IonType, transition.CleavageOffset);
            MoleculeMassOffset moleculeMassOffset;
            if (_fragmentedMolecules.TryGetValue(key, out moleculeMassOffset))
            {
                return moleculeMassOffset;
            }

            var fragmentedMolecule = GetPrecursorMolecule().ChangeFragmentIon(transition.IonType, transition.Ordinal);
            moleculeMassOffset = new MoleculeMassOffset(fragmentedMolecule.FragmentFormula, 0, 0);
            _fragmentedMolecules.Add(key, moleculeMassOffset);
            return moleculeMassOffset;
        }
        private FragmentedMolecule _precursorMolecule;
        public FragmentedMolecule GetPrecursorMolecule()
        {
            if (_precursorMolecule == null)
            {
                var modifiedSequence = ModifiedSequence.GetModifiedSequence(Settings, Peptide.Sequence, ExplicitMods, LabelType);
                _precursorMolecule = FragmentedMolecule.EMPTY.ChangeModifiedSequence(modifiedSequence);
            }

            return _precursorMolecule;
        }

        public IEnumerable<ComplexFragmentIon> GetComplexFragmentIons(TransitionGroup transitionGroup, bool useFilter)
        {
            yield return ComplexFragmentIon.EmptyTransition(transitionGroup);
            var transitionGroupDocNode = MakeTransitionGroupDocNode(transitionGroup);
            foreach (var transitionDocNode in transitionGroupDocNode.TransitionGroup.GetTransitions(Settings,
                transitionGroupDocNode, ExplicitMods, transitionGroupDocNode.PrecursorMz,
                transitionGroupDocNode.IsotopeDist, null, null, useFilter, false))
            {
                if (transitionDocNode.Transition.MassIndex != 0)
                {
                    continue;
                }
                yield return new ComplexFragmentIon(ImmutableList.Singleton(transitionDocNode.Transition), transitionDocNode.Losses);
            }
        }

        public TransitionGroupDocNode MakeTransitionGroupDocNode(TransitionGroup transitionGroup)
        {
            var transitionGroupDocNode = new TransitionGroupDocNode(transitionGroup, Annotations.EMPTY, Settings, ExplicitMods, null, ExplicitTransitionGroupValues.EMPTY, null, null, false);
            return transitionGroupDocNode;
        }

        public TransitionGroup MakeTransitionGroup(IsotopeLabelType labelType, Adduct adduct)
        {
            return new TransitionGroup(Peptide, adduct, labelType);
        }
    }
}
