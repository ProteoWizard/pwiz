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
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
    public class ModificationMatcher : AbstractModificationMatcher
    {
        private IEnumerator<string> _sequences;
        private int _sequenceCurrent;
        private int _sequenceCount;
        private IProgressMonitor _progressMonitor;
        private IProgressStatus _status;
        private const int DEFAULT_ROUNDING_DIGITS = 6;

        public bool HasSeenMods { get; private set; }

        public void CreateMatches(SrmSettings settings, IEnumerable<string> sequences,
            MappedList<string, StaticMod> defSetStatic, MappedList<string, StaticMod> defSetHeavy, 
            IProgressMonitor progressMonitor = null, IProgressStatus status = null)
        {
            _progressMonitor = progressMonitor;
            if (progressMonitor != null)
            {
                _status = (status ?? new ProgressStatus()).ChangeMessage(ModelResources.ModificationMatcher_CreateMatches_Matching_modifications);
                var countable = sequences as ICollection<string>;
                if (countable == null)
                {
                    countable = sequences.ToArray();
                    sequences = countable;
                }
                _sequenceCount = countable.Count;
            }

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
            if(!MoveNextSingleSequence())
                return false;
            // Skip sequences that can be created from the current settings.
            // Check first if the sequence has any modifications, because creating doc nodes is expensive
            while (!HasModsChecked(_sequences.Current) ||
                   CreateDocNodeFromSettings(new Target(_sequences.Current), null, DIFF_GROUPS, out _) != null)
            {
                if (!MoveNextSingleSequence())
                    return false;
            }
            return true;
        }

        private bool HasModsChecked(string sequence)
        {
            bool hasMods = HasMods(sequence);
            HasSeenMods = HasSeenMods || hasMods;
            return hasMods;
        }

        private bool MoveNextSingleSequence()
        {
            if (!_sequences.MoveNext())
                return false;

            if (_progressMonitor != null)
            {
                _sequenceCurrent++;
                if (_progressMonitor.IsCanceled)
                    return false;
                if (_sequenceCount > 0)
                    _progressMonitor.UpdateProgress(_status = _status.UpdatePercentCompleteProgress(_progressMonitor, _sequenceCurrent, _sequenceCount));
            }

            return true;
        }

        public override IEnumerable<AAModInfo> GetCurrentSequenceInfos()
        {
            if (_sequences.Current != null)
            {
                CrosslinkLibraryKey crosslinkLibraryKey =
                    CrosslinkSequenceParser.TryParseCrosslinkLibraryKey(_sequences.Current, 0);
                if (crosslinkLibraryKey != null)
                {
                    return crosslinkLibraryKey.PeptideLibraryKeys.SelectMany(peptideLibraryKey =>
                        EnumerateAaModInfos(peptideLibraryKey.ModifiedSequence));
                }
            }

            return EnumerateAaModInfos(_sequences.Current ?? string.Empty);
        }

        private IEnumerable<AAModInfo> EnumerateAaModInfos(string sequence)
        {
            int prevIndexAA = -1;
            bool prevHeavy = false;
            int countModsPerAA = 0;
            foreach (var info in EnumerateSequenceInfos(sequence, false))
            {
                int indexAA = info.IndexAA;
                countModsPerAA = prevIndexAA != indexAA ? 1 : countModsPerAA + 1;
                bool tooManyMods = countModsPerAA > 1 && prevHeavy == info.UserIndicatedHeavy;
                prevIndexAA = indexAA;
                prevHeavy = info.UserIndicatedHeavy;
                if (!tooManyMods)
                    yield return info;
                else
                {
                    var unmatchedSeq = GetSeqModUnmatchedStr(info.IndexAAInSeq);
                    if (!UnmatchedSequences.Contains(unmatchedSeq))
                        UnmatchedSequences.Add(unmatchedSeq);
                }
            }
        }

        private IEnumerable<AAModInfo> EnumerateSequenceInfos(string seq, bool includeUnmod)
        {
            string aas = FastaSequence.StripModifications(seq);
            bool isSpecificHeavy = FastaSequence.OPEN_MOD.All(paren => aas.Length > seq.Count(c => c == paren));
            var lossOnlyMods = Settings.PeptideSettings.Modifications.StaticModifications
                .Where(m =>m.HasLoss && !m.HasMod).ToArray();
            foreach (var modInfo in EnumerateMods(seq, false))
            {
                double? mass = modInfo.Mass;
                if (mass.HasValue)
                    mass = Math.Round(mass.Value, modInfo.RoundedTo);

                var key = new AAModKey
                {
                    Name = modInfo.Name,
                    UniModId = modInfo.Mod?.UnimodId,
                    Mass = mass,
                    AA = modInfo.AA,
                    Terminus = modInfo.ModTerminus,
                    UserIndicatedHeavy = modInfo.IsHeavy,
                    RoundedTo = modInfo.RoundedTo,
                    AppearsToBeSpecificMod = isSpecificHeavy
                };

                yield return new AAModInfo
                {
                    ModKey = key,
                    IndexAA = modInfo.IndexInUnmodifiedSequence,
                    IndexAAInSeq = modInfo.IndexInModifiedSequence,
                };
            }

            int indexAA = 0;
            int indexAAInSeq = 0;
            int i = 0;
            while (i < seq.Length)
            {
                var aa = aas[indexAA];
                int indexBracket = i + 1;
                if (indexBracket < seq.Length && (FastaSequence.OPEN_MOD.Contains(seq[indexBracket])))
                {
                    char openBracket = seq[indexBracket];
                    char closeBracket = FastaSequence.CLOSE_MOD[FastaSequence.OPEN_MOD.IndexOf(c => c == openBracket)];
                    int indexClose = seq.IndexOf(closeBracket, indexBracket);
                    i = indexClose;
                }
                else
                {
                    // Check for applicable loss-only modifications, since they don't appear in the modified sequence
                    var lossMod = lossOnlyMods.FirstOrDefault(m => m.IsLoss(aa, indexAAInSeq, aas.Length));
                    if (lossMod != null)
                    {
                        var info = new AAModInfo
                        {
                            ModKey = new AAModKey { Name = lossMod.Name, AA = aa, Terminus = lossMod.Terminus },
                            IndexAA = indexAA,
                            IndexAAInSeq = indexAAInSeq,
                        };
                        // Make sure this key finds a modification in the Matches dictionary
                        if (!Matches.ContainsKey(info.ModKey))
                            Matches.Add(info.ModKey, new AAModMatch {StructuralMod = lossMod});
                        yield return info;
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
                }
                // If the next character is a bracket, continue using the same amino
                // acid and leave i where it is.
                int iNext = i + 1;
                if (iNext >= seq.Length || !FastaSequence.OPEN_MOD.Contains(seq[iNext]))
                {
                    i = indexAAInSeq = iNext;
                    indexAA++;
                }
            }
        }

        public class ModInfo
        {
            public ModInfo(StaticMod mod, int indexInUnmodifiedSequence, int indexInModifiedSequence,
                char aa, ModTerminus? modTerminus, int roundedTo, bool isHeavy)
            {
                Mod = mod;
                Name = mod?.Name;
                Mass = mod?.MonoisotopicMass;
                IndexInUnmodifiedSequence = indexInUnmodifiedSequence;
                IndexInModifiedSequence = indexInModifiedSequence;
                AA = aa;
                ModTerminus = modTerminus;
                RoundedTo = roundedTo;
                IsHeavy = isHeavy;
            }

            public ModInfo(string name, double? mass, int indexInUnmodifiedSequence, int indexInModifiedSequence,
                char aa, ModTerminus? modTerminus, int roundedTo, bool isHeavy)
                : this(null, indexInUnmodifiedSequence, indexInModifiedSequence, aa, modTerminus, roundedTo, isHeavy)
            {
                Name = name;
                Mass = mass;
            }

            public string Name { get; }
            public double? Mass { get; }
            public StaticMod Mod { get; }
            public int IndexInUnmodifiedSequence { get; }
            public int IndexInModifiedSequence { get; }
            public char AA { get; }
            public ModTerminus? ModTerminus { get; }
            public int RoundedTo { get; }
            public bool IsHeavy { get; }
        }

        public static IEnumerable<ModInfo> EnumerateMods(string modifiedSequence, bool matchUnimodNames = true)
        {
            int indexAA = 0;
            int indexAAInSeq = 0;
            int i = 0;
            string seq = modifiedSequence;
            var unmodifiedSequence = FastaSequence.StripModifications(modifiedSequence);
            while (i < seq.Length)
            {
                var aa = unmodifiedSequence[indexAA];
                int indexBracket = i + 1;
                if (indexBracket < seq.Length && (FastaSequence.OPEN_MOD.Contains(seq[indexBracket])))
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
                    if (indexAA == unmodifiedSequence.Length - 1)
                        modTerminus = ModTerminus.C;

                    bool isHeavy = openBracket == '{';
                    int roundedTo = 0;
                    StaticMod staticMod = null;
                    MassModification massModification = MassModification.Parse(mod);
                    if (massModification != null)
                    {
                        staticMod = new StaticMod(mod, aa.ToString(), modTerminus, null, LabelAtoms.None, massModification.Mass, massModification.Mass);
                        roundedTo = Math.Min(massModification.Precision, DEFAULT_ROUNDING_DIGITS);
                    }
                    else
                    {
                        if (matchUnimodNames)
                        {
                            try
                            {
                                staticMod = GetStaticMod(mod, modTerminus, aa.ToString());
                            }
                            catch (ArgumentException)
                            {
                                TryGetIdFromUnimod(mod, out int uniModId);
                                ThrowUnimodException(seq, uniModId, indexAA, indexBracket, indexClose);
                            }
                        }
                        else
                        {
                            if (TryGetIdFromUnimod(mod, out int uniModId))
                            {
                                staticMod = GetStaticMod(uniModId, aa, modTerminus);
                                if (staticMod == null)
                                    throw ThrowUnimodException(seq, uniModId, indexAA, indexBracket, indexClose);
                            }
                            else
                            {
                                yield return new ModInfo(mod, null, indexAA, indexAAInSeq, aa, modTerminus, roundedTo, false);
                                continue;
                            }
                        }

                        roundedTo = DEFAULT_ROUNDING_DIGITS;
                        if (staticMod!.UnimodId != null)
                            isHeavy = !UniMod.IsStructuralModification(staticMod.Name);
                    }

                    yield return new ModInfo(staticMod, indexAA, indexAAInSeq, aa, modTerminus, roundedTo, isHeavy);
                }

                // If the next character is a bracket, continue using the same amino
                // acid and leave i where it is.
                int iNext = i + 1;
                if (iNext >= seq.Length || !FastaSequence.OPEN_MOD.Contains(seq[iNext]))
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
            const string prefixString = ModifiedSequence.UnimodPrefix;
            if (unimodString.ToLowerInvariant().StartsWith(prefixString))
            {
                int prefixLength = prefixString.Length;
                return int.TryParse(unimodString.Substring(prefixLength, unimodString.Length - prefixLength), out uniModId);
            }
            // Try short name string
            return UniMod.DictShortNamesToUniMod.TryGetValue(unimodString.ToLower(), out uniModId);
        }

        /// <summary>
        /// Get a UniMod StaticMod from any of: unimod:XXX numeric ID, short name, or full name.
        /// If the short name or ID has multiple specificities in UniMod, each one has its own StaticMod.
        /// In that case, the terminus or modAAs parameter must be used to choose which specificity to return.
        /// </summary>
        /// <param name="unimodNameOrId">Mod name or numeric ID</param>
        /// <param name="modTerminus">If non-null, match only mods with the given terminus specificity.</param>
        /// <param name="modAAs">If non-null, match only mods with the given AA specificity.</param>
        /// <exception cref="ArgumentException">When unimodNameOrId does not match anything in UniMod,
        /// or when it matches to multiple StaticMods and modTerminus or modAAs are not provided to pick which specific one to return.</exception>
        public static StaticMod GetStaticMod(string unimodNameOrId, ModTerminus? modTerminus, string modAAs)
        {
            if (TryGetIdFromUnimod(unimodNameOrId, out int uniModId))
            {
                var mod = GetStaticMod(uniModId, modAAs?[0] ?? 'A', modTerminus);
                if (mod != null) return mod;

                var id = uniModId;
                int idMatches = UniMod.DictUniModIds.Count(kvp => kvp.Key.Id == id);
                if (idMatches == 1) // key didn't match because terminus and AAs weren't needed for specificity
                    return UniMod.DictUniModIds.First(kvp => kvp.Key.Id == id).Value;

                Assume.IsTrue(idMatches > 0); // if there were 0 matches, TryGetIdFromUnimod should not have returned true
                if (modTerminus == null && modAAs == null)
                {
                    throw new ArgumentException(ModelResources
                        .ModificationMatcher_GetStaticMod_found_more_than_one_UniMod_match__add_terminus_and_or_amino_acid_specificity_to_choose_a_single_match);
                }
                else
                {
                    var specificityOptions = new List<string>();
                    if (modTerminus.HasValue)
                        specificityOptions.Add(TextUtil.ColonSeparate(PropertyNames.StaticMod_Terminus, modTerminus.ToString()));
                    if (modAAs != null)
                        specificityOptions.Add(TextUtil.ColonSeparate(PropertyNames.StaticMod_AAs, modAAs));
                    string specificity = TextUtil.TextSeparate(TextUtil.CsvSeparator.ToString(), specificityOptions);
                    throw new ArgumentException(string.Format(
                        ModelResources.ModificationMatcher_GetStaticMod_found_more_than_one_UniMod_match_but_the_given_specificity___0___does_not_match_any_of_them_,
                        specificity));
                }
            }

            // Try long name string
            return UniMod.GetModification(unimodNameOrId, out _) ??
                   throw new ArgumentException(ModelResources.ModificationMatcher_GetStaticMod_no_UniMod_match);
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
                if (indexBracket < seq.Length && (FastaSequence.OPEN_MOD.Contains(seq[indexBracket])))
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
                        sb[indexBracket] = isHeavy ? '{' : '[';
                        sb[indexClose] = isHeavy ? '}' : ']';
                    }
                }
                // If the next character is a bracket, continue using the same amino
                // acid and leave i where it is.
                int iNext = i + 1;
                if (iNext >= seq.Length || !FastaSequence.OPEN_MOD.Contains(seq[iNext]))
                {
                    indexAA++;
                    i++;
                }
            }
            return sb.ToString();
        }

        public static Exception ThrowUnimodException(string seq, int uniModId, int indexAA, int indexBracket, int indexClose)
        {
            int indexFirst = Math.Max(0, indexBracket - 1);
            int indexLast = Math.Min(seq.Length, indexClose + 1);
            string unrecognizedAaMod = seq.Substring(indexFirst, indexLast - indexFirst);
            if (UniMod.IsValidUnimodId(uniModId))
            {
                throw new FormatException(
                    string.Format(ModelResources.ModificationMatcher_ThrowUnimodException_Unrecognized_modification_placement_for_Unimod_id__0__in_modified_peptide_sequence__1___amino_acid__2____3___,
                        uniModId, seq, indexAA + 1, unrecognizedAaMod));
            }

            throw new FormatException(
                string.Format(ModelResources.ModificationMatcher_ThrowUnimodException_Unrecognized_Unimod_id__0__in_modified_peptide_sequence__1___amino_acid__2____3___,
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

        public override PeptideDocNode GetModifiedNode(string seq)
        {
            return GetModifiedNode(seq, null);
        }

        public PeptideDocNode GetModifiedNode(string seq, FastaSequence fastaSequence)
        {
            var seqUnmod = FastaSequence.StripModifications(seq);
            var peptide = fastaSequence != null
              ? fastaSequence.CreateFullPeptideDocNode(Settings, new Target(seqUnmod)).Peptide
              : new Peptide(null, seqUnmod, null, null,
                            Settings.PeptideSettings.Enzyme.CountCleavagePoints(seqUnmod));
            // First, try to create the peptide using the current settings.
            PeptideDocNode nodePep = 
                CreateDocNodeFromSettings(new Target(seq), peptide, SrmSettingsDiff.ALL, out _);
            if (nodePep != null)
                return nodePep;
            // Create the peptideDocNode.
            nodePep = fastaSequence == null
              ? new PeptideDocNode(peptide)
              : fastaSequence.CreateFullPeptideDocNode(Settings, new Target(seqUnmod));
            return CreateDocNodeFromMatches(nodePep, EnumerateSequenceInfos(seq, false));
        }

        protected override bool IsMatch(Target target, PeptideDocNode nodePep, out TransitionGroupDocNode nodeGroup)
        {
            string seqSimplified = SimplifyUnimodSequence(target.Sequence);
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
                    StaticMod modMatch;
                    if (structural)
                    {
                        modMatch = modifications.StaticModifications.FirstOrDefault(mod => Equals(mod.Name, info1.Name));
                    }
                    else
                    {
                        modMatch = modifications.AllHeavyModifications.FirstOrDefault(mod => Equals(mod.Name, info1.Name));
                    }

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
