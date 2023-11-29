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
    /// <summary>
    /// Calculates molecule and fragment formulas for a crosslinked peptide.
    /// </summary>
    public class CrosslinkBuilder
    {
        private readonly List<CrosslinkPeptideBuilder> _peptideBuilders;

        public CrosslinkBuilder(SrmSettings settings, Peptide peptide, ExplicitMods explicitMods,
            IsotopeLabelType labelType) : this(settings, new PeptideStructure(peptide, explicitMods), labelType)
        {

        }
        public CrosslinkBuilder(SrmSettings settings, PeptideStructure peptideStructure, IsotopeLabelType labelType)
        {
            Settings = settings;
            PeptideStructure = peptideStructure;
            LabelType = labelType;
            _peptideBuilders = new List<CrosslinkPeptideBuilder>();
            for (int i = 0; i < PeptideStructure.Peptides.Count; i++)
            {
                _peptideBuilders.Add(new CrosslinkPeptideBuilder(settings, PeptideStructure.Peptides[i], PeptideStructure.ExplicitModList[i], labelType));
            }
        }

        public SrmSettings Settings { get; }
        public PeptideStructure PeptideStructure { get; }
        public IsotopeLabelType LabelType { get;  }

        public TransitionDocNode MakeTransitionDocNode(ComplexFragmentIon chargedIon, IsotopeDistInfo isotopeDist = null)
        {
            return MakeTransitionDocNode(chargedIon, isotopeDist, Annotations.EMPTY,
                TransitionDocNode.TransitionQuantInfo.DEFAULT, ExplicitTransitionValues.EMPTY, null);
        }

        public TransitionDocNode MakeTransitionDocNode(TransitionGroup group, NeutralFragmentIon complexFragmentIon, Adduct adduct, IsotopeDistInfo isotopeDist = null)
        {
            var transition = complexFragmentIon.MakeTransition(group, adduct);
            var chargedIon = new ComplexFragmentIon(transition, complexFragmentIon, PeptideStructure);
            return MakeTransitionDocNode(chargedIon);
        }

        public TransitionDocNode MakeTransitionDocNode(
            ComplexFragmentIon chargedIon,
            IsotopeDistInfo isotopeDist, 
            Annotations annotations,
            TransitionDocNode.TransitionQuantInfo transitionQuantInfo,
            ExplicitTransitionValues explicitTransitionValues,
            Results<TransitionChromInfo> results)
        {
            var neutralFormula = GetNeutralFormula(chargedIon.NeutralFragmentIon);
            var productMass = GetFragmentMassFromFormula(Settings, neutralFormula);
            if (chargedIon.NeutralFragmentIon.IsMs1 && Settings.TransitionSettings.FullScan.IsHighResPrecursor)
            {
                isotopeDist = isotopeDist ?? GetPrecursorIsotopeDistInfo(chargedIon.Adduct, 0);
                productMass = isotopeDist.GetMassI(chargedIon.MassIndex, chargedIon.DecoyMassShift);
                transitionQuantInfo = transitionQuantInfo.ChangeIsotopeDistInfo(new TransitionIsotopeDistInfo(
                    isotopeDist.GetRankI(chargedIon.MassIndex), isotopeDist.GetProportionI(chargedIon.MassIndex)));
            }
            else
            {
                if (null != transitionQuantInfo.IsotopeDistInfo)
                {
                    transitionQuantInfo = transitionQuantInfo.ChangeIsotopeDistInfo(null);
                }
            }
            return new TransitionDocNode(chargedIon, annotations, productMass, transitionQuantInfo, explicitTransitionValues, results);
        }

        public MoleculeMassOffset GetNeutralFormula(NeutralFragmentIon complexFragmentIon)
        {
            var parts = new List<MoleculeMassOffset>();
            for (int i = 0; i < complexFragmentIon.IonChain.Count; i++)
            {
                parts.Add(_peptideBuilders[i].GetFragmentFormula(complexFragmentIon.IonChain[i]));
            }
            parts.AddRange(ToMoleculeMassOffsets(complexFragmentIon.Losses));
            foreach (var crosslink in PeptideStructure.Crosslinks)
            {
                if (false != complexFragmentIon.ContainsCrosslink(PeptideStructure, crosslink.Sites))
                {
                    parts.Add(crosslink.Crosslinker.GetMoleculeMassOffset());
                }
            }
            return MoleculeMassOffset.Sum(parts);
        }

        public TypedMass GetFragmentMass(NeutralFragmentIon complexFragmentIon)
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
                var moleculeMassOffset = GetPrecursorFormula();
                _precursorMassDistribution = fragmentedMoleculeSettings
                    .GetMassDistribution(moleculeMassOffset.Molecule, moleculeMassOffset.MonoMassOffset, 0);
            }

            return _precursorMassDistribution;
        }

        public MoleculeMassOffset GetPrecursorFormula()
        {
            var parts = new List<MoleculeMassOffset>();
            for (int i = 0; i < PeptideStructure.Peptides.Count; i++)
            {
                parts.Add(_peptideBuilders[i].GetPrecursorMolecule().PrecursorFormula);
            }

            foreach (var crosslink in PeptideStructure.Crosslinks)
            {
                parts.Add(crosslink.Crosslinker.GetMoleculeMassOffset());
            }
            return MoleculeMassOffset.Sum(parts);
        }

        public FragmentedMolecule.Settings GetFragmentedMoleculeSettings()
        {
            return FragmentedMolecule.Settings.FromSrmSettings(Settings);
        }

        public IEnumerable<NeutralFragmentIon> GetComplexFragmentIons(TransitionGroup transitionGroup,
            bool useFilter)
        {
            var simpleTransitions = new List<IList<SingleFragmentIon>>();
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
                simpleTransitions.Add(_peptideBuilders[i].GetSingleFragmentIons(peptideTransitionGroup, useFilter).ToList());
            }

            int maxNeutralLosses = Settings.PeptideSettings.Modifications.MaxNeutralLosses;
            return PermuteTransitions(simpleTransitions, maxNeutralLosses).SelectMany(neutralFragmentIon =>
                PermuteCleavedCrosslinks(neutralFragmentIon, maxNeutralLosses));
        }

        public IEnumerable<TransitionDocNode> GetTransitionDocNodes(TransitionGroup transitionGroup,
            double precursorMz,
            IsotopeDistInfo isotopeDist,
            Dictionary<double, LibraryRankedSpectrumInfo.RankedMI> transitionRanks,
            bool useFilter)
        {
            var complexFragmentIons = GetComplexFragmentIons(transitionGroup, useFilter);
            
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
                        RemoveUnmeasurable(precursorMz, ExpandPrecursorIsotopes(tran, isotopeDist, useFilter))).ToList();
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

        public IEnumerable<NeutralFragmentIon> PermuteTransitions(IList<IList<SingleFragmentIon>> startingIons, int maxFragmentationEventCount)
        {
            var result = new List<NeutralFragmentIon>() {null};
            for (int iPeptide = 0; iPeptide < startingIons.Count; iPeptide++)
            {
                var newResult = new List<NeutralFragmentIon>();
                foreach (var left in result)
                {
                    foreach (var right in startingIons[iPeptide])
                    {
                        var concat = right.Prepend(left);
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

        public IEnumerable<NeutralFragmentIon> PermuteCleavedCrosslinks(NeutralFragmentIon neutralFragmentIon, int maxFragmentationEventCount)
        {
            var cleavedCrosslinks = neutralFragmentIon.GetCleavedCrosslinks(PeptideStructure).ToList();
            if (cleavedCrosslinks.Count == 0)
            {
                yield return neutralFragmentIon;
                yield break;
            }

            if (cleavedCrosslinks.Count + neutralFragmentIon.CountFragmentationEvents() > maxFragmentationEventCount)
            {
                yield break;
            }

            var massType = Settings.TransitionSettings.Prediction.FragmentMassType;
            foreach (var lossList in PermuteTransitionLosses(
                         cleavedCrosslinks.Select(crosslink => crosslink.Crosslinker),
                         massType))
            {
                var transitionLosses = new TransitionLosses(lossList.ToList(), massType);
                yield return neutralFragmentIon.AddLosses(transitionLosses);
            }
        }

        public static IList<ImmutableList<TransitionLoss>> PermuteTransitionLosses(IEnumerable<StaticMod> staticMods, MassType massType)
        {
            var allLosses = new List<ImmutableList<TransitionLoss>> { ImmutableList<TransitionLoss>.EMPTY };
            foreach (var group in staticMods.GroupBy(mod => mod))
            {
                var staticMod = group.Key;
                if (staticMod.Losses == null)
                {
                    return Array.Empty<ImmutableList<TransitionLoss>>();
                }

                var transitionLosses = staticMod.Losses.Select(loss => new TransitionLoss(staticMod, loss, massType)).ToList();
                var integerCombinations = IntegerCombinations(transitionLosses.Count, group.Count());
                allLosses = allLosses.SelectMany(entry =>
                {
                    return integerCombinations.Select(indexes =>
                        ImmutableList.ValueOf(entry.Concat(indexes.Select(i => transitionLosses[i]))));
                }).ToList();
            }

            return allLosses;
        }

        private static List<ImmutableList<int>> IntegerCombinations(int range, int count)
        {
            var list = new List<ImmutableList<int>> { ImmutableList<int>.EMPTY };
            for (int position = 0; position < count; position++)
            {
                list = list.SelectMany(entry => Enumerable.Range(0, range).Select(i =>
                {
                    var newEntry = entry.Append(i).ToList();
                    newEntry.Sort();
                    return ImmutableList.ValueOf(newEntry);
                })).Distinct().ToList();
            }

            return list;
        }

        public IEnumerable<TransitionDocNode> MakeTransitionDocNodes(
            TransitionGroup transitionGroup,
            IsotopeDistInfo isotopeDist,
            IEnumerable<NeutralFragmentIon> complexFragmentIons,
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
                if (!complexFragmentIon.IsConnected(PeptideStructure))
                {
                    continue;
                }
                ICollection<Adduct> adducts;
                bool isPrecursor = complexFragmentIon.IsIonTypePrecursor;
                if (isPrecursor)
                {
                    if (excludePrecursors)
                    {
                        continue;
                    }
                    var expectedCharge = transitionGroup.PrecursorAdduct.AdductCharge;
                    if (complexFragmentIon.Losses != null)
                    {
                        expectedCharge -= complexFragmentIon.Losses.TotalCharge;
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
                    yield return MakeTransitionDocNode(transitionGroup, complexFragmentIon, adduct, isotopeDist);
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

        public static IEnumerable<MoleculeMassOffset> ToMoleculeMassOffsets(TransitionLosses transitionLosses)
        {
            if (transitionLosses == null)
            {
                return new[]{MoleculeMassOffset.EMPTY};
            }

            return transitionLosses.Losses.Select(loss => FragmentedMolecule.LossAsMoleculeMassOffset(loss.Loss));
        }
    }
}
