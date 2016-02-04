/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class TransitionGroup : Identity
    {
        public const int MIN_PRECURSOR_CHARGE = 1;
        public const int MAX_PRECURSOR_CHARGE = 80;
        public const int MAX_PRECURSOR_CHARGE_PICK = 6;

        /// <summary>
        /// From mProphet paper: shift precursor m/z by 10
        /// </summary>
        public const int ALTERED_SEQUENCE_DECOY_MZ_SHIFT = 10;
        public const int MIN_PRECURSOR_DECOY_MASS_SHIFT = -30;
        public const int MAX_PRECURSOR_DECOY_MASS_SHIFT = 30;

        public static ICollection<int> MassShifts { get { return MASS_SHIFTS; } }

        private static readonly HashSet<int> MASS_SHIFTS = new HashSet<int>(MassShiftEnum);
        
        private static IEnumerable<int> MassShiftEnum
        {
            get
            {
                yield return ALTERED_SEQUENCE_DECOY_MZ_SHIFT;
                for (int i = MIN_PRECURSOR_DECOY_MASS_SHIFT; i < MAX_PRECURSOR_DECOY_MASS_SHIFT; i++)
                    yield return i;
            }
        }

        private readonly Peptide _peptide;

        public TransitionGroup(Peptide peptide, DocNodeCustomIon customIon, int precursorCharge, IsotopeLabelType labelType)
            : this(peptide, customIon, precursorCharge, labelType, false, null)
        {
        }

        public TransitionGroup(Peptide peptide, int precursorCharge, IsotopeLabelType labelType, bool unlimitedCharge, int? decoyMassShift)
            : this(peptide, null, precursorCharge, labelType, unlimitedCharge, decoyMassShift)
        {
        }

        public TransitionGroup(Peptide peptide, DocNodeCustomIon customIon, int precursorCharge, IsotopeLabelType labelType, bool unlimitedCharge, int? decoyMassShift)
        {
            _peptide = peptide;
            CustomIon = customIon ?? Peptide.CustomIon; // If parent molecule is a custom ion, and none specified, use that
            PrecursorCharge = precursorCharge;
            LabelType = labelType;
            DecoyMassShift = decoyMassShift;

            Validate(unlimitedCharge ? 0 : precursorCharge);
        }

        /// <summary>
        /// Our parent molecule
        /// </summary>
        public Peptide Peptide { get { return _peptide; } }

        /// <summary>
        /// This typically matches the parent molecule's custom ion if we're the first transition group, but not necessarily
        /// </summary>
        public DocNodeCustomIon CustomIon { get; private set; }

        public int PrecursorCharge { get; private set; }
        public IsotopeLabelType LabelType { get; private set; }
        public int? DecoyMassShift { get; private set; }
        public bool IsCustomIon { get { return CustomIon != null; } }
        public string LabelTypeText
        {
            get { return (!LabelType.IsLight ? " (" + LabelType + ")" : string.Empty); } // Not L10N
        }

        public static int CompareTransitions(TransitionDocNode node1, TransitionDocNode node2)
        {
            Transition tran1 = node1.Transition, tran2 = node2.Transition;
            int diffType = GetOrder(tran1.IonType) - GetOrder(tran2.IonType);
            if (diffType != 0)
                return diffType;
            int diffCharge = tran1.Charge - tran2.Charge;
            if (diffCharge != 0)
                return diffCharge;
            int diffOffset = tran1.CleavageOffset - tran2.CleavageOffset;
            if (diffOffset != 0)
                return diffOffset;
            return Comparer<double>.Default.Compare(node1.LostMass, node2.LostMass);
        }

        private static int GetOrder(IonType ionType)
        {
            int i = (int) ionType;
            return i < 0 ? i : Transition.ALL_TYPE_ORDERS[i];
        }

        public TransitionDocNode[] GetMatchingTransitions(SrmSettings settings,
                                                          TransitionGroupDocNode nodeGroupMatching,
                                                          ExplicitMods mods)
        {
            // If no calculator for this type, then not possible to calculate transtions
            var calc = settings.GetFragmentCalc(LabelType, mods);
            if (calc == null)
                return null;

            var listTrans = new List<TransitionDocNode>();
            foreach (TransitionDocNode nodeTran in nodeGroupMatching.Children)
            {
                var nodeTranMatching = GetMatchingTransition(settings, nodeGroupMatching, nodeTran, calc);
                listTrans.Add(nodeTranMatching);
            }
            return listTrans.ToArray();
        }

        private TransitionDocNode GetMatchingTransition(SrmSettings settings,
                                                        TransitionGroupDocNode nodeGroupMatching,
                                                        TransitionDocNode nodeTran,
                                                        IFragmentMassCalc calc)
        {
            var transition = nodeTran.Transition;
            var losses = nodeTran.Losses;
            var libInfo = nodeTran.LibInfo;
            int? decoyMassShift = transition.IsPrecursor() ? DecoyMassShift : transition.DecoyMassShift;
            var tranNew = new Transition(this,
                transition.IonType,
                transition.CleavageOffset,
                transition.MassIndex,
                transition.Charge,
                decoyMassShift,
                transition.CustomIon ?? CustomIon); // Handle reporter ions as well as small molecules
            var isotopeDist = nodeGroupMatching.IsotopeDist;
            double massH;
            if (tranNew.IsCustom())
            {
                massH = tranNew.CustomIon.GetMass(settings.TransitionSettings.Prediction.FragmentMassType);
            }
            else
            {
                massH = calc.GetFragmentMass(tranNew, isotopeDist);
            }
            var isotopeDistInfo = TransitionDocNode.GetIsotopeDistInfo(tranNew, losses, isotopeDist);
            var nodeTranMatching = new TransitionDocNode(tranNew, losses, massH, isotopeDistInfo, libInfo);
            return nodeTranMatching;
        }

        /// <summary>
        /// True to force checking of all isotope label types for exclusions affecting a transition
        /// to avoid the case where a transition is accepted for one label type but not all types.
        /// </summary>
        public static bool IsAvoidMismatchedIsotopeTransitions { get { return true; } }

        public IEnumerable<TransitionDocNode> GetTransitions(SrmSettings settings,
                                                             TransitionGroupDocNode groupDocNode,
                                                             ExplicitMods mods,
                                                             double precursorMz,
                                                             IsotopeDistInfo isotopeDist,
                                                             SpectrumHeaderInfo libInfo,
                                                             IDictionary<double, LibraryRankedSpectrumInfo.RankedMI> transitionRanks,
                                                             bool useFilter)
        {
            Assume.IsTrue(ReferenceEquals(groupDocNode.TransitionGroup, this));
            // Get necessary mass calculators and masses
            var calcFilterPre = settings.GetPrecursorCalc(IsotopeLabelType.light, mods);
            var calcFilter = settings.GetFragmentCalc(IsotopeLabelType.light, mods);
            var calcPredict = settings.GetFragmentCalc(LabelType, mods);

            string sequence = Peptide.Sequence;

            // Save the true precursor m/z for TranstionSettings.Accept() now that all isotope types are
            // checked.  This is more correct than just using the light precursor m/z for precursor window
            // exclusion.
            double precursorMzAccept = precursorMz;
            if (!ReferenceEquals(calcFilter, calcPredict))
            {
                // Get the normal precursor m/z for filtering, so that light and heavy ion picks will match.
                precursorMz = IsCustomIon ? 
                    BioMassCalc.CalculateIonMz(calcFilterPre.GetPrecursorMass(groupDocNode.CustomIon), groupDocNode.TransitionGroup.PrecursorCharge) : 
                    SequenceMassCalc.GetMZ(calcFilterPre.GetPrecursorMass(sequence), groupDocNode.TransitionGroup.PrecursorCharge);
            }
            if (!IsAvoidMismatchedIsotopeTransitions)
                precursorMzAccept = precursorMz;

            var tranSettings = settings.TransitionSettings;
            var filter = tranSettings.Filter;
            var charges = filter.ProductCharges;
            var startFinder = filter.FragmentRangeFirst;
            var endFinder = filter.FragmentRangeLast;
            double precursorMzWindow = filter.PrecursorMzWindow;
            var types = filter.IonTypes;
            MassType massType = tranSettings.Prediction.FragmentMassType;
            int minMz = tranSettings.Instrument.GetMinMz(precursorMzAccept);
            int maxMz = tranSettings.Instrument.MaxMz;

            var pepMods = settings.PeptideSettings.Modifications;
            var potentialLosses = CalcPotentialLosses(sequence, pepMods, mods, massType);

            // A start m/z will need to be calculated if the start fragment
            // finder uses m/z and their are losses to consider.  If the filter
            // is set to only consider fragments with m/z greater than the
            // precursor, the code below needs to also prevent loss fragments
            // from being under that m/z.
            double startMz = 0;

            // Get library settings
            var pick = tranSettings.Libraries.Pick;
            if (!useFilter)
            {
                pick = TransitionLibraryPick.all;
                var listAll = Transition.ALL_CHARGES.ToList();
                listAll.AddRange(charges.Where(c => !Transition.ALL_CHARGES.Contains(c)));
                listAll.Sort();
                charges = listAll.ToArray();
                types = Transition.ALL_TYPES;
            }
            // If there are no libraries or no library information, then
            // picking cannot use library information
            else if (!settings.PeptideSettings.Libraries.HasLibraries || libInfo == null)
                pick = TransitionLibraryPick.none;

            // If filtering without library picking
            if (potentialLosses != null)
            {
                if (pick == TransitionLibraryPick.none)
                {
                    // Only include loss combinations where all losses are included always
                    potentialLosses = potentialLosses.Where(losses =>
                        losses.All(loss => loss.TransitionLoss.Loss.Inclusion == LossInclusion.Always)).ToArray();
                }
                else if (useFilter)
                {
                    // Exclude all losses which should never be included by default
                    potentialLosses = potentialLosses.Where(losses =>
                        losses.All(loss => loss.TransitionLoss.Loss.Inclusion != LossInclusion.Never)).ToArray();
                }
                if (!potentialLosses.Any())
                    potentialLosses = null;
            }

            // Return precursor ions
            if (!useFilter || types.Contains(IonType.precursor))
            {
                bool libraryFilter = (pick == TransitionLibraryPick.all || pick == TransitionLibraryPick.filter);
                foreach (var nodeTran in GetPrecursorTransitions(settings, mods, calcFilterPre, calcPredict,
                    precursorMz, isotopeDist, potentialLosses, transitionRanks, libraryFilter, useFilter))
                {
                    if (minMz <= nodeTran.Mz && nodeTran.Mz <= maxMz)
                        yield return nodeTran;
                }
            }

            // Return special ions from settings, if this is a peptide
            if (!IsCustomIon)
            {
                // This is a peptide, but it may have custom transitions (reporter ions), check those
                foreach (var measuredIon in tranSettings.Filter.MeasuredIons.Where(m => m.IsCustom))
                {
                    if (useFilter && measuredIon.IsOptional)
                        continue;
                    var tran = new Transition(this, measuredIon.Charge, null, measuredIon.CustomIon);
                    double mass = settings.GetFragmentMass(IsotopeLabelType.light, null, tran, null);
                    var nodeTran = new TransitionDocNode(tran, null, mass, null, null);
                    if (minMz <= nodeTran.Mz && nodeTran.Mz <= maxMz)
                        yield return nodeTran;
                }
            }

            // For small molecules we can't generate new nodes, so just mz filter those we have
            foreach (var nodeTran in groupDocNode.Transitions.Where(tran => tran.Transition.IsNonPrecursorNonReporterCustomIon()))
            {
                if (minMz <= nodeTran.Mz && nodeTran.Mz <= maxMz)
                    yield return nodeTran;
            }

            if (sequence == null) // Completely custom
                yield break;

            // If picking relies on library information
            if (useFilter && pick != TransitionLibraryPick.none)
            {
                // If it is not yet loaded, or nothing got ranked, return an empty enumeration
                if (!settings.PeptideSettings.Libraries.IsLoaded ||
                    (transitionRanks != null && transitionRanks.Count == 0))
                {
                    yield break;
                }
            }

            double[,] massesPredict = calcPredict.GetFragmentIonMasses(sequence);
            int len = massesPredict.GetLength(1);
            if (len == 0)
                yield break;

            double[,] massesFilter = massesPredict;
            if (!ReferenceEquals(calcFilter, calcPredict))
            {
                // Get the normal m/z values for filtering, so that light and heavy
                // ion picks will match.
                massesFilter = calcFilter.GetFragmentIonMasses(sequence);
            }

            // Get types other than this to make sure matches are possible for all types
            var listOtherTypes = new List<Tuple<TransitionGroupDocNode, IFragmentMassCalc>>();
            foreach (var labelType in settings.PeptideSettings.Modifications.GetModificationTypes())
            {
                if (Equals(labelType, LabelType))
                    continue;
                var calc = settings.GetFragmentCalc(labelType, mods);
                if (calc == null)
                    continue;
                var tranGroupOther = new TransitionGroup(Peptide, PrecursorCharge, labelType, false, DecoyMassShift);
                var nodeGroupOther = new TransitionGroupDocNode(tranGroupOther, Annotations.EMPTY, settings, mods,
                    libInfo, ExplicitTransitionGroupValues.EMPTY, null, new TransitionDocNode[0], false);

                listOtherTypes.Add(new Tuple<TransitionGroupDocNode, IFragmentMassCalc>(nodeGroupOther, calc));
            }

            // Loop over potential product ions picking transitions
            foreach (IonType type in types)
            {
                // Precursor type is handled above.
                if (type == IonType.precursor)
                    continue;

                foreach (int charge in charges)
                {
                    // Precursor charge can never be lower than product ion charge.
                    if (Math.Abs(PrecursorCharge) < Math.Abs(charge))
                        continue;

                    int start = 0, end = 0;
                    if (pick != TransitionLibraryPick.all)
                    {
                        start = startFinder.FindStartFragment(massesFilter, type, charge,
                            precursorMz, precursorMzWindow, out startMz);
                        end = endFinder.FindEndFragment(type, start, len);
                        if (Transition.IsCTerminal(type))
                            Helpers.Swap(ref start, ref end);
                    }

                    for (int i = 0; i < len; i++)
                    {
                        // Get the predicted m/z that would be used in the transition
                        double massH = massesPredict[(int)type, i];
                        foreach (var losses in CalcTransitionLosses(type, i, massType, potentialLosses))
                        {
                            double ionMz = SequenceMassCalc.GetMZ(Transition.CalcMass(massH, losses), charge);

                            // Make sure the fragment m/z value falls within the valid instrument range.
                            // CONSIDER: This means that a heavy transition might excede the instrument
                            //           range where a light one is accepted, leading to a disparity
                            //           between heavy and light transtions picked.
                            if (minMz > ionMz || ionMz > maxMz)
                                continue;

                            TransitionDocNode nodeTranReturn = null;
                            bool accept = true;
                            if (pick == TransitionLibraryPick.all || pick == TransitionLibraryPick.all_plus)
                            {
                                if (!useFilter)
                                {
                                    nodeTranReturn = CreateTransitionNode(type, i, charge, massH, losses, transitionRanks);
                                    accept = false;
                                }
                                else
                                {
                                    if (IsMatched(transitionRanks, ionMz, type, charge, losses))
                                    {
                                        nodeTranReturn = CreateTransitionNode(type, i, charge, massH, losses, transitionRanks);
                                        accept = false;
                                    }
                                    // If allowing library or filter, check the filter to decide whether to accept
                                    else if (pick == TransitionLibraryPick.all_plus &&
                                                tranSettings.Accept(sequence, precursorMzAccept, type, i, ionMz, start, end, startMz))
                                    {
                                        nodeTranReturn = CreateTransitionNode(type, i, charge, massH, losses, transitionRanks);
                                    }
                                }
                            }
                            else if (tranSettings.Accept(sequence, precursorMzAccept, type, i, ionMz, start, end, startMz))
                            {
                                if (pick == TransitionLibraryPick.none)
                                    nodeTranReturn = CreateTransitionNode(type, i, charge, massH, losses, transitionRanks);
                                else
                                {
                                    if (IsMatched(transitionRanks, ionMz, type, charge, losses))
                                        nodeTranReturn = CreateTransitionNode(type, i, charge, massH, losses, transitionRanks);
                                }
                            }
                            if (nodeTranReturn != null)
                            {
                                if (IsAvoidMismatchedIsotopeTransitions &&
                                    !OtherLabelTypesAllowed(settings, minMz, maxMz, start, end, startMz, accept,
                                        groupDocNode, nodeTranReturn, listOtherTypes))
                                {
                                    continue;
                                }
                                Assume.IsTrue(minMz <= nodeTranReturn.Mz && nodeTranReturn.Mz <= maxMz);
                                yield return nodeTranReturn;
                            }
                        }
                    }
                }
            }
        }

        private bool OtherLabelTypesAllowed(SrmSettings settings, double minMz, double maxMz, int start, int end, double startMz, bool accept,
                                            TransitionGroupDocNode nodeGroupMatching,
                                            TransitionDocNode nodeTran,
                                            IEnumerable<Tuple<TransitionGroupDocNode, IFragmentMassCalc>> listOtherTypes)
        {
            foreach (var otherType in listOtherTypes)
            {
                var nodeGroupOther = otherType.Item1;
                var tranGroupOther = nodeGroupOther.TransitionGroup;
                var nodeTranMatching = tranGroupOther.GetMatchingTransition(settings,
                    nodeGroupMatching, nodeTran, otherType.Item2);
                if (minMz > nodeTranMatching.Mz || nodeTranMatching.Mz > maxMz)
                    return false;
                if (accept && !settings.TransitionSettings.Accept(Peptide.Sequence,
                    nodeGroupOther.PrecursorMz,
                    nodeTranMatching.Transition.IonType,
                    nodeTranMatching.Transition.CleavageOffset,
                    nodeTranMatching.Mz,
                    start,
                    end,
                    startMz))
                {
                    return false;
                }
            }
            return true;
        }

        public IEnumerable<TransitionDocNode> GetPrecursorTransitions(SrmSettings settings,
                                                             ExplicitMods mods,
                                                             IPrecursorMassCalc calcFilterPre,
                                                             IFragmentMassCalc calcPredict,
                                                             double precursorMz,
                                                             IsotopeDistInfo isotopeDist,
                                                             IList<IList<ExplicitLoss>> potentialLosses,
                                                             IDictionary<double, LibraryRankedSpectrumInfo.RankedMI> transitionRanks,
                                                             bool libraryFilter,
                                                             bool useFilter)
        {
            var tranSettings = settings.TransitionSettings;
            var fullScan = tranSettings.FullScan;
            MassType massType = tranSettings.Prediction.FragmentMassType;
            int minMz = tranSettings.Instrument.GetMinMz(precursorMz);
            int maxMz = tranSettings.Instrument.MaxMz;
            bool precursorMS1 = fullScan.IsEnabledMs;

            if (IsCustomIon)
            {
                var ionMz =
                    BioMassCalc.CalculateIonMz(
                        CustomIon.GetMass(settings.TransitionSettings.Prediction.PrecursorMassType),
                        PrecursorCharge);
                if (!useFilter ||
                    !libraryFilter || 
                    IsMatched(transitionRanks, 
                        ionMz, IonType.precursor,
                        PrecursorCharge, null))
                {
                    if (precursorMS1 && isotopeDist != null)
                    {
                        foreach (int i in fullScan.SelectMassIndices(isotopeDist, useFilter))
                        {
                            double precursorMS1Mass = isotopeDist.GetMassI(i);
                            ionMz = BioMassCalc.CalculateIonMz(precursorMS1Mass, PrecursorCharge);
                            if (minMz > ionMz || ionMz > maxMz)
                                continue;
                            var isotopeDistInfo = new TransitionIsotopeDistInfo(isotopeDist.GetRankI(i), isotopeDist.GetProportionI(i));
                            yield return CreateTransitionNode(i, precursorMS1Mass, isotopeDistInfo, null, transitionRanks, CustomIon);
                        }
                    }
                    else
                    {
                        var transition = new Transition(this, PrecursorCharge, null, CustomIon, IonType.precursor);
                        double massH = CustomIon.GetMass(settings.TransitionSettings.Prediction.PrecursorMassType);
                        yield return new TransitionDocNode(transition, null, massH, null, null);
                    }
                }
                yield break;
            }

            string sequence = Peptide.Sequence;
            bool precursorNoProducts = precursorMS1 && !fullScan.IsEnabledMsMs &&
                tranSettings.Filter.IonTypes.Count == 1 && tranSettings.Filter.IonTypes[0] == IonType.precursor;
            double precursorMassPredict = calcPredict.GetPrecursorFragmentMass(sequence);

            foreach (var losses in CalcTransitionLosses(IonType.precursor, 0, massType, potentialLosses))
            {
                double ionMz = SequenceMassCalc.GetMZ(Transition.CalcMass(precursorMassPredict, losses), PrecursorCharge);
                if (losses == null)
                {
                    if (precursorMS1 && isotopeDist != null)
                    {
                        foreach (int i in fullScan.SelectMassIndices(isotopeDist, useFilter))
                        {
                            double precursorMS1Mass = isotopeDist.GetMassI(i, DecoyMassShift);
                            ionMz = SequenceMassCalc.GetMZ(precursorMS1Mass, PrecursorCharge);
                            if (minMz > ionMz || ionMz > maxMz)
                                continue;
                            var isotopeDistInfo = new TransitionIsotopeDistInfo(
                                isotopeDist.GetRankI(i), isotopeDist.GetProportionI(i));
                            yield return CreateTransitionNode(i, precursorMS1Mass, isotopeDistInfo, null, transitionRanks);
                        }
                        continue;
                    }
                }
                // If there was loss, it is possible (though not likely) that the ion m/z value
                // will now fall below the minimum measurable value for the instrument
                else if (minMz > ionMz)
                {
                    continue;
                }

                // If filtering precursors from MS1 scans, then ranking in MS/MS does not apply
                bool precursorIsProduct = !precursorMS1 || losses != null;
                // Skip product ion precursors, if the should not be included
                if (useFilter && precursorIsProduct && precursorNoProducts)
                    continue;
                if (!useFilter || !precursorIsProduct ||
                        !libraryFilter || IsMatched(transitionRanks, ionMz, IonType.precursor,
                                                    PrecursorCharge, losses))
                {
                    yield return CreateTransitionNode(0, precursorMassPredict, null, losses,
                                                      precursorIsProduct ? transitionRanks : null);
                }
            }            
        }

        public bool IsMatched(IDictionary<double, LibraryRankedSpectrumInfo.RankedMI> transitionRanks,
                              double ionMz, IonType type, int charge, TransitionLosses losses)
        {
            LibraryRankedSpectrumInfo.RankedMI rmi;
            return (transitionRanks != null &&
                    transitionRanks.TryGetValue(ionMz, out rmi) &&
                    rmi.IonType == type &&
                    rmi.Charge == charge &&
                    Equals(rmi.Losses, losses));
        }

        public static IList<IList<ExplicitLoss>> CalcPotentialLosses(string sequence,
            PeptideModifications pepMods, ExplicitMods mods, MassType massType)
        {
            if (sequence == null)
            {
                return new IList<ExplicitLoss>[0];
            }
            // First build a list of the amino acids in this peptide which can be experience loss,
            // and the losses which apply to them.
            IList<KeyValuePair<IList<TransitionLoss>, int>> listIndexedListLosses = null;

            // Add losses for any explicit static modifications
            bool explicitStatic = (mods != null && mods.StaticModifications != null);
            bool explicitLosses = (explicitStatic && mods.HasNeutralLosses);

            // Add the losses for the implicit modifications, if there
            // are no explicit static modifications, or if explicit static
            // modifications exist, but they are for variable modifications.
            bool implicitAllowed = (!explicitStatic || mods.IsVariableStaticMods);
            bool implicitLosses = (implicitAllowed && pepMods.HasNeutralLosses);

            if (explicitLosses || implicitLosses)
            {
                // Enumerate each amino acid in the sequence
                int len = sequence.Length;
                for (int i = 0; i < len; i++)
                {
                    char aa = sequence[i];
                    if (implicitLosses)
                    {
                        // Test implicit modifications to see if they apply
                        foreach (var mod in pepMods.NeutralLossModifications)
                        {
                            // If the modification does apply, store it in the list
                            if (mod.IsLoss(aa, i, len))
                                listIndexedListLosses = AddNeutralLosses(i, mod, massType, listIndexedListLosses);
                        }
                    }
                    if (explicitLosses)
                    {
                        foreach (var mod in mods.NeutralLossModifications)
                        {
                            if (mod.IndexAA == i)
                            {
                                listIndexedListLosses = AddNeutralLosses(mod.IndexAA, mod.Modification,
                                    massType, listIndexedListLosses);
                            }
                        }
                    }
                }
            }

            // If no losses were found, return null
            if (listIndexedListLosses == null)
                return null;

            var listListLosses = new List<IList<ExplicitLoss>>();
            int maxLossCount = Math.Min(pepMods.MaxNeutralLosses, listIndexedListLosses.Count);
            for (int lossCount = 1; lossCount <= maxLossCount; lossCount++)
            {
                var lossStateMachine = new NeutralLossStateMachine(lossCount, listIndexedListLosses);

                foreach (var listLosses in lossStateMachine.GetStates())
                    listListLosses.Add(listLosses);
            }
            return listListLosses;
        }

        private static IList<KeyValuePair<IList<TransitionLoss>, int>> AddNeutralLosses(int indexAA,
            StaticMod mod, MassType massType, IList<KeyValuePair<IList<TransitionLoss>, int>> listListMods)
        {
            if (listListMods == null)
                listListMods = new List<KeyValuePair<IList<TransitionLoss>, int>>();
            if (listListMods.Count == 0 || listListMods[listListMods.Count - 1].Value != indexAA)
                listListMods.Add(new KeyValuePair<IList<TransitionLoss>, int>(new List<TransitionLoss>(), indexAA));
            foreach (var loss in mod.Losses)
                listListMods[listListMods.Count - 1].Key.Add(new TransitionLoss(mod, loss, massType));
            return listListMods;
        }

        /// <summary>
        /// State machine that provides an IEnumerable{IList{ExplicitMod}} for
        /// enumerating all potential neutral loss states for a peptidw, given its sequence, 
        /// number of possible losses, and the set of possible losses.
        /// </summary>
        private sealed class NeutralLossStateMachine : ModificationStateMachine<TransitionLoss, ExplicitLoss, IList<ExplicitLoss>>
        {
            public NeutralLossStateMachine(int lossCount,
                IList<KeyValuePair<IList<TransitionLoss>, int>> listListLosses)
                : base(lossCount, listListLosses)
            {
            }

            protected override ExplicitLoss CreateMod(int indexAA, TransitionLoss mod)
            {
                return new ExplicitLoss(indexAA, mod);
            }

            protected override IList<ExplicitLoss> CreateState(ExplicitLoss[] mods)
            {
                return mods;
            }
        }

        private TransitionDocNode CreateTransitionNode(int massIndex, double precursorMassH, TransitionIsotopeDistInfo isotopeDistInfo,
            TransitionLosses losses, IDictionary<double, LibraryRankedSpectrumInfo.RankedMI> transitionRanks, CustomIon customIon = null)
        {
            Transition transition = new Transition(this, massIndex, customIon);
            var info = isotopeDistInfo == null ? TransitionDocNode.GetLibInfo(transition, Transition.CalcMass(precursorMassH, losses), transitionRanks) : null;
            return new TransitionDocNode(transition, losses, precursorMassH, isotopeDistInfo, info);
        }

        private TransitionDocNode CreateTransitionNode(IonType type, int cleavageOffset, int charge, double massH,
            TransitionLosses losses, IDictionary<double, LibraryRankedSpectrumInfo.RankedMI> transitionRanks, CustomIon customIon = null)
        {
            Transition transition = new Transition(this, type, cleavageOffset, 0, charge, null, customIon);
            var info = TransitionDocNode.GetLibInfo(transition, Transition.CalcMass(massH, losses), transitionRanks);
            return new TransitionDocNode(transition, losses, massH, null, info);
        }

        /// <summary>
        /// Calculate all possible transition losses that apply to a transition with
        /// a specific type and cleavage offset, given all of the potential loss permutations
        /// for the precursor.
        /// </summary>
        public static IEnumerable<TransitionLosses> CalcTransitionLosses(IonType type, int cleavageOffset,
            MassType massType, IEnumerable<IList<ExplicitLoss>> potentialLosses)
        {
            // First return no losses
            yield return null;

            if (type.Equals(IonType.custom))
            {
                foreach (var potentialLoss in potentialLosses)
                {
                    yield return GetCustomTransitionLosses(potentialLoss, massType);
                }    
            }
            else if (potentialLosses != null)
            {
                // Try to avoid allocating a whole list for this, as in many cases
                // there should be only one loss
                TransitionLosses firstLosses = null;
                List<TransitionLosses> allLosses = null;
                foreach (var losses in potentialLosses)
                {
                    var tranLosses = CalcTransitionLosses(type, cleavageOffset, massType, losses);
                    if (tranLosses == null ||
                            (firstLosses != null && firstLosses.Mass == tranLosses.Mass) ||
                            (allLosses != null && allLosses.Contains(l => l.Mass == tranLosses.Mass)))
                        continue;

                    if (allLosses == null)
                    {
                        if (firstLosses == null)
                            firstLosses = tranLosses;
                        else
                        {
                            allLosses = new List<TransitionLosses> { firstLosses };
                            firstLosses = null;
                        }
                    }
                    if (allLosses != null)
                        allLosses.Add(tranLosses);
                }

                // Handle the single losses case first
                if (firstLosses != null)
                    yield return firstLosses;
                else if (allLosses != null)
                {
                    // If more then one set of transition losses return them sorted by mass
                    allLosses.Sort((l1, l2) => Comparer<double>.Default.Compare(l1.Mass, l2.Mass));
                    foreach (var tranLosses in allLosses)
                        yield return tranLosses;
                }
            }
        }

        /// <summary>
        /// Calculate the transition losses that apply to a transition with
        /// a specific type and cleavage offset for a single set of explicit losses.
        /// </summary>
        private static TransitionLosses CalcTransitionLosses(IonType type, int cleavageOffset,
            MassType massType, IEnumerable<ExplicitLoss> losses)
        {
            List<TransitionLoss> listLosses = null;
            foreach (var loss in losses)
            {
                if (!Transition.IsPrecursor(type))
                {
                    if (Transition.IsNTerminal(type) && loss.IndexAA > cleavageOffset)
                        continue;
                    if (Transition.IsCTerminal(type) && loss.IndexAA <= cleavageOffset)
                        continue;
                }
                if (listLosses == null)
                    listLosses = new List<TransitionLoss>();
                listLosses.Add(loss.TransitionLoss);
            }
            if (listLosses == null)
                return null;
            return  new TransitionLosses(listLosses, massType);
        }

        private static TransitionLosses GetCustomTransitionLosses(IEnumerable<ExplicitLoss> losses,MassType massType)
        {
            List<TransitionLoss> listLosses = new List<TransitionLoss>();
            foreach (var loss in losses)
            {
                listLosses.Add(loss.TransitionLoss);            
            }
            return new TransitionLosses(listLosses,massType);
        }

        public void GetLibraryInfo(SrmSettings settings, ExplicitMods mods, bool useFilter,
            ref SpectrumHeaderInfo libInfo,
            Dictionary<double, LibraryRankedSpectrumInfo.RankedMI> transitionRanks)
        {
            PeptideLibraries libraries = settings.PeptideSettings.Libraries;
            // No libraries means no library info
            if (!libraries.HasLibraries)
            {
                libInfo = null;
                return;
            }
            // If not loaded, leave everything alone, and let the update
            // when loading is complete fix things.
            if (!libraries.IsLoaded)
                return;

            IsotopeLabelType labelType;
            if (!settings.TryGetLibInfo(Peptide.Sequence, PrecursorCharge, mods, out labelType, out libInfo))
                libInfo = null;                
            else if (transitionRanks != null)
            {
                try
                {
                    SpectrumPeaksInfo spectrumInfo;
                    string sequenceMod = settings.GetModifiedSequence(Peptide.Sequence, labelType, mods);
                    if (libraries.TryLoadSpectrum(new LibKey(sequenceMod, PrecursorCharge), out spectrumInfo))
                    {
                        var spectrumInfoR = new LibraryRankedSpectrumInfo(spectrumInfo, labelType,
                            this, settings, mods, useFilter, 50);
                        foreach (var rmi in spectrumInfoR.PeaksRanked)
                        {
                            Debug.Assert(!transitionRanks.ContainsKey(rmi.PredictedMz));
                            transitionRanks.Add(rmi.PredictedMz, rmi);
                            if (!useFilter && rmi.PredictedMz2 != 0 && !transitionRanks.ContainsKey(rmi.PredictedMz2))
                                transitionRanks.Add(rmi.PredictedMz2, rmi);
                        }
                    }
                }
                // Catch and ignore file access exceptions
                catch (IOException) {}
                catch (UnauthorizedAccessException) {}
                catch (ObjectDisposedException) {}
            }
        }

        public static bool IsGluAsp(string sequence, int cleavageOffset)
        {
            char c = Transition.GetFragmentCTermAA(sequence, cleavageOffset);
            return (c == 'G' || c == 'A');
        }

        public static bool IsPro(string sequence, int cleavageOffset)
        {
            return (Transition.GetFragmentNTermAA(sequence, cleavageOffset) == 'P');
        }

        public void Validate(int? charge)
        {
            if (charge.HasValue && charge != 0)
            {
                if (IsCustomIon)
                {
                    if (MIN_PRECURSOR_CHARGE > Math.Abs(charge.Value) || Math.Abs(charge.Value) > MAX_PRECURSOR_CHARGE)
                        throw new InvalidDataException(
                            string.Format(Resources.Transition_Validate_Precursor_charge__0__must_be_non_zero_and_between__1__and__2__,
                                charge, -MAX_PRECURSOR_CHARGE, MAX_PRECURSOR_CHARGE));
                }
                else if (MIN_PRECURSOR_CHARGE > charge || charge > MAX_PRECURSOR_CHARGE)
                {
                    throw new InvalidDataException(
                        string.Format(Resources.TransitionGroup_Validate_Precursor_charge__0__must_be_between__1__and__2__,
                            charge, MIN_PRECURSOR_CHARGE, MAX_PRECURSOR_CHARGE));
                }
            }
            if (DecoyMassShift.HasValue)
            {
                if ((DecoyMassShift != 0) &&
                    (DecoyMassShift != ALTERED_SEQUENCE_DECOY_MZ_SHIFT) &&
                    (DecoyMassShift < MIN_PRECURSOR_DECOY_MASS_SHIFT || DecoyMassShift > MAX_PRECURSOR_DECOY_MASS_SHIFT))
                {
                    throw new InvalidDataException(
                        string.Format(Resources.TransitionGroup_Validate_Precursor_decoy_mass_shift__0__must_be_between__1__and__2__,
                                      DecoyMassShift, MIN_PRECURSOR_DECOY_MASS_SHIFT, MAX_PRECURSOR_DECOY_MASS_SHIFT));
                }
            }
        }

        #region object overrides

        public bool Equals(TransitionGroup obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            bool equal =  Equals(obj._peptide, _peptide) &&
                Equals(obj.CustomIon, CustomIon) &&
                obj.PrecursorCharge == PrecursorCharge &&
                obj.LabelType.Equals(LabelType) &&
                obj.DecoyMassShift.Equals(DecoyMassShift);
            return equal; // For debugging convenience
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (TransitionGroup)) return false;
            return Equals((TransitionGroup) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = _peptide.GetHashCode();
                result = (result*397) ^ PrecursorCharge;
                result = (result*397) ^ LabelType.GetHashCode();
                result = (result*397) ^ (DecoyMassShift.HasValue ? DecoyMassShift.Value : 0);
                return result;
            }
        }

        public override string ToString()
        {
            return LabelType.IsLight
                       ? string.Format("Charge {0} {1}", PrecursorCharge,   // Not L10N : For debugging
                                       Transition.GetDecoyText(DecoyMassShift)) 
                       : string.Format("Charge {0} ({1}) {2}", PrecursorCharge, LabelType, // Not L10N: For debugging
                                       Transition.GetDecoyText(DecoyMassShift));
        }

        #endregion
    }
}
