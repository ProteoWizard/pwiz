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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class CrosslinkBuilder
    {
        private IDictionary<ModificationSite, CrosslinkBuilder> _childBuilders = new Dictionary<ModificationSite, CrosslinkBuilder>();

        private IDictionary<Tuple<IonType, int>, MoleculeMassOffset> _fragmentedMolecules =
            new Dictionary<Tuple<IonType, int>, MoleculeMassOffset>();

        private MassDistribution _precursorMassDistribution;
        public CrosslinkBuilder(SrmSettings settings, PeptideStructure peptideStructure, IsotopeLabelType labelType)
        {
            Settings = settings;
            PeptideStructure = peptideStructure;
            LabelType = labelType;
        }

        public SrmSettings Settings { get; private set; }
        public PeptideStructure PeptideStructure { get; private set; }
        public IsotopeLabelType LabelType { get; private set; }

        public TransitionDocNode MakeTransitionDocNode(LegacyComplexFragmentIon complexFragmentIon, IsotopeDistInfo isotopeDist = null)
        {
            return MakeTransitionDocNode(complexFragmentIon, isotopeDist, Annotations.EMPTY,
                TransitionDocNode.TransitionQuantInfo.DEFAULT, ExplicitTransitionValues.EMPTY, null);
        }

        public TransitionDocNode MakeTransitionDocNode(LegacyComplexFragmentIon complexFragmentIon,
            IsotopeDistInfo isotopeDist, 
            Annotations annotations,
            TransitionDocNode.TransitionQuantInfo transitionQuantInfo,
            ExplicitTransitionValues explicitTransitionValues,
            Results<TransitionChromInfo> results)
        {
            var neutralFormula = GetNeutralFormula(complexFragmentIon);
            var productMass = GetFragmentMassFromFormula(Settings, neutralFormula);
            if (complexFragmentIon.Children.Count > 0)
            {
                complexFragmentIon = complexFragmentIon.CloneTransition();
            }

            if (complexFragmentIon.IsMs1 && Settings.TransitionSettings.FullScan.IsHighResPrecursor)
            {
                isotopeDist = isotopeDist ?? GetPrecursorIsotopeDistInfo(complexFragmentIon.Transition.Adduct, 0);
                productMass = isotopeDist.GetMassI(complexFragmentIon.Transition.MassIndex, complexFragmentIon.Transition.DecoyMassShift);
                transitionQuantInfo = transitionQuantInfo.ChangeIsotopeDistInfo(new TransitionIsotopeDistInfo(
                    isotopeDist.GetRankI(complexFragmentIon.Transition.MassIndex), isotopeDist.GetProportionI(complexFragmentIon.Transition.MassIndex)));
            }
            return new TransitionDocNode(complexFragmentIon, annotations, productMass, transitionQuantInfo, explicitTransitionValues, results);
        }

        public MoleculeMassOffset GetNeutralFormula(LegacyComplexFragmentIon complexFragmentIon)
        {
            var result = GetSimpleFragmentFormula(complexFragmentIon);
            foreach (var child in complexFragmentIon.Children)
            {
                var childBuilder = GetChildBuilder(child.Key);
                result = result.Plus(childBuilder.GetNeutralFormula(child.Value));
            }

            result = SubtractLosses(result, complexFragmentIon.TransitionLosses);
            return result;
        }

        /// <summary>
        /// Returns the chemical formula for this fragment and none of its children.
        /// </summary>
        private MoleculeMassOffset GetSimpleFragmentFormula(LegacyComplexFragmentIon complexFragmentIon)
        {
            if (complexFragmentIon.IsOrphan)
            {
                return MoleculeMassOffset.EMPTY;
            }

            var key = Tuple.Create(complexFragmentIon.Transition.IonType, complexFragmentIon.Transition.CleavageOffset);
            MoleculeMassOffset moleculeMassOffset;
            if (_fragmentedMolecules.TryGetValue(key, out moleculeMassOffset))
            {
                return moleculeMassOffset;
            }

            var fragmentedMolecule = GetSimplePrecursorMolecule().ChangeFragmentIon(complexFragmentIon.Transition.IonType, complexFragmentIon.Transition.Ordinal);
            moleculeMassOffset = new MoleculeMassOffset(fragmentedMolecule.FragmentFormula, 0, 0);
            _fragmentedMolecules.Add(key, moleculeMassOffset);
            return moleculeMassOffset;
        }

        private FragmentedMolecule _precursorMolecule;
        public FragmentedMolecule GetSimplePrecursorMolecule(ModifiedPeptide modifiedPeptide)
        {
            if (_precursorMolecule == null)
            {
                var modifiedSequence = ModifiedSequence.GetModifiedSequence(Settings, modifiedPeptide.Peptide.Sequence, modifiedPeptide.ExplicitMods, LabelType)
                    .SeverCrosslinks();
                _precursorMolecule = FragmentedMolecule.EMPTY.ChangeModifiedSequence(modifiedSequence);
            }

            return _precursorMolecule;
        }

        public TypedMass GetFragmentMass(LegacyComplexFragmentIon complexFragmentIon)
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
            var moleculeMassOffset = new MoleculeMassOffset(GetSimplePrecursorMolecule().PrecursorFormula);
            if (ExplicitMods != null)
            {
                foreach (var child in ExplicitMods.LinkedCrossslinks)
                {
                    moleculeMassOffset = moleculeMassOffset.Plus(GetChildBuilder(child.Key).GetPrecursorFormula());
                }
            }

            return moleculeMassOffset;
        }

        public FragmentedMolecule.Settings GetFragmentedMoleculeSettings()
        {
            return FragmentedMolecule.Settings.FromSrmSettings(Settings);
        }

        private CrosslinkBuilder GetChildBuilder(ModificationSite modificationSite)
        {
            CrosslinkBuilder childBuilder;
            if (!_childBuilders.TryGetValue(modificationSite, out childBuilder))
            {
                LinkedPeptide linkedPeptide;
                ExplicitMods.Crosslinks.TryGetValue(modificationSite, out linkedPeptide);
                childBuilder = new CrosslinkBuilder(Settings, linkedPeptide.Peptide, linkedPeptide.ExplicitMods, LabelType);
                _childBuilders.Add(modificationSite, childBuilder);
            }

            return childBuilder;
        }

        public IEnumerable<TransitionDocNode> GetComplexTransitions(TransitionGroup transitionGroup,
            double precursorMz,
            IsotopeDistInfo isotopeDist,
            Dictionary<double, LibraryRankedSpectrumInfo.RankedMI> transitionRanks,
            IEnumerable<TransitionDocNode> simpleTransitions,
            bool useFilter)
        {
            var allTransitions =
                RemoveUnmeasurable(precursorMz,
                        RemoveDuplicates(
                            GetAllComplexTransitions(transitionGroup, isotopeDist, simpleTransitions, useFilter)))
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

        public IEnumerable<TransitionDocNode> GetAllComplexTransitions(
            TransitionGroup transitionGroup,
            IsotopeDistInfo isotopeDist,
            IEnumerable<TransitionDocNode> simpleTransitions, 
            bool useFilter)
        {
            var startingFragmentIons = new List<LegacyComplexFragmentIon>();
            var productAdducts = Settings.TransitionSettings.Filter.PeptideProductCharges.ToHashSet();
            var precursorLosses = new HashSet<TransitionLosses>();

            foreach (var simpleTransition in simpleTransitions)
            {
                var startingFragmentIon = simpleTransition.ComplexFragmentIon
                    .ChangeCrosslinkStructure(ExplicitMods.Crosslinks);
                startingFragmentIons.Add(startingFragmentIon);
                if (startingFragmentIon.IsIonTypePrecursor)
                {
                    precursorLosses.Add(startingFragmentIon.TransitionLosses);
                }
            }

            bool excludePrecursors = false;
            IEnumerable<Adduct> allProductAdducts;
            if (useFilter)
            {
                allProductAdducts = Settings.TransitionSettings.Filter.PeptideProductCharges;
                excludePrecursors = !Settings.TransitionSettings.Filter.PeptideIonTypes.Contains(IonType.precursor);
            }
            else
            {
                allProductAdducts = Settings.TransitionSettings.Filter.PeptideProductCharges
                    .Concat(Transition.DEFAULT_PEPTIDE_CHARGES);
            }

            allProductAdducts = allProductAdducts.Append(transitionGroup.PrecursorAdduct);

            // Add ions representing the precursor waiting to be joined with a crosslinked peptide
            foreach (var productAdduct in allProductAdducts.Distinct())
            {
                foreach (var transitionLosses in precursorLosses)
                {
                    if (productAdduct.IsValidProductAdduct(transitionGroup.PrecursorAdduct, null))
                    {
                        var precursorTransition = new Transition(transitionGroup, IonType.precursor,
                            Peptide.Sequence.Length - 1, 0, productAdduct);

                        startingFragmentIons.Add(new LegacyComplexFragmentIon(precursorTransition, transitionLosses, ExplicitMods.Crosslinks, true));
                        startingFragmentIons.Add(new LegacyComplexFragmentIon(precursorTransition, transitionLosses, ExplicitMods.Crosslinks));
                    }
                }
            }

            foreach (var complexFragmentIon in LinkedPeptide.PermuteComplexFragmentIons(ExplicitMods, Settings,
                Settings.PeptideSettings.Modifications.MaxNeutralLosses, useFilter, startingFragmentIons.Distinct()).Distinct())
            {
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
                        if (complexFragmentIon.Transition.MassIndex != 0)
                        {
                            continue;
                        }
                        expectedCharge -= complexFragmentIon.TransitionLosses.TotalCharge;
                    }

                    if (expectedCharge != complexFragmentIon.Transition.Adduct.AdductCharge)
                    {
                        continue;
                    }
                }
                else
                {
                    if (complexFragmentIon.Transition.MassIndex != 0)
                    {
                        continue;
                    }
                    if (useFilter)
                    {
                        if (!productAdducts.Contains(complexFragmentIon.Transition.Adduct))
                        {
                            continue;
                        }
                    }
                }

                if (!complexFragmentIon.Transition.Adduct.IsValidProductAdduct(transitionGroup.PrecursorAdduct,
                    complexFragmentIon.TransitionLosses))
                {
                    continue;
                }

                var complexTransitionDocNode = MakeTransitionDocNode(complexFragmentIon, isotopeDist);
                yield return complexTransitionDocNode;
            }
        }

        public IEnumerable<LegacyComplexFragmentIon> PermuteComplexFragmentIons(int maxFragmentationCount, bool useFilter, IEnumerable<LegacyComplexFragmentIon> startingFragmentIons)
        {
            var result = startingFragmentIons;
            if (ExplicitMods != null)
            {
                foreach (var crosslinkMod in ExplicitMods.Crosslinks)
                {
                    result = crosslinkMod.Value.PermuteFragmentIons(Settings, maxFragmentationCount, useFilter,
                        crosslinkMod.Key, result);
                }
            }

            return result.Where(cfi => !cfi.IsEmptyOrphan);
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
