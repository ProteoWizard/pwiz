/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class CrosslinkBuilder
    {
        private ImmutableList<CrosslinkPeptideBuilder> _peptideBuilders;
        public CrosslinkBuilder(SrmSettings settings, PeptideStructure peptideStructure, IsotopeLabelType labelType)
        {
            Settings = settings;
            PeptideStructure = peptideStructure;
            LabelType = labelType;
            _peptideBuilders = ImmutableList.ValueOf(peptideStructure.Peptides.Select(pep =>
                new CrosslinkPeptideBuilder(settings, pep.Peptide, pep.ExplicitMods, labelType)));
        }

        public SrmSettings Settings { get; private set; }
        public PeptideStructure PeptideStructure { get; private set; }
        public IsotopeLabelType LabelType { get; private set; }

        public TransitionDocNode MakeTransitionDocNode(ComplexFragmentIon complexFragmentIon, IsotopeDistInfo isotopeDist = null)
        {
            return MakeTransitionDocNode(complexFragmentIon, isotopeDist, Annotations.EMPTY,
                TransitionDocNode.TransitionQuantInfo.DEFAULT, ExplicitTransitionValues.EMPTY, null);
        }

        public TransitionDocNode MakeTransitionDocNode(ComplexFragmentIon complexFragmentIon,
            IsotopeDistInfo isotopeDist, 
            Annotations annotations,
            TransitionDocNode.TransitionQuantInfo transitionQuantInfo,
            ExplicitTransitionValues explicitTransitionValues,
            Results<TransitionChromInfo> results)
        {
            var neutralFormula = GetNeutralFormula(complexFragmentIon);
            var productMass = GetFragmentMassFromFormula(Settings, neutralFormula);
            if (true) // TODO(nicksh): make sure this is right
            {
                complexFragmentIon = complexFragmentIon.CloneTransition();
            }

            if (complexFragmentIon.IsMs1 && Settings.TransitionSettings.FullScan.IsHighResPrecursor)
            {
                isotopeDist = isotopeDist ?? GetPrecursorIsotopeDistInfo(complexFragmentIon.PrimaryTransition.Adduct, 0);
                productMass = isotopeDist.GetMassI(complexFragmentIon.PrimaryTransition.MassIndex, complexFragmentIon.PrimaryTransition.DecoyMassShift);
                transitionQuantInfo = transitionQuantInfo.ChangeIsotopeDistInfo(new TransitionIsotopeDistInfo(
                    isotopeDist.GetRankI(complexFragmentIon.PrimaryTransition.MassIndex), isotopeDist.GetProportionI(complexFragmentIon.PrimaryTransition.MassIndex)));
            }
            return new TransitionDocNode(complexFragmentIon, annotations, productMass, transitionQuantInfo, explicitTransitionValues, results);
        }

        public MoleculeMassOffset GetNeutralFormula(ComplexFragmentIon complexFragmentIon)
        {
            MoleculeMassOffset result = MoleculeMassOffset.EMPTY;
            for (int i = 0; i < complexFragmentIon.Transitions.Count; i++)
            {
                result = result.Plus(_peptideBuilders[i].GetFragmentFormula(complexFragmentIon.Transitions[i]));
            }

            return result;
        }


        private FragmentedMolecule _precursorMolecule;
        public TypedMass GetFragmentMass(ComplexFragmentIon complexFragmentIon)
        {
            var neutralFormula = GetNeutralFormula(complexFragmentIon);
            return GetFragmentMassFromFormula(Settings, neutralFormula);
        }

        public static TypedMass GetFragmentMassFromFormula(SrmSettings settings, MoleculeMassOffset formula)
        {
            var fragmentedMoleculeSettings = FragmentedMolecule.Settings.FromSrmSettings(settings);
            MassType massType = settings.TransitionSettings.Prediction.FragmentMassType;
            if (massType.IsMonoisotopic())
            {
                return new TypedMass(fragmentedMoleculeSettings.GetMonoMass(formula.Molecule) + formula.MonoMassOffset + BioMassCalc.MassProton, MassType.MonoisotopicMassH);
            }
            else
            {
                return new TypedMass(fragmentedMoleculeSettings.GetAverageMass(formula.Molecule) + formula.AverageMassOffset + BioMassCalc.MassProton, MassType.AverageMassH);
            }
        }

        public IsotopeDistInfo GetPrecursorIsotopeDistInfo(Adduct adduct, double decoyMassShift)
        {
            var massDistribution = GetPrecursorMassDistribution();
            var mzDistribution = massDistribution.OffsetAndDivide(
                adduct.AdductCharge * (BioMassCalc.MassProton + decoyMassShift), adduct.AdductCharge);

            return IsotopeDistInfo.MakeIsotopeDistInfo(mzDistribution, GetPrecursorMass(MassType.MonoisotopicMassH), adduct, Settings.TransitionSettings.FullScan);
        }

        public TypedMass GetPrecursorMass(MassType massType)
        {
            var formula = GetPrecursorFormula();
            var fragmentedMoleculeSettings = GetFragmentedMoleculeSettings();
            double mass = massType.IsMonoisotopic()
                ? fragmentedMoleculeSettings.GetMonoMass(formula)
                : fragmentedMoleculeSettings.GetAverageMass(formula);
            if (massType.IsMassH())
            {
                mass += BioMassCalc.MassProton;
            }

            return new TypedMass(mass, massType);
        }

        private MassDistribution _precursorMassDistribution;
        public MassDistribution GetPrecursorMassDistribution()
        {
            if (_precursorMassDistribution == null)
            {
                var fragmentedMoleculeSettings = FragmentedMolecule.Settings.FromSrmSettings(Settings);
                _precursorMassDistribution = fragmentedMoleculeSettings
                    .GetMassDistribution(GetPrecursorFormula().Molecule, 0, 0);
            }

            return _precursorMassDistribution;
        }

        public MoleculeMassOffset GetPrecursorFormula()
        {
            var moleculeMassOffset = MoleculeMassOffset.EMPTY;
            for (int i = 0; i < PeptideStructure.Peptides.Count; i++)
            {
                moleculeMassOffset =
                    moleculeMassOffset.Plus(
                        new MoleculeMassOffset(_peptideBuilders[i].GetPrecursorMolecule().PrecursorFormula));
            }
            return moleculeMassOffset;
        }

        public FragmentedMolecule.Settings GetFragmentedMoleculeSettings()
        {
            return FragmentedMolecule.Settings.FromSrmSettings(Settings);
        }

        public IEnumerable<ComplexFragmentIon> GetComplexFragmentIons(TransitionGroup transitionGroup,
            double precursorMz,
            bool useFilter)
        {
            var simpleTransitions = new List<IList<ComplexFragmentIon>>();
            for (int i = 0; i < _peptideBuilders.Count; i++)
            {
                var builder = _peptideBuilders[i];
                TransitionGroup peptideTransitionGroup;
                if (i == 0)
                {
                    peptideTransitionGroup = transitionGroup;
                }
                else
                {
                    peptideTransitionGroup = builder.MakeTransitionGroup(transitionGroup.LabelType, Adduct.SINGLY_PROTONATED);
                }
                simpleTransitions.Add(_peptideBuilders[i].GetComplexFragmentIons(peptideTransitionGroup, useFilter).ToList());
            }

            return PermuteTransitions(simpleTransitions, Settings.PeptideSettings.Modifications.MaxNeutralLosses);
        }

        public IEnumerable<TransitionDocNode> GetTransitionDocNodes(TransitionGroup transitionGroup,
            double precursorMz,
            IsotopeDistInfo isotopeDist,
            Dictionary<double, LibraryRankedSpectrumInfo.RankedMI> transitionRanks,
            bool useFilter)
        {
            var complexFragmentIons = GetComplexFragmentIons(transitionGroup, precursorMz, useFilter);
            
            var allTransitions =
                RemoveUnmeasurable(precursorMz,
                        RemoveDuplicates(
                            MakeTransitionDocNodes(transitionGroup, isotopeDist, complexFragmentIons, useFilter)))
                    .OrderBy(tran => tran.ComplexFragmentIon)
                    .ToList();
            
            IList<TransitionDocNode> ms2transitions;
            IList<TransitionDocNode> ms1transitions;
            if (Settings.TransitionSettings.FullScan.IsEnabledMs)
            {
                ms2transitions = allTransitions.Where(tran => !tran.IsMs1).ToList();
                ms1transitions = allTransitions.Where(tran => tran.IsMs1).ToList();
                if (Settings.TransitionSettings.FullScan.IsHighResPrecursor)
                {
                    ms1transitions = ms1transitions.SelectMany(tran =>
                        ExpandPrecursorIsotopes(tran, isotopeDist, useFilter)).ToList();
                }
            }
            else
            {
                ms2transitions = allTransitions;
                ms1transitions = new TransitionDocNode[0];
            }

            if (useFilter)
            {
                if (!Settings.TransitionSettings.Filter.PeptideIonTypes.Contains(IonType.precursor))
                {
                    ms1transitions = new TransitionDocNode[0];
                }

                ms2transitions = FilterTransitions(transitionRanks, ms2transitions).ToList();
            }

            return ms1transitions.Concat(ms2transitions);
        }

        public IEnumerable<ComplexFragmentIon> PermuteTransitions(IList<IList<ComplexFragmentIon>> startingIons, int maxFragmentationEventCount)
        {
            var result = new List<ComplexFragmentIon>() {ComplexFragmentIon.EMPTY};
            for (int iPeptide = 0; iPeptide < startingIons.Count; iPeptide++)
            {
                var newResult = new List<ComplexFragmentIon>();
                foreach (var left in result)
                {
                    foreach (var right in startingIons[iPeptide])
                    {
                        var concat = left.Concat(right);
                        if (concat.CountFragmentationEvents() > maxFragmentationEventCount)
                        {
                            continue;
                        }

                        if (!concat.IsAllowed(PeptideStructure))
                        {
                            continue;
                        }
                        newResult.Add(concat);
                    }
                }

                result = newResult;
            }

            return result;
        }

        public IEnumerable<TransitionDocNode> MakeTransitionDocNodes(
            TransitionGroup transitionGroup,
            IsotopeDistInfo isotopeDist,
            IEnumerable<ComplexFragmentIon> complexFragmentIons,
            bool useFilter)
        {
            bool excludePrecursors = false;
            HashSet<Adduct> productAdducts = new HashSet<Adduct>(Settings.TransitionSettings.Filter.PeptideProductCharges);
            if (useFilter)
            {
                excludePrecursors = !Settings.TransitionSettings.Filter.PeptideIonTypes.Contains(IonType.precursor);
            }
            else
            {
                productAdducts.UnionWith(Transition.DEFAULT_PEPTIDE_CHARGES);
            }

            foreach (var complexFragmentIon in complexFragmentIons)
            {
                ICollection<Adduct> adducts;
                bool isPrecursor = complexFragmentIon.IsIonTypePrecursor;
                if (isPrecursor)
                {
                    if (excludePrecursors)
                    {
                        continue;
                    }
                    var expectedCharge = transitionGroup.PrecursorAdduct.AdductCharge;
                    if (complexFragmentIon.TransitionLosses != null)
                    {
                        expectedCharge -= complexFragmentIon.TransitionLosses.TotalCharge;
                    }

                    if (expectedCharge <= 0)
                    {
                        continue;
                    }

                    adducts = new[] {Adduct.FromChargeProtonated(expectedCharge)};
                }
                else
                {
                    adducts = productAdducts;
                }

                foreach (var adduct in adducts)
                {
                    yield return MakeTransitionDocNode(complexFragmentIon.ChangeAdduct(adduct), isotopeDist);
                }
            }
        }

        public IEnumerable<TransitionDocNode> ExpandPrecursorIsotopes(TransitionDocNode transitionNode, IsotopeDistInfo isotopeDist, bool useFilter)
        {
            var fullScan = Settings.TransitionSettings.FullScan;
            foreach (int massIndex in fullScan.SelectMassIndices(isotopeDist, useFilter))
            {
                var complexFragmentIon = transitionNode.ComplexFragmentIon.ChangeMassIndex(massIndex);
                yield return MakeTransitionDocNode(complexFragmentIon, isotopeDist);
            }
        }

        public IEnumerable<TransitionDocNode> FilterTransitions(Dictionary<double, LibraryRankedSpectrumInfo.RankedMI> transitionRanks, IEnumerable<TransitionDocNode> transitions)
        {
            if (transitionRanks == null || transitionRanks.Count == 0)
            {
                return transitions;
            }

            var pick = Settings.TransitionSettings.Libraries.Pick;
            if (pick != TransitionLibraryPick.all && pick != TransitionLibraryPick.filter)
            {
                return transitions;
            }

            var tranRanks = new List<Tuple<LibraryRankedSpectrumInfo.RankedMI, TransitionDocNode>>();
            foreach (var transition in transitions)
            {
                LibraryRankedSpectrumInfo.RankedMI rankedMI;
                if (!transitionRanks.TryGetValue(transition.Mz, out rankedMI))
                {
                    continue;
                }

                var complexIonName = transition.ComplexFragmentIon.GetName();
                var matchedIon =
                    rankedMI.MatchedIons.FirstOrDefault(ion => Equals(complexIonName, ion.ComplexFragmentIonName));
                if (matchedIon == null)
                {
                    continue;
                }
                tranRanks.Add(Tuple.Create(rankedMI, transition));
            }

            int ionCount = Settings.TransitionSettings.Libraries.IonCount;
            if (ionCount < tranRanks.Count)
            {
                var rankValues = tranRanks.Select(tuple => tuple.Item1.Rank).ToList();
                rankValues.Sort();
                int cutoff = rankValues[ionCount];
                tranRanks = tranRanks.Where(tuple => tuple.Item1.Rank < cutoff).ToList();
            }

            return tranRanks.Select(tuple => tuple.Item2);
        }

        public IEnumerable<TransitionDocNode> RemoveUnmeasurable(double precursorMz, IEnumerable<TransitionDocNode> transitions)
        {
            var instrumentSettings = Settings.TransitionSettings.Instrument;
            foreach (var transition in transitions)
            {
                if (transition.ComplexFragmentIon.IsMs1)
                {
                    if (!instrumentSettings.IsMeasurable(transition.Mz))
                    {
                        continue;
                    }
                }
                else
                {
                    if (!instrumentSettings.IsMeasurable(transition.Mz, precursorMz))
                    {
                        continue;
                    }
                }

                yield return transition;
            }
        }
        private static IEnumerable<TransitionDocNode> RemoveDuplicates(IEnumerable<TransitionDocNode> transitions)
        {
            var keys = new HashSet<TransitionLossKey>();
            foreach (var transition in transitions)
            {
                var key = transition.Key(null);
                if (keys.Add(key))
                {
                    yield return transition;
                }
            }
        }

        public MoleculeMassOffset SubtractLosses(MoleculeMassOffset moleculeMassOffset, TransitionLosses transitionLosses)
        {
            if (transitionLosses == null)
            {
                return moleculeMassOffset;
            }

            foreach (var loss in transitionLosses.Losses)
            {
                moleculeMassOffset = moleculeMassOffset.Minus(FragmentedMolecule.ToMoleculeMassOffset(loss.Loss));
            }

            return moleculeMassOffset;
        }

        
    }
}
