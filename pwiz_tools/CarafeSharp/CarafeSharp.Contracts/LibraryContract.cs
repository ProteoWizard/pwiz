/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Text.RegularExpressions;

namespace pwiz.CarafeSharp.Contracts
{
    /// <summary>
    /// What a correct Osprey spectral library is, checked by one piece of code on both sides:
    /// CarafeSharp runs it before it keeps a library it writes in contract mode, and Osprey runs
    /// it when it loads a library that claims the contract (the <see cref="METADATA_KEY"/> row of
    /// its <c>LibMetadata</c> table). The rules, by number (Osprey docs, "The Osprey library
    /// contract"):
    /// <list type="number">
    /// <item>One row per precursor: no two rows share a modified sequence and charge.</item>
    /// <item>Clean accessions: no per-peptide <c>_pepNNNNN</c> suffix.</item>
    /// <item>Decoys are marked, and only decoys: every accession of a decoy row carries a decoy
    /// prefix, no accession of a target row does, and a library's own decoy marking (DecoyPairs)
    /// agrees.</item>
    /// <item>Entrapment is marked: every accession of an entrapment row carries
    /// <see cref="ENTRAPMENT_MARKER"/>.</item>
    /// <item>The manifest is authoritative: every sequence is in it (a peptide clipped of its
    /// N-terminal methionine counts as its unclipped manifest peptide), with the kind and the
    /// accessions the manifest gives it.</item>
    /// <item>Pairs are whole: a decoy row's target, and a target row's decoy, is in the library
    /// at the same charge; the entrapment partition pairs the same way.</item>
    /// </list>
    /// Rules 1 to 4 are checked without a manifest; 5 and 6 need one. Every violation is
    /// reported, grouped by class with its count and the first few examples, so one report says
    /// how many kinds of defect a library has and what each looks like.
    /// </summary>
    public static class LibraryContract
    {
        /// <summary>The <c>LibMetadata</c> key a library claims the contract with (one of the shared, unprefixed keys), and its value.</summary>
        public const string METADATA_KEY = @"library_contract";
        public const string METADATA_VALUE = @"1";

        /// <summary>The accession marker of an entrapment peptide, as the pairing manifest spells it.</summary>
        public const string ENTRAPMENT_MARKER = @"_p_target";

        /// <summary>
        /// Carafe's per-peptide pseudo-protein suffix, <c>_pep</c> and digits, mid-accession.
        /// Digits are required, so a real accession containing a bare <c>_pep</c> is left alone.
        /// </summary>
        public static readonly Regex PEPTIDE_SUFFIX = new Regex(@"_pep\d+", RegexOptions.CultureInvariant);

        /// <summary>How many rows a class reports as examples.</summary>
        public const int MAX_EXAMPLES = 5;

