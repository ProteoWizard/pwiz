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

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Contracts;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// The Osprey library contract on hand-built rows: a library that meets it reports nothing,
    /// and one row of each defect reports exactly its class, with the count and example the
    /// report names. The classes are checked one at a time so a rule that fires for the wrong
    /// reason shows as an extra class rather than hiding behind the expected one.
    /// </summary>
    [TestClass]
    public class LibraryContractTest
    {
        private static readonly string[] PREFIXES = { @"decoy_" };

        [TestMethod]
        public void TestLibraryContract()
        {
            var manifest = new List<PairingManifestRow>
            {
                ManifestRow(@"PEPTIDEK", PairingManifest.KIND_TARGET, @"sp|P1|A;sp|P2|B", 1),
                ManifestRow(@"KEDITPEP", PairingManifest.KIND_DECOY, @"decoy_sp|P1|A;decoy_sp|P2|B", 1),
                ManifestRow(@"SAMPLERK", PairingManifest.KIND_P_TARGET, @"sp|P1_p_target|A_p_target", 1),
                ManifestRow(@"LPMSAERK", PairingManifest.KIND_P_DECOY, @"decoy_sp|P1_p_target|A_p_target", 1),
                ManifestRow(@"MELVISLIVESK", PairingManifest.KIND_TARGET, @"sp|P3|C", 2),
                ManifestRow(@"MSEVILSIVLEK", PairingManifest.KIND_DECOY, @"decoy_sp|P3|C", 2),
            };
            var clean = new List<LibraryContractRow>
            {
                Row(@"PEPTIDEK", @"PEPTIDEK", 2, @"sp|P1|A;sp|P2|B"),
                Row(@"PEPTIDEK", @"PEPTIDEK", 3, @"sp|P1|A;sp|P2|B"),
                Row(@"KEDITPEP", @"KEDITPEP", 2, @"decoy_sp|P1|A;decoy_sp|P2|B"),
                Row(@"KEDITPEP", @"KEDITPEP", 3, @"decoy_sp|P1|A;decoy_sp|P2|B"),
                Row(@"SAMPLERK", @"SAMPLERK", 2, @"sp|P1_p_target|A_p_target"),
                Row(@"LPMSAERK", @"LPMSAERK", 2, @"decoy_sp|P1_p_target|A_p_target"),
                // The clip of an N-terminal methionine peptide pairs with the clip of its partner.
                Row(@"ELVISLIVESK", @"ELVISLIVESK", 2, @"sp|P3|C"),
                Row(@"SEVILSIVLEK", @"SEVILSIVLEK", 2, @"decoy_sp|P3|C"),
            };
            var report = LibraryContract.Validate(clean, manifest, PREFIXES);
            Assert.IsTrue(report.IsValid, report.Describe());
            Assert.IsTrue(report.ManifestChecked);
            Assert.AreEqual(string.Empty, report.Describe());

            // Without a manifest rules 1 to 4 pass and the report says what was not checked.
            report = LibraryContract.Validate(clean, null, PREFIXES);
            Assert.IsTrue(report.IsValid);
            Assert.IsFalse(report.ManifestChecked);
            Assert.AreEqual(ContractsResources.LibraryContractReport_Describe_No_pairing_manifest_was_given__so_the_rows__kinds__accessions_and_pairs_were_not_checked_against_one_,
                report.Describe());

            // One defect at a time, each the one class it names.
            AssertViolation(LibraryContractViolationClass.DuplicatePrecursor, @"PEPTIDEK/2 x2", manifest,
                clean.Concat(new[] { Row(@"PEPTIDEK", @"PEPTIDEK", 2, @"sp|P1|A;sp|P2|B") }).ToList());
            AssertViolation(LibraryContractViolationClass.AccessionSuffix, @"sp|P1_pep00019|A", manifest,
                Replace(clean, 0, Row(@"PEPTIDEK", @"PEPTIDEK", 2, @"sp|P1_pep00019|A;sp|P2|B")),
                LibraryContractViolationClass.ProteinsDisagreeWithManifest);
            AssertViolation(LibraryContractViolationClass.DecoyUnmarked, @"KEDITPEP: sp|P2|B", manifest,
                Replace(clean, 2, Row(@"KEDITPEP", @"KEDITPEP", 2, @"decoy_sp|P1|A;sp|P2|B")),
                LibraryContractViolationClass.ProteinsDisagreeWithManifest);
            AssertViolation(LibraryContractViolationClass.TargetMarkedDecoy, @"PEPTIDEK: decoy_sp|P2|B", manifest,
                Replace(clean, 0, Row(@"PEPTIDEK", @"PEPTIDEK", 2, @"sp|P1|A;decoy_sp|P2|B")),
                LibraryContractViolationClass.ProteinsDisagreeWithManifest);
            AssertViolation(LibraryContractViolationClass.EntrapmentUnmarked, @"SAMPLERK: sp|P1|A", manifest,
                Replace(clean, 4, Row(@"SAMPLERK", @"SAMPLERK", 2, @"sp|P1_p_target|A_p_target;sp|P1|A")),
                LibraryContractViolationClass.ProteinsDisagreeWithManifest);
            AssertViolation(LibraryContractViolationClass.NotInManifest, @"UNKNOWNPEPTIDEK", manifest,
                clean.Concat(new[] { Row(@"UNKNOWNPEPTIDEK", @"UNKNOWNPEPTIDEK", 2, @"sp|P9|Z") }).ToList());
            // The merged-decoy defect: a decoy row whose sequence the manifest calls a target.
            AssertViolation(LibraryContractViolationClass.KindDisagreesWithManifest, @"PEPTIDEK (target in the manifest)", manifest,
                Replace(clean, 0, Row(@"PEPTIDEK", @"PEPTIDEK", 2, @"decoy_sp|P1|A;decoy_sp|P2|B")));
            AssertViolation(LibraryContractViolationClass.ProteinsDisagreeWithManifest, @"PEPTIDEK: sp|P1|A (manifest sp|P1|A;sp|P2|B)", manifest,
                Replace(clean, 0, Row(@"PEPTIDEK", @"PEPTIDEK", 2, @"sp|P1|A")));
            AssertViolation(LibraryContractViolationClass.UnpairedDecoy, @"KEDITPEP/3 (no PEPTIDEK/3)", manifest,
                clean.Where((_, i) => i != 1).ToList());
            AssertViolation(LibraryContractViolationClass.UnpairedTarget, @"ELVISLIVESK/2 (no SEVILSIVLEK/2)", manifest,
                clean.Where((_, i) => i != 7).ToList());

            // The library's own marking: a DecoyPairs row disagreeing with the manifest, and, with
            // no manifest, with the accessions.
            AssertViolation(LibraryContractViolationClass.KindDisagreesWithManifest, @"PEPTIDEK (target in the manifest)", manifest,
                Replace(clean, 0, Row(@"PEPTIDEK", @"PEPTIDEK", 2, @"sp|P1|A;sp|P2|B", true)));
            AssertViolation(LibraryContractViolationClass.TargetMarkedDecoy, @"PEPTIDEK: decoy_sp|P1|A", null,
                Replace(clean, 0, Row(@"PEPTIDEK", @"PEPTIDEK", 2, @"decoy_sp|P1|A;sp|P2|B", false)).Take(1).ToList());

            // Without a manifest a decoy row is one with any decoy accession, and its others must agree.
            AssertViolation(LibraryContractViolationClass.DecoyUnmarked, @"KEDITPEP: sp|P2|B", null,
                Replace(clean, 2, Row(@"KEDITPEP", @"KEDITPEP", 2, @"decoy_sp|P1|A;sp|P2|B")));

            // Rule 6 from the library's own pairing (a blib's DecoyPairs) when there is no manifest:
            // each pair id names one decoy and one target at one charge, and the report says the
            // pairs were checked that way. A decoy whose pair has no target row is unpaired.
            var tablePaired = new List<LibraryContractRow>
            {
                Row(@"PEPTIDEK", @"PEPTIDEK", 2, @"sp|P1|A;sp|P2|B", false, 1),
                Row(@"KEDITPEP", @"KEDITPEP", 2, @"decoy_sp|P1|A;decoy_sp|P2|B", true, 1),
            };
            report = LibraryContract.Validate(tablePaired, null, PREFIXES);
            Assert.IsTrue(report.IsValid, report.Describe());
            Assert.IsTrue(report.PairsCheckedFromLibrary);
            Assert.AreEqual(ContractsResources.LibraryContractReport_Describe_No_pairing_manifest_was_given__so_the_rows__kinds_and_accessions_were_not_checked_against_one__the_pairs_were_checked_from_the_library_s_own_DecoyPairs_table_,
                report.Describe());
            AssertViolation(LibraryContractViolationClass.UnpairedDecoy, @"KEDITPEP/3 (no target in pair 2)", null,
                tablePaired.Concat(new[] { Row(@"KEDITPEP", @"KEDITPEP", 3, @"decoy_sp|P1|A;decoy_sp|P2|B", true, 2) }).ToList());

            // Pairs-only and rows-only checks apply the rules they name.
            var unpaired = clean.Where((_, i) => i != 1).Select(r => Row(r.Sequence, r.ModifiedSequence, r.Charge, string.Empty)).ToList();
            report = LibraryContract.Validate(unpaired, manifest, PREFIXES, LibraryContractChecks.Pairs);
            Assert.AreEqual(LibraryContractViolationClass.UnpairedDecoy, report.Violations.Single().Class);
            var peptides = clean.Select(r => Row(r.Sequence, null, 0, string.Join(@";", r.Accessions))).ToList();
            Assert.IsTrue(LibraryContract.Validate(peptides, manifest, PREFIXES, LibraryContractChecks.Rows).IsValid);
        }

        private static void AssertViolation(LibraryContractViolationClass expected, string example,
            List<PairingManifestRow> manifest, List<LibraryContractRow> rows, params LibraryContractViolationClass[] alsoExpected)
        {
            var report = LibraryContract.Validate(rows, manifest, PREFIXES);
            var classes = report.Violations.Select(v => v.Class).ToArray();
            CollectionAssert.AreEquivalent(new[] { expected }.Concat(alsoExpected).ToArray(), classes, report.Describe());
            var violation = report.Violations.Single(v => v.Class == expected);
            Assert.AreEqual(1, violation.Count, report.Describe());
            Assert.AreEqual(example, violation.Examples.Single());
            StringAssert.Contains(report.Describe(), expected + @": 1 (" + example + @")");
            StringAssert.Contains(report.Describe(), string.Format(
                ContractsResources.LibraryContractReport_Describe_The_library_violates_the_Osprey_library_contract_in__0__way_s__, classes.Length));
        }

        private static List<LibraryContractRow> Replace(List<LibraryContractRow> rows, int index, LibraryContractRow row)
        {
            var copy = new List<LibraryContractRow>(rows);
            copy[index] = row;
            return copy;
        }

        private static LibraryContractRow Row(string sequence, string modifiedSequence, int charge, string accessions, bool? markedDecoy = null,
            int? pairId = null)
        {
            return new LibraryContractRow(sequence, modifiedSequence, charge,
                accessions.Length > 0 ? accessions.Split(';') : new string[0], markedDecoy, pairId);
        }

        private static PairingManifestRow ManifestRow(string sequence, string kind, string proteins, int pairIndex)
        {
            PeptideKinds.TryParse(kind, out var parsed);
            return new PairingManifestRow(sequence, PeptideKinds.IsDecoySide(parsed) ? PairingManifest.DECOY_YES : PairingManifest.DECOY_NO,
                proteins, kind, parsed, pairIndex);
        }
    }
}
