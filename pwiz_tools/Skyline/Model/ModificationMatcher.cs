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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class ModificationMatcher : AbstractModificationMatcher
    {
        public IFormatProvider FormatProvider { get; set; }

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

        private static readonly SrmSettingsDiff DIFF_GROUPS = new SrmSettingsDiff(false, false, true, false, false, false);

        public override bool MoveNextSequence()
        {
            if(!_sequences.MoveNext())
                return false;
            // Skip sequences that can be created from the current settings.
            TransitionGroupDocNode nodeGroup;
            while (CreateDocNodeFromSettings(_sequences.Current, null, DIFF_GROUPS, out nodeGroup) != null)
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
            foreach (var info in EnumerateSequenceInfos(_sequences.Current ?? string.Empty, false))
            {
                int indexAA = info.IndexAA;
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

        private IEnumerable<AAModInfo> EnumerateSequenceInfos(string seq, bool includeUnmod)
        {
            string aas = FastaSequence.StripModifications(seq);
            bool isSpecificHeavy = FastaSequence.OPEN_MOD.All(paren => aas.Length > seq.Count(c => c == paren));
            int indexAA = 0;
            int indexAAInSeq = 0;
            int i = 0;
            while (i < seq.Length)
            {
                var aa = aas[indexAA];
                int indexBracket = i + 1;
                if (indexBracket < seq.Length && (FastaSequence.OPEN_MOD.Contains(seq[indexBracket]))) // Not L10N
                {
                    char openBracket = seq[indexBracket];
                    bool isHeavy = openBracket == '{'; // Not L10N
                    char closeBracket = FastaSequence.CLOSE_MOD[FastaSequence.OPEN_MOD.IndexOf(c => c == openBracket)];
                    int indexStart = indexBracket + 1;
                    int indexClose = seq.IndexOf(closeBracket, indexBracket);
                    string mod = seq.Substring(indexStart, indexClose - indexStart);
                    i = indexClose;
                    ModTerminus? modTerminus = null;
                    if (indexAA == 0)
                        modTerminus = ModTerminus.N;
                    if (indexAA == aas.Length - 1)
                        modTerminus = ModTerminus.C;
                    string name = null;
                    double? mass = null;
                    int roundedTo = 0;
                    double result;
                    // If passed in modification in UniMod notation, look up the id and find the name and mass
                    int uniModId;
                    if (TryGetIdFromUnimod(mod, out uniModId))
                    {
                        var staticMod = GetStaticMod(uniModId, aa, modTerminus);
                        if (staticMod == null)
                        {
                            ThrowUnimodException(seq, uniModId, indexAA, indexBracket, indexClose);
                        }
                        name = staticMod.Name;
                        isHeavy = !UniMod.IsStructuralModification(name);
                        // CONSIDER: Mass depends on TransitionPrediction settings for precursors
                        mass = staticMod.MonoisotopicMass;
                        roundedTo = DEFAULT_ROUNDING_DIGITS;
                    }
                    else if (double.TryParse(mod, NumberStyles.Float, FormatProvider ?? NumberFormatInfo.CurrentInfo, out result))
                    {
                        mass = result;
                        int decPlace = mod.IndexOf(LocalizationHelper.CurrentCulture.NumberFormat.NumberDecimalSeparator,
                            StringComparison.Ordinal);
                        if (decPlace != -1)
                            roundedTo = Math.Min(mod.Length - decPlace - 1, DEFAULT_ROUNDING_DIGITS);
                    }
                    else
                    {
                        name = mod;
                    }
                    
                    if (mass.HasValue)
                        mass = Math.Round(mass.Value, roundedTo);
 
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
                if (iNext >= seq.Length || !FastaSequence.OPEN_MOD.Contains(seq[iNext])) // Not L10N
                {
                    i = indexAAInSeq = iNext;
                    indexAA++;
                }
            }
        }

        public static StaticMod GetStaticMod(int uniModId, char aa, ModTerminus? modTerminus)
        {
            // Always check the simple AA mod case
            var idKeysToTry = new List<UniMod.UniModIdKey>
            {
                new UniMod.UniModIdKey
                {
                    Id = uniModId,
                    Aa = aa,
                    AllAas = false,
                    Terminus = null
                },
                new UniMod.UniModIdKey
                {
                    Id = uniModId,
                    Aa = aa,
                    AllAas = true,
                    Terminus = null
                }
            };
            // If mod is on a terminal AA, it could still be a non-terminal mod
            // Or a terminal mod that applies to any amino acid
            if (modTerminus != null)
            {
                idKeysToTry.Add(new UniMod.UniModIdKey
                {
                    Id = uniModId,
                    Aa = aa,
                    AllAas = false,
                    Terminus = modTerminus
                });
                idKeysToTry.Add(new UniMod.UniModIdKey
                {
                    Id = uniModId,
                    Aa = aa,
                    AllAas = true,
                    Terminus = modTerminus
                });
            }
            foreach (var key in idKeysToTry)
            {
                StaticMod staticMod;
                if (UniMod.DictUniModIds.TryGetValue(key, out staticMod))
                    return staticMod;
            }
            return null;
        }

        /// <summary>
        /// Tries to get a unimod ID from either a unimod:XXX string or a short name string
        /// </summary>
        /// <param name="unimodString">Mod notation to be converted to unimod</param>
        /// <param name="uniModId">uniMod ID number</param>
        /// <returns></returns>
        public static bool TryGetIdFromUnimod(string unimodString, out int uniModId)
        {
            const string prefixString = "unimod:"; // Not L10N
            if (unimodString.ToLowerInvariant().StartsWith(prefixString))
            {
                int prefixLength = prefixString.Length;
                return int.TryParse(unimodString.Substring(prefixLength, unimodString.Length - prefixLength), out uniModId);
            }
            // Try short name string
            return UniMod.DictShortNamesToUniMod.TryGetValue(unimodString.ToLower(), out uniModId);
        }

        public string SimplifyUnimodSequence(string seq)
        {
            var sb = new StringBuilder(seq);
            string aas = FastaSequence.StripModifications(seq);
            int indexAA = 0;
            int i = 0;
            while (i < seq.Length)
            {
                var aa = aas[indexAA];
                int indexBracket = i + 1;
                if (indexBracket < seq.Length && (FastaSequence.OPEN_MOD.Contains(seq[indexBracket]))) // Not L10N
                {
                    char openBracket = seq[indexBracket];
                    char closeBracket = FastaSequence.CLOSE_MOD[FastaSequence.OPEN_MOD.IndexOf(c => c == openBracket)];
                    int indexStart = indexBracket + 1;
                    int indexClose = seq.IndexOf(closeBracket, indexBracket);
                    string mod = seq.Substring(indexStart, indexClose - indexStart);
                    i = indexClose;
                    ModTerminus? modTerminus = null;
                    if (indexAA == 0)
                        modTerminus = ModTerminus.N;
                    if (indexAA == aas.Length - 1)
                        modTerminus = ModTerminus.C;
                    // Here we are only interested in uniMod
                    int uniModId;
                    if (TryGetIdFromUnimod(mod, out uniModId))
                    {
                        var staticMod = GetStaticMod(uniModId, aa, modTerminus);
                        if (staticMod == null)
                        {
                            ThrowUnimodException(seq, uniModId, indexAA, indexBracket, indexClose);
                            return null;    // Keep ReSharper happy
                        }
                        string name = staticMod.Name;
                        bool isHeavy = !UniMod.DictStructuralModNames.ContainsKey(name);
                        sb[indexBracket] = isHeavy ? '{' : '['; // Not L10N
                        sb[indexClose] = isHeavy ? '}' : ']'; // Not L10N
                    }
                }
                // If the next character is a bracket, continue using the same amino
                // acid and leave i where it is.
                int iNext = i + 1;
                if (iNext >= seq.Length || !FastaSequence.OPEN_MOD.Contains(seq[iNext])) // Not L10N
                {
                    indexAA++;
                    i++;
                }
            }
            return sb.ToString();
        }

        private static void ThrowUnimodException(string seq, int uniModId, int indexAA, int indexBracket, int indexClose)
        {
            int indexFirst = Math.Max(0, indexBracket - 1);
            int indexLast = Math.Min(seq.Length, indexClose + 1);
            string unrecognizedAaMod = seq.Substring(indexFirst, indexLast - indexFirst);
            if (UniMod.IsValidUnimodId(uniModId))
            {
                throw new FormatException(
                    string.Format(Resources.ModificationMatcher_ThrowUnimodException_Unrecognized_modification_placement_for_Unimod_id__0__in_modified_peptide_sequence__1___amino_acid__2____3___,
                        uniModId, seq, indexAA + 1, unrecognizedAaMod));
            }

            throw new FormatException(
                string.Format(Resources.ModificationMatcher_ThrowUnimodException_Unrecognized_Unimod_id__0__in_modified_peptide_sequence__1___amino_acid__2____3___,
                    uniModId, seq, indexAA + 1, unrecognizedAaMod));
        }

        public string GetSeqModUnmatchedStr(int startIndex)
        {
            var sequence = _sequences.Current ?? string.Empty;
            var result = new StringBuilder(sequence[startIndex].ToString(CultureInfo.InvariantCulture));
            bool parenExpected = true;
            for (int i = startIndex + 1; i < sequence.Length; i++)
            {
                char c = sequence[i];
                if (parenExpected && !FastaSequence.OPEN_MOD.Contains(c))
                    return result.ToString();
                parenExpected = FastaSequence.CLOSE_MOD.Contains(c);
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
              ? new PeptideDocNode(peptide)
              : fastaSequence.CreateFullPeptideDocNode(Settings, seqUnmod);
            return CreateDocNodeFromMatches(nodePep, EnumerateSequenceInfos(seq, false));
        }

        protected override bool IsMatch(string seq, PeptideDocNode nodePep, out TransitionGroupDocNode nodeGroup)
        {
            string seqSimplified = SimplifyUnimodSequence(seq);
            var seqLight = FastaSequence.StripModifications(seqSimplified, FastaSequence.RGX_HEAVY);
            var seqHeavy = FastaSequence.StripModifications(seqSimplified, FastaSequence.RGX_LIGHT);
            var calcLight = Settings.TryGetPrecursorCalc(IsotopeLabelType.light, nodePep.ExplicitMods);
            foreach (TransitionGroupDocNode nodeGroupChild in nodePep.Children)
            {
                nodeGroup = nodeGroupChild;
                if (nodeGroup.TransitionGroup.LabelType.IsLight)
                {
                    // Light modifications must match.
                    if (!EqualsModifications(seqLight, calcLight, null))
                        return false;
                    // If the sequence only has light modifications, a match has been found.
                    if (Equals(seqLight, seqSimplified))
                        return true;
                }
                else
                {
                    var calc = Settings.TryGetPrecursorCalc(nodeGroup.TransitionGroup.LabelType, nodePep.ExplicitMods);
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
                        ((index = modifications.HeavyModifications.IndexOf(mod => Equals(mod.Name, info1.Name)))
                        != -1))
                        modMatch = modifications.HeavyModifications[index];

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