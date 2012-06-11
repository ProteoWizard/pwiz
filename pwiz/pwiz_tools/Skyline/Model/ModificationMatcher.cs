/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using System.Text;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class ModificationMatcher : AbstractModificationMatcher
    {
        private IEnumerator<string> _sequences;

        private const int DEFAULT_ROUNDING_DIGITS = 6;

        public void CreateMatches(SrmSettings settings, IEnumerable<string> sequences,
            MappedList<string, StaticMod> defSetStatic, MappedList<string, StaticMod> defSetHeavy)
        {
            _sequences = sequences.GetEnumerator();
            InitMatcherSettings(settings, defSetStatic, defSetHeavy);
            if (UnmatchedSequences.Count > 0)
            {
                UnmatchedSequences.Sort();
                throw new FormatException(UninterpretedMods);
            }
        }

        public override bool MoveNextSequence()
        {
            if(!_sequences.MoveNext())
                return false;
            // Skip sequences that can be created from the current settings.
            TransitionGroupDocNode nodeGroup;
            while (CreateDocNodeFromSettings(_sequences.Current, null, null, out nodeGroup) != null)
            {
                if (!_sequences.MoveNext())
                    return false;
            }
            return true;
        }

        public override IEnumerable<AAModInfo> GetCurrentSequenceInfos()
        {
            int prevIndexAA = -1;
            bool prevHeavy = false;
            int countModsPerAA = 0;
            List<int> badSeqIndices = new List<int>();
            foreach (var info in EnumerateSequenceInfos(_sequences.Current ?? "", false))
            {
                int indexAA = info.IndexAA;
                var indexAAinSeq = info.IndexAAInSeq;
                if (badSeqIndices.Contains(indexAAinSeq))
                    continue;
                countModsPerAA = prevIndexAA != indexAA ? 1 : countModsPerAA + 1;
                bool tooManyMods = countModsPerAA > 1 && prevHeavy == info.UserIndicatedHeavy;
                prevIndexAA = indexAA;
                prevHeavy = info.UserIndicatedHeavy;
                if(!tooManyMods)
                    yield return info;
                else
                {
                    var unmatchedSeq = GetSeqModUnmatchedStr(info.IndexAAInSeq);
                    if(!UnmatchedSequences.Contains(unmatchedSeq))
                        UnmatchedSequences.Add(unmatchedSeq);
                }
            }
        }

        private static IEnumerable<AAModInfo> EnumerateSequenceInfos(string seq, bool includeUnmod)
        {
            string aas = FastaSequence.StripModifications(seq);
            bool isSpecificHeavy = aas.Length > seq.Count(c => c == '{') && aas.Length > seq.Count(c => c == '[');
            int indexAA = 0;
            int indexAAInSeq = 0;
            int i = 0;
            while (i < seq.Length)
            {
                var aa = aas[indexAA];
                int indexBracket = i + 1;
                if (indexBracket < seq.Length && (seq[indexBracket] == '[' || seq[indexBracket] == '{'))
                {
                    bool isHeavy = seq[indexBracket] == '{';
                    char closeBracket = isHeavy ? '}' : ']';
                    int indexStart = indexBracket + 1;
                    int indexClose = seq.IndexOf(closeBracket, indexBracket);
                    string mod = seq.Substring(indexStart, indexClose - indexStart);
                    i = indexClose;
                    ModTerminus? modTerminus = null;
                    if (indexAA == 0)
                        modTerminus = ModTerminus.N;
                    if (indexAA == aas.Length - 1)
                        modTerminus = ModTerminus.C;
                    int decPlace = mod.IndexOf(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator,
                        StringComparison.Ordinal);
                    string name = null;
                    var roundedTo = Math.Min(decPlace == -1 ? 0 : mod.Length - decPlace - 1,
                        DEFAULT_ROUNDING_DIGITS);
                    double? mass = null;
                    double result;
                    if (double.TryParse(mod, out result))
                        mass = Math.Round(result, roundedTo);
                    else
                        name = mod;
                    var key = new AAModKey
                    {
                        Name = name,
                        Mass = mass,
                        AA = aa,
                        Terminus = modTerminus,
                        UserIndicatedHeavy = isHeavy,
                        RoundedTo = roundedTo,
                        AppearsToBeSpecificMod = isSpecificHeavy
                    };

                    yield return new AAModInfo
                    {
                        ModKey = key,
                        IndexAA = indexAA,
                        IndexAAInSeq = indexAAInSeq,
                    };
                }
                else if (includeUnmod)
                {
                    // If need unmodified amino acids (as when 
                    // checking for equality), yield SequenceKeys for these AA's.
                    var key = new AAModKey
                    {
                        AA = aa,
                        Mass = 0
                    };
                    yield return new AAModInfo
                    {
                        ModKey = key,
                        IndexAA = indexAA,
                    };
                }
                // If the next character is a bracket, continue using the same amino
                // acid and leave i where it is.
                int iNext = i + 1;
                if (iNext >= seq.Length || (seq[iNext] != '[' && seq[iNext] != '{'))
                {
                    i = indexAAInSeq = iNext;
                    indexAA++;
                }
            }
        }

        public string GetSeqModUnmatchedStr(int startIndex)
        {
            var sequence = _sequences.Current ?? "";
            var result = new StringBuilder(sequence[startIndex].ToString(CultureInfo.InvariantCulture));
            bool parenExpected = true;
            for (int i = startIndex + 1; i < sequence.Length; i++)
            {
                char c = sequence[i];
                if (parenExpected && !(c == '[' || c == '{'))
                    return result.ToString();
                parenExpected = c == ']' || c == '}';
                result.Append(c);
            }
            return result.ToString();
        }

        public override void UpdateMatcher(AAModInfo info, AAModMatch? match)
        {
            if(match == null)
            {
                var unmatchedSeq = GetSeqModUnmatchedStr(info.IndexAAInSeq);
                if (!UnmatchedSequences.Contains(unmatchedSeq))
                    UnmatchedSequences.Add(unmatchedSeq);
            }
        }

        public PeptideDocNode GetModifiedNode(string seq)
        {
            return GetModifiedNode(seq, null);
        }

        public PeptideDocNode GetModifiedNode(string seq, FastaSequence fastaSequence)
        {
            var seqUnmod = FastaSequence.StripModifications(seq);
            var peptide = fastaSequence != null
              ? fastaSequence.CreateFullPeptideDocNode(Settings, seqUnmod).Peptide
              : new Peptide(null, seqUnmod, null, null,
                            Settings.PeptideSettings.Enzyme.CountCleavagePoints(seqUnmod));
            // First, try to create the peptide using the current settings.
            TransitionGroupDocNode nodeGroup;
            PeptideDocNode nodePep = 
                CreateDocNodeFromSettings(seq, peptide, SrmSettingsDiff.ALL, out nodeGroup);
            if (nodePep != null)
                return nodePep;
            // Create the peptideDocNode.
            nodePep = fastaSequence == null
              ? new PeptideDocNode(peptide, new TransitionGroupDocNode[0])
              : fastaSequence.CreateFullPeptideDocNode(Settings, seqUnmod);
            return CreateDocNodeFromMatches(nodePep, EnumerateSequenceInfos(seq, false));
        }

        protected override bool IsMatch(string seq, PeptideDocNode nodePep, out TransitionGroupDocNode nodeGroup)
        {
            var seqLight = FastaSequence.StripModifications(seq, FastaSequence.RGX_HEAVY);
            var seqHeavy = FastaSequence.StripModifications(seq, FastaSequence.RGX_LIGHT);
            var calcLight = Settings.GetPrecursorCalc(IsotopeLabelType.light, nodePep.ExplicitMods);
            foreach (TransitionGroupDocNode nodeGroupChild in nodePep.Children)
            {
                nodeGroup = nodeGroupChild;
                if (nodeGroup.TransitionGroup.LabelType.IsLight)
                {
                    // Light modifications must match.
                    if (!EqualsModifications(seqLight, calcLight, null))
                        return false;
                    // If the sequence only has light modifications, a match has been found.
                    if (Equals(seqLight, seq))
                        return true;
                }
                else
                {
                    var calc = Settings.GetPrecursorCalc(nodeGroup.TransitionGroup.LabelType, nodePep.ExplicitMods);
                    if (calc != null && EqualsModifications(seqHeavy, calc, calcLight))
                        return true;
                }
            }
            nodeGroup = null;
            return false;
        }

        /// <summary>
        /// Compares the modifications indicated in the sequence string to the calculated masses.
        /// </summary>
        /// <param name="seq">The modified sequence.</param>
        /// <param name="calc">Calculator used to calculate the masses.</param>
        /// <param name="calcLight">
        /// Additional light calculator if necessary to isolate mass changes
        /// caused by heavy modifications alone.
        /// </param>
        /// <returns>
        /// True if the given calculators explain the modifications indicated on the sequence, 
        /// false otherwise.
        /// </returns>
        private bool EqualsModifications(string seq, IPrecursorMassCalc calc, IPrecursorMassCalc calcLight)
        {
            var modifications = Settings.PeptideSettings.Modifications;
            bool structural = calcLight == null;
            string aas = FastaSequence.StripModifications(seq);
            foreach (var info in EnumerateSequenceInfos(seq, true))
            {
                int indexAA = info.IndexAA; // ReSharper
                var aa = aas[indexAA];
                var roundedTo = info.RoundedTo;
                // If the user has indicated the modification by name, find that modification 
                // and calculate the mass.
                double massKey;
                if (info.Mass != null)
                    massKey = (double)info.Mass;
                else
                {
                    var info1 = info;
                    StaticMod modMatch = null;
                    int index;
                    if (structural &&
                        ((index = modifications.StaticModifications.IndexOf(mod => Equals(mod.Name, info1.Name)))
                        != -1))
                        modMatch = modifications.StaticModifications[index];
                    if (!structural &&
                        ((index = modifications.StaticModifications.IndexOf(mod => Equals(mod.Name, info1.Name)))
                        != -1))
                        modMatch = modifications.StaticModifications[index];

                    if (modMatch == null)
                        return false;
                    roundedTo = DEFAULT_ROUNDING_DIGITS;
                    massKey = Math.Round(GetDefaultModMass(aa, modMatch), roundedTo);
                }
                double massMod = Math.Round(calc.GetAAModMass(aas[indexAA], indexAA, aas.Length), roundedTo);
                // Subtract the mass difference of the light
                // modifications to isolate the masses of the heavy modifications.
                if (calcLight != null)
                    massMod -= Math.Round(calcLight.GetAAModMass(aas[indexAA], indexAA, aas.Length), roundedTo);
                if (!Equals(massKey, massMod))
                    return false;
            }
            return true;
        }

    }
}