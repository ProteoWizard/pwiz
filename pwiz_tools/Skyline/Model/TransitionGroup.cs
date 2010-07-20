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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class TransitionGroup : Identity
    {
        public const int MIN_PRECURSOR_CHARGE = 1;
        public const int MAX_PRECURSOR_CHARGE = 6;

        private readonly Peptide _peptide;

        public TransitionGroup(Peptide peptide, int precursorCharge, IsotopeLabelType labelType)
            : this(peptide, precursorCharge, labelType, false)
        {            
        }

        public TransitionGroup(Peptide peptide, int precursorCharge, IsotopeLabelType labelType, bool unlimitedCharge)
        {
            _peptide = peptide;

            PrecursorCharge = precursorCharge;
            LabelType = labelType;

            Validate(unlimitedCharge);
        }

        public Peptide Peptide { get { return _peptide; } }

        public int PrecursorCharge { get; private set; }
        public IsotopeLabelType LabelType { get; private set; }

        public string LabelTypeText
        {
            get { return (!LabelType.IsLight ? " ("+ LabelType + ")" : ""); }
        }

        public static int CompareTransitions(TransitionDocNode node1, TransitionDocNode node2)
        {
            Transition tran1 = node1.Transition, tran2 = node2.Transition;
            // TODO: To generate the same ordering as GetTransitions, some attention
            //       would have to be paid to the ordering in the SrmSettings.TransitionSettings
            //       At least this groups the types, and orders by ion ordinal...
            int diffType = ((int) tran1.IonType) - ((int) tran2.IonType);
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

        public IEnumerable<TransitionDocNode> GetTransitions(SrmSettings settings, ExplicitMods mods, double precursorMz,
            SpectrumHeaderInfo libInfo, IDictionary<double, LibraryRankedSpectrumInfo.RankedMI> transitionRanks,
            bool useFilter)
        {
            // Get necessary mass calculators and masses
            var calcFilterPre = settings.GetPrecursorCalc(IsotopeLabelType.light, mods);
            var calcFilter = settings.GetFragmentCalc(IsotopeLabelType.light, mods);
            var calcPredict = settings.GetFragmentCalc(LabelType, mods);

            string sequence = Peptide.Sequence;

            MassType massType = settings.TransitionSettings.Prediction.FragmentMassType;
            var pepMods = settings.PeptideSettings.Modifications;
            var potentialLosses = CalcPotentialLosses(sequence, pepMods, mods,
                massType);
            // Return the precursor ion
            double precursorMassPredict = calcPredict.GetPrecursorFragmentMass(sequence);
            if (!useFilter)
            {
                foreach (var nodeTran in CreateTransitionNodes(precursorMassPredict,
                    transitionRanks, massType, potentialLosses))
                {
                    yield return nodeTran;                    
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
                precursorMz = SequenceMassCalc.GetMZ(calcFilterPre.GetPrecursorMass(sequence), PrecursorCharge);
                massesFilter = calcFilter.GetFragmentIonMasses(sequence);
            }

            var tranSettings = settings.TransitionSettings;
            var filter = tranSettings.Filter;

            // Get filter settings
            var charges = filter.ProductCharges;
            var types = filter.IonTypes;
            var startFinder = filter.FragmentRangeFirst;
            var endFinder = filter.FragmentRangeLast;
            bool pro = filter.IncludeNProline;
            bool gluasp = filter.IncludeCGluAsp;

            // Get library settings
            var pick = tranSettings.Libraries.Pick;
            if (!useFilter)
            {
                pick = TransitionLibraryPick.all;
                charges = Transition.ALL_CHARGES;
                types = Transition.ALL_TYPES;
            }
            // If there are no libraries or no library information, then
            // picking cannot use library information
            else if (!settings.PeptideSettings.Libraries.HasLibraries || libInfo == null)
                pick = TransitionLibraryPick.none;
            // If picking relies on library information
            else if (pick != TransitionLibraryPick.none)
            {
                // If it is not yet loaded, or nothing got ranked, return an empty enumeration
                if (!settings.PeptideSettings.Libraries.IsLoaded || transitionRanks.Count == 0)
                    yield break;
            }

            // Get instrument settings
            int minMz = tranSettings.Instrument.MinMz;
            int maxMz = tranSettings.Instrument.MaxMz;

            // Loop over potential product ions picking transitions
            foreach (IonType type in types)
            {
                foreach (int charge in charges)
                {
                    // Precursor charge can never be lower than product ion charge.
                    if (PrecursorCharge < charge)
                        continue;

                    int start = 0, end = 0;
                    if (pick != TransitionLibraryPick.all)
                    {
                        start = startFinder.FindStartFragment(massesFilter, type, charge, precursorMz);
                        end = endFinder.FindEndFragment(type, start, len);
                        if (Transition.IsCTerminal(type))
                            Helpers.Swap(ref start, ref end);
                    }

                    for (int i = 0; i < len; i++)
                    {
                        // Get the predicted m/z that would be used in the transition
                        double massH = massesPredict[(int) type, i];
                        double ionMz = SequenceMassCalc.GetMZ(massH, charge);

                        // Make sure the fragment m/z value falls within the valid instrument range.
                        // CONSIDER: This means that a heavy transition might excede the instrument
                        //           range where a light one is accepted, leading to a disparity
                        //           between heavy and light transtions picked.
                        if (minMz > ionMz || ionMz > maxMz)
                            continue;

                        if (pick == TransitionLibraryPick.all)
                        {
                            if (!useFilter)
                            {
                                foreach (var nodeTran in CreateTransitionNodes(type, i, charge, massH,
                                    transitionRanks, massType, potentialLosses))
                                {
                                    yield return nodeTran;
                                }
                            }
                            else
                            {
                                LibraryRankedSpectrumInfo.RankedMI rmi;
                                if (transitionRanks.TryGetValue(ionMz, out rmi) && rmi.IonType == type && rmi.Charge == charge)
                                    yield return CreateTransitionNode(type, i, charge, massH, null, transitionRanks);
                            }
                        }
                        else if ((start <= i && i <= end) ||
                            (pro && IsPro(sequence, i)) ||
                            (gluasp && IsGluAsp(sequence, i)))
                        {
                            if (pick == TransitionLibraryPick.none)
                                yield return CreateTransitionNode(type, i, charge, massH, null, transitionRanks);
                            else if (transitionRanks.ContainsKey(ionMz))
                                yield return CreateTransitionNode(type, i, charge, massH, null, transitionRanks);
                        }
                    }
                }
            }
        }

        private static IList<IList<ExplicitLoss>> CalcPotentialLosses(string sequence,
            PeptideModifications pepMods, ExplicitMods mods, MassType massType)
        {
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

                foreach (var listLosses in lossStateMachine.GetLosses())
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
        private sealed class NeutralLossStateMachine
        {
            private readonly int _lossCount;
            private readonly IList<KeyValuePair<IList<TransitionLoss>, int>> _listListLosses;

            /// <summary>
            /// Contains indexes into _listListLosses specifying amino acids currently
            /// modified.
            /// </summary>
            private readonly int[] _arrayLossIndexes1;

            /// <summary>
            /// Contains indexes into the static mod lists of _listListLosses specifying
            /// which modification is currently applied to the amino acid specified
            /// by _arrayLossIndexes1.
            /// </summary>
            private readonly int[] _arrayLossIndexes2;

            /// <summary>
            /// Index to the currently active elements in _arrayModIndexes arrays.
            /// </summary>
            private int _cursorIndex;

            public NeutralLossStateMachine(int lossCount,
                IList<KeyValuePair<IList<TransitionLoss>, int>> listListMods)
            {
                _lossCount = lossCount;
                _listListLosses = listListMods;

                // Fill the mod indexes list with the first possible state
                _arrayLossIndexes1 = new int[_lossCount];
                for (int i = 0; i < lossCount; i++)
                    _arrayLossIndexes1[i] = i;
                // Second set of indexes start all zero initialized
                _arrayLossIndexes2 = new int[_lossCount];
                // Set the cursor to the last modification
                _cursorIndex = lossCount - 1;
            }

            public IEnumerable<IList<ExplicitLoss>> GetLosses()
            {
                while (_cursorIndex >= 0)
                {
                    yield return CurrentLosses;

                    if (!ShiftCurrentLoss())
                    {
                        // Attempt to advance any loss to the left of the current loss
                        do
                        {
                            _cursorIndex--;
                        }
                        while (_cursorIndex >= 0 && !ShiftCurrentLoss());

                        // If a loss was successfully advanced, reset all losses to its right
                        // and start over with them.
                        if (_cursorIndex >= 0)
                        {
                            for (int i = 1; i < _lossCount - _cursorIndex; i++)
                            {
                                _arrayLossIndexes1[_cursorIndex + i] = _arrayLossIndexes1[_cursorIndex] + i;
                                _arrayLossIndexes2[_cursorIndex + i] = 0;
                            }
                            _cursorIndex = _lossCount - 1;
                        }
                    }
                }
            }

            private bool ShiftCurrentLoss()
            {
                int modIndex = _arrayLossIndexes1[_cursorIndex];
                if (_arrayLossIndexes2[_cursorIndex] < _listListLosses[modIndex].Key.Count - 1)
                {
                    // Shift the current amino acid through all possible loss states
                    _arrayLossIndexes2[_cursorIndex]++;
                }
                else if (modIndex < _listListLosses.Count - _lossCount + _cursorIndex)
                {
                    // Shift the current loss through all possible positions
                    _arrayLossIndexes1[_cursorIndex]++;
                    _arrayLossIndexes2[_cursorIndex] = 0;
                }
                else
                {
                    // Current loss has seen all possible states
                    return false;
                }
                return true;
            }

            private IList<ExplicitLoss> CurrentLosses
            {
                get
                {
                    var explicitLosses = new ExplicitLoss[_lossCount];
                    for (int i = 0; i < _lossCount; i++)
                    {
                        var pair = _listListLosses[_arrayLossIndexes1[i]];
                        var loss = pair.Key[_arrayLossIndexes2[i]];

                        explicitLosses[i] = new ExplicitLoss(pair.Value, loss);
                    }
                    return explicitLosses;
                }
            }
        }

        private IEnumerable<TransitionDocNode>
            CreateTransitionNodes(double precursorMassH,
                                  IDictionary<double, LibraryRankedSpectrumInfo.RankedMI> transitionRanks,
                                  MassType massType,
                                  IList<IList<ExplicitLoss>> potentialLosses)
        {
            foreach (var losses in CalcTransitionLosses(IonType.precursor, 0, massType, potentialLosses))
                yield return CreateTransitionNode(precursorMassH, losses, transitionRanks);
        }

        private TransitionDocNode CreateTransitionNode(double precursorMassH, TransitionLosses losses,
            IDictionary<double, LibraryRankedSpectrumInfo.RankedMI> transitionRanks)
        {
            Transition transition = new Transition(this);
            var info = TransitionDocNode.GetLibInfo(transition, precursorMassH, transitionRanks);
            return new TransitionDocNode(transition, losses, precursorMassH, info);
        }

        private IEnumerable<TransitionDocNode>
            CreateTransitionNodes(IonType type,
                                  int cleavageOffset,
                                  int charge,
                                  double massH,
                                  IDictionary<double, LibraryRankedSpectrumInfo.RankedMI> transitionRanks,
                                  MassType massType,
                                  IList<IList<ExplicitLoss>> potentialLosses)
        {
            foreach (var losses in CalcTransitionLosses(type, cleavageOffset, massType, potentialLosses))
                yield return CreateTransitionNode(type, cleavageOffset, charge, massH, losses, transitionRanks);
        }

        /// <summary>
        /// Calculate all possible transition losses that apply to a transition with
        /// a specific type and cleavage offset, given all of the potential loss permutations
        /// for the precursor.
        /// </summary>
        private static IEnumerable<TransitionLosses> CalcTransitionLosses(IonType type, int cleavageOffset,
            MassType massType, IEnumerable<IList<ExplicitLoss>> potentialLosses)
        {
            // First return no losses
            yield return null;

            if (potentialLosses != null)
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

        private TransitionDocNode CreateTransitionNode(IonType type, int cleavageOffset, int charge, double massH,
            TransitionLosses losses, IDictionary<double, LibraryRankedSpectrumInfo.RankedMI> transitionRanks)
        {
            Transition transition = new Transition(this, type, cleavageOffset, charge);
            var info = TransitionDocNode.GetLibInfo(transition, massH, transitionRanks);
            return new TransitionDocNode(transition, losses, massH, info);
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
            else if (!libraries.IsLoaded)
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
                        }
                    }
                }
                catch (IOException)
                {
                }
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

        private void Validate(bool unlimitedCharge)
        {
            if (unlimitedCharge)
                return;
            if (MIN_PRECURSOR_CHARGE > PrecursorCharge || PrecursorCharge > MAX_PRECURSOR_CHARGE)
            {
                throw new InvalidDataException(string.Format("Precursor charge {0} must be between {1} and {2}.",
                    PrecursorCharge, MIN_PRECURSOR_CHARGE, MAX_PRECURSOR_CHARGE));
            }
        }

        #region object overrides

        public bool Equals(TransitionGroup obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj._peptide, _peptide) &&
                obj.PrecursorCharge == PrecursorCharge &&
                obj.LabelType.Equals(LabelType);
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
                return result;
            }
        }

        public override string ToString()
        {
            if (LabelType == IsotopeLabelType.heavy)
                return string.Format("Charge {0} (heavy)", PrecursorCharge);
            else
                return string.Format("Charge {0}", PrecursorCharge);
        }

        #endregion
    }
}