        /// <summary>
        /// Checks <paramref name="rows"/> against the rules above. <paramref name="manifest"/>
        /// may be null, which checks rules 1 to 4 only and says so in the report.
        /// <paramref name="checks"/> narrows the rules: <see cref="LibraryContractChecks.Rows"/>
        /// (rules 1 to 5) for peptides whose charges are not known yet, or
        /// <see cref="LibraryContractChecks.Pairs"/> (rules 1 and 6) for written precursors whose
        /// accessions are not at hand. <paramref name="decoyPrefixes"/> are the accession
        /// prefixes that mark a decoy, compared ignoring case.
        /// </summary>
        public static LibraryContractReport Validate(IReadOnlyList<LibraryContractRow> rows,
            IReadOnlyList<PairingManifestRow> manifest, IReadOnlyList<string> decoyPrefixes,
            LibraryContractChecks checks = LibraryContractChecks.All)
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));
            var prefixes = (decoyPrefixes ?? Array.Empty<string>()).Where(p => !string.IsNullOrEmpty(p)).ToArray();
            var tallies = new Dictionary<LibraryContractViolationClass, Tally>();
            CheckDuplicates(rows, tallies);
            var bySequence = manifest != null ? IndexManifest(manifest) : null;
            if ((checks & LibraryContractChecks.Rows) != 0)
            {
                foreach (var row in rows)
                {
                    var marks = new RowMarks(row, prefixes);
                    CheckAccessionSuffixes(row, tallies);
                    if (bySequence == null)
                    {
                        CheckMarkingWithoutManifest(row, marks, tallies);
                        continue;
                    }
                    var entry = FindManifestEntry(bySequence, row.Sequence, out _);
                    if (entry == null)
                    {
                        Hit(tallies, LibraryContractViolationClass.NotInManifest, row.Sequence);
                        continue;
                    }
                    CheckMarkingAgainstManifest(row, marks, entry, tallies);
                }
            }
            bool pairsFromLibrary = false;
            if ((checks & LibraryContractChecks.Pairs) != 0)
            {
                if (bySequence != null)
                    CheckPairs(rows, manifest, bySequence, tallies);
                else
                    pairsFromLibrary = CheckPairsFromLibrary(rows, tallies);
            }
            var violations = tallies.OrderBy(pair => pair.Key).Select(pair => pair.Value.ToViolation(pair.Key)).ToArray();
            return new LibraryContractReport(violations, manifest != null, pairsFromLibrary);
        }

        private static void CheckDuplicates(IReadOnlyList<LibraryContractRow> rows, Dictionary<LibraryContractViolationClass, Tally> tallies)
        {
            var counts = new Dictionary<(string, int), int>();
            foreach (var row in rows)
            {
                if (row.Charge <= 0 || row.ModifiedSequence == null)
                    continue;
                var key = (row.ModifiedSequence, row.Charge);
                counts.TryGetValue(key, out int count);
                counts[key] = count + 1;
            }
            foreach (var pair in counts.Where(pair => pair.Value > 1).OrderBy(pair => pair.Key.Item1, StringComparer.Ordinal).ThenBy(pair => pair.Key.Item2))
            {
                Hit(tallies, LibraryContractViolationClass.DuplicatePrecursor, string.Format(CultureInfo.InvariantCulture,
                    @"{0}/{1} x{2}", pair.Key.Item1, pair.Key.Item2, pair.Value));
            }
        }

        private static void CheckAccessionSuffixes(LibraryContractRow row, Dictionary<LibraryContractViolationClass, Tally> tallies)
        {
            foreach (string accession in row.Accessions)
            {
                if (PEPTIDE_SUFFIX.IsMatch(accession))
                    Hit(tallies, LibraryContractViolationClass.AccessionSuffix, accession);
            }
        }

        /// <summary>
        /// Rules 3 and 4 with no manifest to say what a row is: a row is a decoy row when any
        /// accession carries a decoy prefix, or when the library's own marking says so, and an
        /// entrapment row when any accession carries the marker; the rest of its accessions must
        /// agree.
        /// </summary>
        private static void CheckMarkingWithoutManifest(LibraryContractRow row, RowMarks marks,
            Dictionary<LibraryContractViolationClass, Tally> tallies)
        {
            if (row.MarkedDecoy == false && marks.AnyDecoy)
                HitAccessions(tallies, LibraryContractViolationClass.TargetMarkedDecoy, row, marks.DecoyMarked, true);
            else if ((marks.AnyDecoy || row.MarkedDecoy == true) && !marks.AllDecoy)
                HitAccessions(tallies, LibraryContractViolationClass.DecoyUnmarked, row, marks.DecoyMarked, false);
            if (marks.AnyEntrapment && !marks.AllEntrapment)
                HitAccessions(tallies, LibraryContractViolationClass.EntrapmentUnmarked, row, marks.EntrapmentMarked, false);
        }

        /// <summary>
        /// Rules 3, 4 and 5 with the manifest saying what a row is. A row uniformly of the other
        /// kind disagrees with the manifest (the merged-decoy defect); a row of the right kind with
        /// a stray accession is a marking defect of that accession. A row whose kind agrees must
        /// carry the manifest's accessions.
        /// </summary>
        private static void CheckMarkingAgainstManifest(LibraryContractRow row, RowMarks marks, PairingManifestRow entry,
            Dictionary<LibraryContractViolationClass, Tally> tallies)
        {
            if (!entry.Kind.HasValue)
                return;
            bool expectDecoy = PeptideKinds.IsDecoySide(entry.Kind.Value);
            bool expectEntrapment = PeptideKinds.IsEntrapment(entry.Kind.Value);
            bool kindAgrees = true;
            if (row.Accessions.Count > 0)
            {
                if (expectDecoy ? !marks.AnyDecoy : marks.AllDecoy)
                    kindAgrees = false;
                if (expectEntrapment ? !marks.AnyEntrapment : marks.AllEntrapment)
                    kindAgrees = false;
            }
            else if (expectDecoy || expectEntrapment)
            {
                kindAgrees = false;
            }
            if (row.MarkedDecoy.HasValue && row.MarkedDecoy.Value != expectDecoy)
                kindAgrees = false;
            if (!kindAgrees)
            {
                Hit(tallies, LibraryContractViolationClass.KindDisagreesWithManifest, string.Format(CultureInfo.InvariantCulture,
                    @"{0} ({1} in the manifest)", row.Sequence, entry.KindText));
                return;
            }
            if (expectDecoy && !marks.AllDecoy)
                HitAccessions(tallies, LibraryContractViolationClass.DecoyUnmarked, row, marks.DecoyMarked, false);
            else if (!expectDecoy && marks.AnyDecoy)
                HitAccessions(tallies, LibraryContractViolationClass.TargetMarkedDecoy, row, marks.DecoyMarked, true);
            if (expectEntrapment && !marks.AllEntrapment)
                HitAccessions(tallies, LibraryContractViolationClass.EntrapmentUnmarked, row, marks.EntrapmentMarked, false);
            if (!SameAccessions(row.Accessions, entry.Proteins))
            {
                Hit(tallies, LibraryContractViolationClass.ProteinsDisagreeWithManifest, string.Format(CultureInfo.InvariantCulture,
                    @"{0}: {1} (manifest {2})", row.Sequence, string.Join(@";", row.Accessions), entry.ProteinsText));
            }
        }

        /// <summary>
        /// Rule 6: for every row in a manifest pair, the partner side's sequence is in the library
        /// at the row's charge. A pair the manifest gives no partner for (a manifest built without
        /// decoys) has nothing to check.
        /// </summary>
        private static void CheckPairs(IReadOnlyList<LibraryContractRow> rows, IReadOnlyList<PairingManifestRow> manifest,
            Dictionary<string, PairingManifestRow> bySequence, Dictionary<LibraryContractViolationClass, Tally> tallies)
        {
            var groups = new Dictionary<int, Dictionary<PeptideKind, string>>();
            foreach (var entry in manifest)
            {
                if (!entry.Kind.HasValue)
                    continue;
                if (!groups.TryGetValue(entry.PairIndex, out var group))
                    groups.Add(entry.PairIndex, group = new Dictionary<PeptideKind, string>());
                group[entry.Kind.Value] = entry.Sequence;
            }
            var present = new HashSet<(string, int)>();
            foreach (var row in rows)
            {
                if (row.Charge > 0 && row.Sequence != null)
                    present.Add((row.Sequence, row.Charge));
            }
            foreach (var row in rows)
            {
                if (row.Charge <= 0)
                    continue;
                var entry = FindManifestEntry(bySequence, row.Sequence, out bool clipped);
                if (entry == null || !entry.Kind.HasValue || !groups.TryGetValue(entry.PairIndex, out var group))
                    continue;
                var kind = entry.Kind.Value;
                if (!group.TryGetValue(PartnerKind(kind), out string partner))
                    continue;
                if (clipped)
                {
                    partner = StripLeadingMethionine(partner);
                    if (partner == null)
                        continue;
                }
                if (present.Contains((partner, row.Charge)))
                    continue;
                var violationClass = PeptideKinds.IsDecoySide(kind)
                    ? LibraryContractViolationClass.UnpairedDecoy
                    : LibraryContractViolationClass.UnpairedTarget;
                Hit(tallies, violationClass, string.Format(CultureInfo.InvariantCulture, @"{0}/{1} (no {2}/{1})", row.Sequence, row.Charge, partner));
            }
        }

        /// <summary>
        /// Rule 6 from the library's own pairing, for a library without a manifest: every decoy
        /// row names a target row of the same charge in <see cref="LibraryContractRow.TargetRefSpectraId"/>,
        /// and every target row is named by exactly one decoy row - the rule Osprey pairs by. A
        /// decoy naming no target, or a target of another charge, is unpaired; so is a target no
        /// decoy names, or that two name. Returns false when no row carries a spectrum id, so the
        /// caller knows the pairs went unchecked.
        /// </summary>
        private static bool CheckPairsFromLibrary(IReadOnlyList<LibraryContractRow> rows, Dictionary<LibraryContractViolationClass, Tally> tallies)
        {
            var byId = new Dictionary<long, LibraryContractRow>();
            foreach (var row in rows)
            {
                if (row.RefSpectraId.HasValue && row.MarkedDecoy.HasValue)
                    byId[row.RefSpectraId.Value] = row;
            }
            if (byId.Count == 0)
                return false;
            var decoysByTarget = new Dictionary<long, List<LibraryContractRow>>();
            foreach (var row in byId.Values.OrderBy(r => r.RefSpectraId.Value))
            {
                if (!row.MarkedDecoy.Value)
                    continue;
                var target = row.TargetRefSpectraId.HasValue && byId.TryGetValue(row.TargetRefSpectraId.Value, out var t) ? t : null;
                if (target == null || target.MarkedDecoy.Value || target.Charge != row.Charge)
                {
                    Hit(tallies, LibraryContractViolationClass.UnpairedDecoy,
                        string.Format(ContractsResources.LibraryContract_CheckPairsFromLibrary__0___1___no_target_in_pair__2__,
                            row.Sequence, row.Charge, row.PairingId));
                    continue;
                }
                if (!decoysByTarget.TryGetValue(target.RefSpectraId.Value, out var decoys))
                    decoysByTarget.Add(target.RefSpectraId.Value, decoys = new List<LibraryContractRow>());
                decoys.Add(row);
            }
            foreach (var row in byId.Values.OrderBy(r => r.RefSpectraId.Value))
            {
                if (row.MarkedDecoy.Value)
                    continue;
                if (!decoysByTarget.TryGetValue(row.RefSpectraId.Value, out var decoys))
                {
                    Hit(tallies, LibraryContractViolationClass.UnpairedTarget,
                        string.Format(ContractsResources.LibraryContract_CheckPairsFromLibrary__0___1___no_decoy_in_pair__2__,
                            row.Sequence, row.Charge, row.PairingId));
                    continue;
                }
                // A target pairs with the first decoy naming it (Osprey claims a target once);
                // any other decoy naming it is unpaired.
                for (int i = 1; i < decoys.Count; i++)
                {
                    Hit(tallies, LibraryContractViolationClass.UnpairedDecoy,
                        string.Format(ContractsResources.LibraryContract_CheckPairsFromLibrary__0___1___pair__2__has__3__decoy_row_s__and__4__target_row_s__,
                            decoys[i].Sequence, decoys[i].Charge, decoys[i].PairingId, decoys.Count, 1));
                }
            }
            return true;
        }

        private static PeptideKind PartnerKind(PeptideKind kind)
        {
            switch (kind)
            {
                case PeptideKind.Target:
                    return PeptideKind.Decoy;
                case PeptideKind.Decoy:
                    return PeptideKind.Target;
                case PeptideKind.PTarget:
                    return PeptideKind.PDecoy;
                default:
                    return PeptideKind.PTarget;
            }
        }

        /// <summary>The manifest rows by sequence; a sequence listed twice keeps its first row.</summary>
        private static Dictionary<string, PairingManifestRow> IndexManifest(IReadOnlyList<PairingManifestRow> manifest)
        {
            var bySequence = new Dictionary<string, PairingManifestRow>(StringComparer.Ordinal);
            foreach (var entry in manifest)
            {
                if (!string.IsNullOrEmpty(entry.Sequence) && !bySequence.ContainsKey(entry.Sequence))
                    bySequence.Add(entry.Sequence, entry);
            }
            return bySequence;
        }

        /// <summary>
        /// The manifest row of a library sequence: its own, or, for a peptide clipped of its
        /// N-terminal methionine, the unclipped peptide's (<paramref name="clipped"/> true).
        /// </summary>
        private static PairingManifestRow FindManifestEntry(Dictionary<string, PairingManifestRow> bySequence, string sequence, out bool clipped)
        {
            clipped = false;
            if (sequence == null)
                return null;
            if (bySequence.TryGetValue(sequence, out var entry))
                return entry;
            if (bySequence.TryGetValue(@"M" + sequence, out entry))
            {
                clipped = true;
                return entry;
            }
            return null;
        }

        private static string StripLeadingMethionine(string sequence)
        {
            return sequence != null && sequence.StartsWith(@"M", StringComparison.Ordinal) ? sequence.Substring(1) : null;
        }

        private static bool SameAccessions(IReadOnlyList<string> accessions, IReadOnlyList<string> expected)
        {
            if (accessions.Count != expected.Count)
                return false;
            var set = new HashSet<string>(accessions, StringComparer.Ordinal);
            return expected.All(set.Contains);
        }

        private static void Hit(Dictionary<LibraryContractViolationClass, Tally> tallies, LibraryContractViolationClass violationClass, string example)
        {
            if (!tallies.TryGetValue(violationClass, out var tally))
                tallies.Add(violationClass, tally = new Tally());
            tally.Hit(example);
        }

        /// <summary>One hit per accession of <paramref name="row"/> whose mark is <paramref name="marked"/>; a row with none hits by sequence.</summary>
        private static void HitAccessions(Dictionary<LibraryContractViolationClass, Tally> tallies, LibraryContractViolationClass violationClass,
            LibraryContractRow row, bool[] marks, bool marked)
        {
            bool any = false;
            for (int i = 0; i < marks.Length; i++)
            {
                if (marks[i] != marked)
                    continue;
                any = true;
                Hit(tallies, violationClass, string.Format(CultureInfo.InvariantCulture, @"{0}: {1}", row.Sequence, row.Accessions[i]));
            }
            if (!any)
                Hit(tallies, violationClass, row.Sequence);
        }

        /// <summary>Which of a row's accessions carry a decoy prefix and which the entrapment marker.</summary>
        private sealed class RowMarks
        {
            public RowMarks(LibraryContractRow row, string[] prefixes)
            {
                int count = row.Accessions.Count;
                DecoyMarked = new bool[count];
                EntrapmentMarked = new bool[count];
                AllDecoy = count > 0;
                AllEntrapment = count > 0;
                for (int i = 0; i < count; i++)
                {
                    string accession = row.Accessions[i] ?? string.Empty;
                    DecoyMarked[i] = prefixes.Any(p => accession.StartsWith(p, StringComparison.OrdinalIgnoreCase));
                    EntrapmentMarked[i] = accession.IndexOf(ENTRAPMENT_MARKER, StringComparison.Ordinal) >= 0;
                    AnyDecoy |= DecoyMarked[i];
                    AnyEntrapment |= EntrapmentMarked[i];
                    AllDecoy &= DecoyMarked[i];
                    AllEntrapment &= EntrapmentMarked[i];
                }
            }

            public bool[] DecoyMarked { get; }
            public bool[] EntrapmentMarked { get; }
            public bool AnyDecoy { get; }
            public bool AllDecoy { get; }
            public bool AnyEntrapment { get; }
            public bool AllEntrapment { get; }
        }

        /// <summary>A class's full count with its first examples.</summary>
        private sealed class Tally
        {
            private readonly List<string> _examples = new List<string>();
            private int _count;

            public void Hit(string example)
            {
                _count++;
                if (_examples.Count < MAX_EXAMPLES)
                    _examples.Add(example);
            }

            public LibraryContractViolation ToViolation(LibraryContractViolationClass violationClass)
            {
                return new LibraryContractViolation(violationClass, _count, _examples.ToArray());
            }
        }
    }

    /// <summary>
    /// One library precursor as the contract sees it: its stripped sequence, its modified
    /// sequence (BiblioSpec mass notation) and charge (0 when not yet known, which skips the
    /// per-precursor rules), its protein accessions, and the library's own word on whether it is
    /// a decoy (a DecoyPairs row or a Decoy column), when the library has one.
    /// </summary>
    public sealed class LibraryContractRow
    {
        public LibraryContractRow(string sequence, string modifiedSequence, int charge, IReadOnlyList<string> accessions,
            bool? markedDecoy = null, int? pairingId = null, long? refSpectraId = null, long? targetRefSpectraId = null)
        {
            Sequence = sequence;
            ModifiedSequence = modifiedSequence;
            Charge = charge;
            Accessions = accessions ?? Array.Empty<string>();
            MarkedDecoy = markedDecoy;
            PairingId = pairingId;
            RefSpectraId = refSpectraId;
            TargetRefSpectraId = targetRefSpectraId;
        }

        public string Sequence { get; }
        public string ModifiedSequence { get; }
        public int Charge { get; }
        public IReadOnlyList<string> Accessions { get; }
        /// <summary>The library's own marking of the row (a blib's <c>DecoyPairs.IsDecoy</c>), when it has one.</summary>
        public bool? MarkedDecoy { get; }
        /// <summary>The library's own pairing of the row (a blib's <c>DecoyPairs.PairingID</c>), when it has one.</summary>
        public int? PairingId { get; }
        /// <summary>The row's spectrum id in the library (a blib's <c>RefSpectraID</c>), when it has one.</summary>
        public long? RefSpectraId { get; }
        /// <summary>A decoy row's target spectrum (a blib's <c>DecoyPairs.TargetRefSpectraID</c>), when it names one.</summary>
        public long? TargetRefSpectraId { get; }
    }

    /// <summary>Which rules <see cref="LibraryContract.Validate"/> applies; rule 1 always runs.</summary>
    [Flags]
    public enum LibraryContractChecks
    {
        /// <summary>Rules 2 to 5: the accessions and kinds of each row.</summary>
        Rows = 1,
        /// <summary>Rule 6: every row's pair partner is in the library at its charge.</summary>
        Pairs = 2,
        All = Rows | Pairs,
    }

    /// <summary>The classes of contract violation, in the order of the rules they break.</summary>
    public enum LibraryContractViolationClass
    {
        DuplicatePrecursor,
        AccessionSuffix,
        DecoyUnmarked,
        TargetMarkedDecoy,
        EntrapmentUnmarked,
        NotInManifest,
        KindDisagreesWithManifest,
        ProteinsDisagreeWithManifest,
        UnpairedDecoy,
        UnpairedTarget,
    }

    /// <summary>One class of violation: how many rows, and the first few as examples.</summary>
    public sealed class LibraryContractViolation
    {
        public LibraryContractViolation(LibraryContractViolationClass violationClass, int count, IReadOnlyList<string> examples)
        {
            Class = violationClass;
            Count = count;
            Examples = examples;
        }

        public LibraryContractViolationClass Class { get; }
        public int Count { get; }
        public IReadOnlyList<string> Examples { get; }

        /// <summary>The class name, the count and the examples: <c>DuplicatePrecursor: 3 (PEPTIDEK/2 x2, ...)</c>.</summary>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, @"{0}: {1} ({2})", Class, Count, string.Join(@", ", Examples));
        }
    }

    /// <summary>Every violation of a library, by class, and whether a manifest was there to check rules 5 and 6.</summary>
    public sealed class LibraryContractReport
    {
        public LibraryContractReport(IReadOnlyList<LibraryContractViolation> violations, bool manifestChecked,
            bool pairsCheckedFromLibrary = false)
        {
            Violations = violations;
            ManifestChecked = manifestChecked;
            PairsCheckedFromLibrary = pairsCheckedFromLibrary;
        }

        public IReadOnlyList<LibraryContractViolation> Violations { get; }
        public bool ManifestChecked { get; }
        /// <summary>Rule 6 was checked from the library's own pairing (a blib's <c>DecoyPairs</c>), with no manifest.</summary>
        public bool PairsCheckedFromLibrary { get; }

        public bool IsValid
        {
            get { return Violations.Count == 0; }
        }

        /// <summary>
        /// The report as text: the classes with their counts, one per line, then a line for a
        /// check made without a manifest. Empty for a library that meets the contract.
        /// </summary>
        public string Describe()
        {
            var text = new StringBuilder();
            if (Violations.Count > 0)
            {
                text.AppendLine(string.Format(ContractsResources.LibraryContractReport_Describe_The_library_violates_the_Osprey_library_contract_in__0__way_s__,
                    Violations.Count));
                foreach (var violation in Violations)
                    text.AppendLine(@"  " + violation);
            }
            if (!ManifestChecked)
            {
                text.AppendLine(PairsCheckedFromLibrary
                    ? ContractsResources.LibraryContractReport_Describe_No_pairing_manifest_was_given__so_the_rows__kinds_and_accessions_were_not_checked_against_one__the_pairs_were_checked_from_the_library_s_own_DecoyPairs_table_
                    : ContractsResources.LibraryContractReport_Describe_No_pairing_manifest_was_given__so_the_rows__kinds__accessions_and_pairs_were_not_checked_against_one_);
            }
            return text.ToString().TrimEnd();
        }

        public override string ToString()
        {
            return Describe();
        }
    }
}
