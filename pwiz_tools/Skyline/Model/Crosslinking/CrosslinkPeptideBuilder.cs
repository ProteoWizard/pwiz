using System;
using System.Collections.Generic;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class CrosslinkPeptideBuilder
    {
        private IDictionary<FragmentIonType, MoleculeMassOffset> _fragmentedMolecules =
            new Dictionary<FragmentIonType, MoleculeMassOffset>();

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
        public MoleculeMassOffset GetFragmentFormula(FragmentIonType part)
        {
            if (part.IsEmpty)
            {
                return MoleculeMassOffset.EMPTY;
            }

            MoleculeMassOffset moleculeMassOffset;
            if (_fragmentedMolecules.TryGetValue(part, out moleculeMassOffset))
            {
                return moleculeMassOffset;
            }

            var fragmentedMolecule = GetPrecursorMolecule().ChangeFragmentIon(part.Type.Value, part.Ordinal);
                
            moleculeMassOffset = new MoleculeMassOffset(fragmentedMolecule.FragmentFormula, 0, 0);
            _fragmentedMolecules.Add(part, moleculeMassOffset);
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

        public IEnumerable<SimpleFragmentIon> GetSimpleFragmentIons(TransitionGroup transitionGroup, bool useFilter)
        {
            var transitionGroupDocNode = MakeTransitionGroupDocNode(transitionGroup);
            foreach (var transitionDocNode in transitionGroupDocNode.TransitionGroup.GetTransitions(Settings,
                transitionGroupDocNode, ExplicitMods, transitionGroupDocNode.PrecursorMz,
                transitionGroupDocNode.IsotopeDist, null, null, useFilter, false))
            {
                if (transitionDocNode.Transition.MassIndex != 0)
                {
                    continue;
                }
                yield return SimpleFragmentIon.FromDocNode(transitionDocNode);
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
