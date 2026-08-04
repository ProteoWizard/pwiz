/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.CommonMsData;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    /// <summary>
    /// Regression test for "Times (390) and intensities (779) disagree in point count"
    /// when importing a Shimadzu MRM file whose method has two acquisition events
    /// at the same Q1 but with disjoint Q3 sets — e.g., Wesley Vermaelen's steroid
    /// hormone panel where Deoxycorticosterone (DOC) and 17α-hydroxyprogesterone
    /// both sit at Q1 = 331.2 but are scheduled as separate events with non-
    /// overlapping product-ion sets.
    ///
    /// Without the fix in <see cref="ChromCollector"/>.AddPoint / FillZeroes,
    /// SpectraChromDataProvider's "missing-ion" trailing zero-fill in
    /// ProcessExtractedSpectrum would append intensities to the unique-Q3
    /// collectors without matching times (each ChromCollector owns its own
    /// times in single-time SRM mode), desyncing the per-transition arrays
    /// and aborting import.
    ///
    /// A single import of Wesley's panel (augmented with synthetic boundary
    /// compounds at the shared Q1) drives three validations: the times/intensities
    /// fix (<see cref="ResultsTestDocumentContainer.AssertComplete"/>), the same-Q1
    /// chromatogram-collapse fix (<see cref="AssertNoSameQ1Collapse"/>), and the
    /// transition-aware matching rule (<see cref="AssertTransitionAwareMatching"/>).
    ///
    /// Reported on
    /// https://skyline.ms/home/support/announcements-thread.view?rowId=66356
    /// </summary>
    [TestClass]
    public class ShimadzuSrmDuplicateQ1Test : AbstractUnitTest
    {
        private const string ZIP_FILE = @"TestData\Results\ShimadzuSrmDuplicateQ1.zip";

        [TestMethod]
        public void ShimadzuSrmDuplicateQ1ImportTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            // Augment Wesley's panel with three synthetic boundary compounds at DOC / 17α-OH-P's
            // shared Q1 so a single import exercises both the same-Q1 collapse fix (real co-targets)
            // and the transition-aware matching rule (the phantoms). 200/250 are product channels
            // deliberately absent from the file.
            var p97 = ("97.1", "97.100548579909457");     // shared, measured
            var p81 = ("81.05", "81.05054857990946");     // shared, measured
            var pAbsentA = ("200", "200.00054857990946"); // not measured
            var pAbsentB = ("250", "250.00054857990946"); // not measured

            string srcDocPath = TestFilesDir.GetTestPath("Wesley.sky");
            string docText = File.ReadAllText(srcDocPath);
            int insertAt = docText.IndexOf("</peptide_list>", StringComparison.Ordinal);
            Assert.AreNotEqual(-1, insertAt,
                "Test setup: could not find a peptide_list to inject the boundary compounds into");
            string phantoms =
                PhantomMolecule(PHANTOM_MINORITY, p97, pAbsentA, pAbsentB) +        // 1 of 3
                PhantomMolecule(PHANTOM_HALF, p97, p81, pAbsentA, pAbsentB) +       // 2 of 4
                PhantomMolecule(PHANTOM_MAJORITY, p97, p81, pAbsentA);             // 2 of 3
            docText = docText.Substring(0, insertAt) + phantoms + docText.Substring(insertAt);
            string docPath = TestFilesDir.GetTestPath("WesleyIncidental.sky");
            File.WriteAllText(docPath, docText);

            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);

            using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
            {
                const string replicateName = "WesleyLCD";
                // Prefer the .lcd path through the real Shimadzu vendor reader;
                // fall back to the bundled .mzML when the Shimadzu reader isn't
                // available (offscreen/nightly mode).
                string extRaw = ExtensionTestContext.ExtShimadzuRaw;
                string dataPath = TestFilesDir.GetTestPath("Labsolutions_PackA_MA_alt" + extRaw);
                var chromSets = new[]
                {
                    new ChromatogramSet(replicateName, new[]
                        { new MsDataFilePath(dataPath) }),
                };
                var docResults = doc.ChangeMeasuredResults(new MeasuredResults(chromSets));
                Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
                // Without the fix, AssertComplete throws InvalidDataException with
                // "Times (390) and intensities (779) disagree in point count".
                docContainer.AssertComplete();

                var importedDoc = docContainer.Document;
                AssertNoSameQ1Collapse(importedDoc);
                AssertTransitionAwareMatching(importedDoc);
            }
        }

        /// <summary>
        /// Wesley's second issue from the same thread: precursors that share a Q1 m/z (DOC and
        /// 17α-OH-P both at 331.227 here) get collapsed by the chromatogram-to-target binding so only
        /// one retains data. For every same-Q1 group where at least one member got data, every other
        /// member must also have data. Phantom compounds are excluded — they exist to probe the
        /// matching rule and some are denied data by design (see <see cref="AssertTransitionAwareMatching"/>).
        /// </summary>
        private static void AssertNoSameQ1Collapse(SrmDocument importedDoc)
        {
            // HasResults only checks Results != null, not Count, so guard the index access
            // explicitly before reading Results[0].
            bool HasFirstResult(TransitionGroupDocNode tg) => tg.HasResults && tg.Results.Count > 0;

            var collisionGroups = importedDoc.MoleculePrecursorPairs
                .Where(p => !p.NodePep.ModifiedTarget.ToString().Contains(PHANTOM_MARKER))
                .GroupBy(p => p.NodeGroup.PrecursorMz)
                .Where(g => g.Count() > 1)
                .ToList();
            Assert.AreNotEqual(0, collisionGroups.Count,
                "Test setup: expected the document to contain at least one duplicate-Q1 pair");
            var failures = new List<string>();
            foreach (var collisionGroup in collisionGroups)
            {
                var members = collisionGroup.ToList();
                bool anyHasData = members.Any(p => HasFirstResult(p.NodeGroup)
                                                   && !p.NodeGroup.Results[0].IsEmpty);
                if (!anyHasData)
                    continue; // No data for this Q1 in the file; not a collapse.

                // Group-level: every same-Q1 peptide must have at least one
                // chromatogram (covers "same Q1, different Q3" — the user-
                // visible "compound is missing" symptom in the support thread).
                foreach (var pair in members)
                {
                    var name = pair.NodePep.ModifiedTarget.ToString();
                    if (!HasFirstResult(pair.NodeGroup))
                        failures.Add(string.Format("Q1 {0} ({1}): no Results[0]",
                            pair.NodeGroup.PrecursorMz, name));
                    else if (pair.NodeGroup.Results[0].IsEmpty)
                        failures.Add(string.Format("Q1 {0} ({1}): empty Results[0]",
                            pair.NodeGroup.PrecursorMz, name));
                }

                // Transition-level: where two colliding peptides share a Q3
                // transition (e.g. Cortexolone_diMO and Corticosterone_diMO
                // both list Q3=343.2 at Q1=405.275), each must have data
                // for that transition — the "same Q1, same Q3" case.
                var byQ3 = new Dictionary<SignedMz, List<KeyValuePair<PeptidePrecursorPair, TransitionDocNode>>>();
                foreach (var pair in members)
                {
                    foreach (var nodeTran in pair.NodeGroup.Transitions)
                    {
                        if (!byQ3.TryGetValue(nodeTran.Mz, out var list))
                            byQ3[nodeTran.Mz] = list = new List<KeyValuePair<PeptidePrecursorPair, TransitionDocNode>>();
                        list.Add(new KeyValuePair<PeptidePrecursorPair, TransitionDocNode>(pair, nodeTran));
                    }
                }
                foreach (var sharedQ3 in byQ3.Where(kv => kv.Value.Count > 1))
                {
                    foreach (var entry in sharedQ3.Value)
                    {
                        var nodeTran = entry.Value;
                        if (!nodeTran.HasResults || nodeTran.Results.Count == 0 || nodeTran.Results[0].IsEmpty)
                        {
                            failures.Add(string.Format(
                                "Q1 {0} Q3 {1} ({2}): shared transition has no chromatogram after import",
                                entry.Key.NodeGroup.PrecursorMz, sharedQ3.Key,
                                entry.Key.NodePep.ModifiedTarget));
                        }
                    }
                }

                // NOTE: a stricter check would assert that each picked
                // peak boundary contains the peptide's ExplicitRetentionTime
                // — necessary when two compounds at the same Q1+Q3 must be
                // distinguished by retention time alone. That check passes
                // for most colliding pairs in this dataset but fails for
                // Cortexolone_MO at Q1=376.248: the picker prefers the
                // dominant peak over a smaller one near the explicit RT.
                // The behavior reproduces on master without any collision,
                // so it is a pre-existing peak-picker issue independent of
                // the binding fix tested here. Tracked in
                // https://github.com/ProteoWizard/pwiz/issues/4306.
            }
            Assert.AreEqual(0, failures.Count,
                "Same-Q1 compounds dropped during import:\n  " +
                string.Join("\n  ", failures));
        }

        // Synthetic compounds injected at DOC / 17α-OH-P's shared Q1 (331.227) to pin the
        // transition-aware matching rule. Those two real co-targets together define the channels
        // measured at this Q1 — the union of their products {81.05, 97.1, 109.05, 121} (the values
        // in the file's binary m/z arrays; the isolation-window labels are integer-rounded). A
        // peptide is assigned only when at least half of its own transitions match those
        // channels (matched*2 >= total). The names encode "matched of total":
        private const string PHANTOM_MINORITY = "PhantomMinority1of3"; // excluded
        private const string PHANTOM_HALF = "PhantomHalf2of4";         // included (exact boundary)
        private const string PHANTOM_MAJORITY = "PhantomMajority2of3"; // included
        private const string PHANTOM_MARKER = "Phantom";

        /// <summary>
        /// A &lt;molecule&gt; at the shared Q1 (331.226771) with the given product ions. Mirrors the
        /// DOC block (same neutral/ion formula, so the same Q1) with a distinct name (distinct
        /// identity); each product is a custom [M+] ion specified directly by (m/z, neutral mass).
        /// </summary>
        private static string PhantomMolecule(string name, params (string mz, string neutral)[] products)
        {
            string xml =
                "    <molecule explicit_retention_time=\"5.4\" auto_manage_children=\"false\" neutral_formula=\"C21H30O3\" neutral_mass_average=\"330.46425\" neutral_mass_monoisotopic=\"330.219495\" custom_ion_name=\"" + name + "\">\r\n" +
                "      <precursor charge=\"1\" precursor_mz=\"331.226771\" explicit_collision_energy=\"45\" auto_manage_children=\"false\" collision_energy=\"0\" ion_formula=\"C21H30O3[M+H]\" neutral_mass_average=\"330.46425\" neutral_mass_monoisotopic=\"330.219495\" custom_ion_name=\"" + name + "\">\r\n";
            foreach (var p in products)
                xml +=
                    "        <transition fragment_type=\"custom\" ion_formula=\"[M+]\" neutral_mass_average=\"" + p.neutral + "\" neutral_mass_monoisotopic=\"" + p.neutral + "\" product_charge=\"1\">\r\n" +
                    "          <precursor_mz>331.226771</precursor_mz>\r\n" +
                    "          <product_mz>" + p.mz + "</product_mz>\r\n" +
                    "          <collision_energy>0</collision_energy>\r\n" +
                    "        </transition>\r\n";
            return xml +
                   "      </precursor>\r\n" +
                   "    </molecule>\r\n";
        }

        /// <summary>
        /// Deciding which compound(s) a shared-Q1 SRM spectrum belongs to must use the product ions,
        /// not the precursor m/z alone. A peptide is assigned the data at a shared Q1 only when at
        /// least half of its own transitions match the channels the file actually measured there
        /// (matched*2 &gt;= total); a compound matching only a minority is an incidental collision —
        /// e.g. a compound targeted by a different acquisition method — and must not be handed that
        /// Q1's chromatogram.
        ///
        /// The three injected phantoms at DOC / 17α-OH-P's Q1 (331.227) bracket the rule:
        /// 1 of 3 (minority → no data), 2 of 4 (exactly half → data), and 2 of 3 (majority → data).
        /// Together they pin the threshold from both sides and at the off-by-one boundary, while the
        /// genuinely co-targeted compounds still get their data.
        /// </summary>
        private static void AssertTransitionAwareMatching(SrmDocument importedDoc)
        {
            var sharedQ1 = new SignedMz(331.226771);

            bool TranHasData(TransitionDocNode t) =>
                t.HasResults && t.Results.Count > 0 && !t.Results[0].IsEmpty;
            bool GroupHasData(TransitionGroupDocNode g) => g.Transitions.Any(TranHasData);

            void AssertPhantom(string name, bool expectData)
            {
                var pair = importedDoc.MoleculePrecursorPairs
                    .FirstOrDefault(p => p.NodePep.ModifiedTarget.ToString().Contains(name));
                Assert.IsNotNull(pair,
                    "Test setup: injected compound '" + name + "' is missing from the imported document");
                Assert.AreEqual(expectData, GroupHasData(pair.NodeGroup),
                    "Compound '" + name + "' should " + (expectData ? "have" : "not have") +
                    " chromatogram data under at-least-half (matched*2 >= total) transition matching.");
            }

            // Sanity: the real co-targets at this Q1 still import (confirms the shared 97.1 / 81.05
            // channels are genuinely present, so a no-data result is from the matching rule, not an
            // empty Q1).
            var realPairs = importedDoc.MoleculePrecursorPairs
                .Where(p => Math.Abs(p.NodeGroup.PrecursorMz - sharedQ1) < 0.001 &&
                            !p.NodePep.ModifiedTarget.ToString().Contains(PHANTOM_MARKER))
                .ToList();
            Assert.AreNotEqual(0, realPairs.Count,
                "Test setup: expected the real DOC / 17α-OH-P co-targets at the shared Q1");
            foreach (var real in realPairs)
                Assert.IsTrue(GroupHasData(real.NodeGroup),
                    real.NodePep.ModifiedTarget + " (a real co-target) unexpectedly lost its data");

            // The 2-of-3 majority phantom counts on 81.05 being a measured channel here; assert
            // that explicitly via the real co-target (DOC) that owns it, so a future data-file
            // regeneration that dropped 81.05 fails loudly rather than as a puzzling Majority miss.
            // Use the document's own product-match tolerance, the same one PeptideFinder applies.
            double mzMatchTol = importedDoc.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            var sharedQ3 = new SignedMz(81.05);
            bool q3Measured = realPairs
                .SelectMany(p => p.NodeGroup.Transitions)
                .Any(t => Math.Abs(t.Mz - sharedQ3) <= mzMatchTol && TranHasData(t));
            Assert.IsTrue(q3Measured,
                "Test setup: expected the shared 81.05 product channel to be measured in this file");

            // A minority compound must be denied this Q1's signal; half-or-more must receive it.
            // The half case pins the ">=" boundary (a tightening to ">" would deny it data and
            // trip this).
            AssertPhantom(PHANTOM_MINORITY, false);
            AssertPhantom(PHANTOM_HALF, true);
            AssertPhantom(PHANTOM_MAJORITY, true);
        }
    }
}
