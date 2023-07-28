/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System.Collections.Generic;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    /// <summary>
    /// Calculates fragment masses for one peptide in a crosslinked peptide
    /// </summary>
    public class CrosslinkPeptideBuilder
    {
        private IDictionary<IonOrdinal, MoleculeMassOffset> _fragmentedMolecules =
            new Dictionary<IonOrdinal, MoleculeMassOffset>();

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
        /// Returns the chemical formula for the fragment
        /// </summary>
        public MoleculeMassOffset GetFragmentFormula(IonOrdinal part)
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
                
            moleculeMassOffset = fragmentedMolecule.FragmentFormula;
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

        public IEnumerable<SingleFragmentIon> GetSingleFragmentIons(TransitionGroup transitionGroup, bool useFilter)
        {
            yield return SingleFragmentIon.EMPTY;
            var transitionGroupDocNode = MakeTransitionGroupDocNode(transitionGroup);
            foreach (var transitionDocNode in transitionGroupDocNode.TransitionGroup.GetTransitions(Settings,
                transitionGroupDocNode, ExplicitMods, transitionGroupDocNode.PrecursorMz,
                transitionGroupDocNode.IsotopeDist, null, null, useFilter, false))
            {
                if (transitionDocNode.Transition.MassIndex != 0)
                {
                    continue;
                }
                yield return SingleFragmentIon.FromDocNode(transitionDocNode);
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
