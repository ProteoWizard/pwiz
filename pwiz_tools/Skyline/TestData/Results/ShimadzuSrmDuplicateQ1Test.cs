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

            string docPath = TestFilesDir.GetTestPath("Wesley.sky");
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

                // Wesley's second issue from the same thread: precursors that
                // share a Q1 m/z (DOC and 17α-OH-P both at 331.227 here) get
                // collapsed by the chromatogram-to-target binding so only one
                // retains data. For every same-Q1 group where at least one
                // member got data, every other member must also have data.
                var importedDoc = docContainer.Document;
                var collisionGroups = importedDoc.MoleculePrecursorPairs
                    .GroupBy(p => p.NodeGroup.PrecursorMz)
                    .Where(g => g.Count() > 1)
                    .ToList();
                Assert.AreNotEqual(0, collisionGroups.Count,
                    "Test setup: expected the document to contain at least one duplicate-Q1 pair");
                var failures = new List<string>();
                foreach (var collisionGroup in collisionGroups)
                {
                    var members = collisionGroup.ToList();
                    // HasResults only checks Results != null, not Count, so guard
                    // the index access explicitly before reading Results[0].
                    bool HasFirstResult(TransitionGroupDocNode tg) =>
                        tg.HasResults && tg.Results.Count > 0;
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
        }

        // Name of the synthetic "incidental neighbor" injected by the test below.
        private const string INCIDENTAL_NEIGHBOR = "IncidentalQ1Neighbor_PHANTOM";

        /// <summary>
        /// A &lt;molecule&gt; sharing DOC / 17α-OH-P's Q1 (331.226771) but whose product ions are
        /// {97.1, 200, 250}: only 97.1 is among the channels this file actually measures at that Q1
        /// (the union {121, 109.05, 97.1, 81.05}), so it matches just 1 of its 3 transitions — a
        /// minority. It stands in for a compound targeted by a *different* acquisition method that
        /// merely collides on Q1. Mirrors the DOC block (same neutral/ion formula, so the same Q1)
        /// with a distinct name (distinct identity) and a deliberately mismatched product set.
        /// </summary>
        private const string INCIDENTAL_NEIGHBOR_MOLECULE_XML =
            "    <molecule explicit_retention_time=\"5.4\" auto_manage_children=\"false\" neutral_formula=\"C21H30O3\" neutral_mass_average=\"330.46425\" neutral_mass_monoisotopic=\"330.219495\" custom_ion_name=\"" + INCIDENTAL_NEIGHBOR + "\">\r\n" +
            "      <precursor charge=\"1\" precursor_mz=\"331.226771\" explicit_collision_energy=\"45\" auto_manage_children=\"false\" collision_energy=\"0\" ion_formula=\"C21H30O3[M+H]\" neutral_mass_average=\"330.46425\" neutral_mass_monoisotopic=\"330.219495\" custom_ion_name=\"" + INCIDENTAL_NEIGHBOR + "\">\r\n" +
            "        <transition fragment_type=\"custom\" ion_formula=\"[M+]\" neutral_mass_average=\"97.100548579909457\" neutral_mass_monoisotopic=\"97.100548579909457\" product_charge=\"1\">\r\n" +
            "          <precursor_mz>331.226771</precursor_mz>\r\n" +
            "          <product_mz>97.1</product_mz>\r\n" +
            "          <collision_energy>0</collision_energy>\r\n" +
            "        </transition>\r\n" +
            "        <transition fragment_type=\"custom\" ion_formula=\"[M+]\" neutral_mass_average=\"200.00054857990946\" neutral_mass_monoisotopic=\"200.00054857990946\" product_charge=\"1\">\r\n" +
            "          <precursor_mz>331.226771</precursor_mz>\r\n" +
            "          <product_mz>200</product_mz>\r\n" +
            "          <collision_energy>0</collision_energy>\r\n" +
            "        </transition>\r\n" +
            "        <transition fragment_type=\"custom\" ion_formula=\"[M+]\" neutral_mass_average=\"250.00054857990946\" neutral_mass_monoisotopic=\"250.00054857990946\" product_charge=\"1\">\r\n" +
            "          <precursor_mz>331.226771</precursor_mz>\r\n" +
            "          <product_mz>250</product_mz>\r\n" +
            "          <collision_energy>0</collision_energy>\r\n" +
            "        </transition>\r\n" +
            "      </precursor>\r\n" +
            "    </molecule>\r\n";

        /// <summary>
        /// Deciding which compound(s) a shared-Q1 SRM spectrum belongs to must use the product ions,
        /// not the precursor m/z alone. A compound that shares a Q1 with a real target but matches
        /// only a minority of its own transitions against the channels the file actually measured at
        /// that Q1 is an incidental collision — e.g. a compound targeted by a different acquisition
        /// method — and must not be handed that Q1's chromatogram.
        ///
        /// This injects such an incidental neighbor at DOC / 17α-OH-P's Q1 (331.227): it matches only
        /// 1 of its 3 transitions against the measured channels, so it must import with no data, while
        /// the genuinely co-targeted compounds at that Q1 still get theirs. Same support thread as
        /// <see cref="ShimadzuSrmDuplicateQ1ImportTest"/>.
        /// </summary>
        [TestMethod]
        public void ShimadzuSrmIncidentalQ1NeighborTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            // Build a variant document that adds the incidental neighbor at the shared Q1.
            string srcDocPath = TestFilesDir.GetTestPath("Wesley.sky");
            string docText = File.ReadAllText(srcDocPath);
            int insertAt = docText.IndexOf("</peptide_list>");
            Assert.AreNotEqual(-1, insertAt,
                "Test setup: could not find a peptide_list to inject the incidental neighbor into");
            docText = docText.Substring(0, insertAt) + INCIDENTAL_NEIGHBOR_MOLECULE_XML + docText.Substring(insertAt);
            string docPath = TestFilesDir.GetTestPath("WesleyIncidental.sky");
            File.WriteAllText(docPath, docText);

            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);

            using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
            {
                const string replicateName = "WesleyLCD";
                string extRaw = ExtensionTestContext.ExtShimadzuRaw;
                string dataPath = TestFilesDir.GetTestPath("Labsolutions_PackA_MA_alt" + extRaw);
                var chromSets = new[]
                {
                    new ChromatogramSet(replicateName, new[]
                        { new MsDataFilePath(dataPath) }),
                };
                var docResults = doc.ChangeMeasuredResults(new MeasuredResults(chromSets));
                Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
                docContainer.AssertComplete();

                var importedDoc = docContainer.Document;
                var sharedQ1 = new SignedMz(331.226771);

                bool TranHasData(TransitionDocNode t) =>
                    t.HasResults && t.Results.Count > 0 && !t.Results[0].IsEmpty;
                bool GroupHasData(TransitionGroupDocNode g) => g.Transitions.Any(TranHasData);

                var phantomPair = importedDoc.MoleculePrecursorPairs
                    .FirstOrDefault(p => p.NodePep.ModifiedTarget.ToString().Contains(INCIDENTAL_NEIGHBOR));
                Assert.IsNotNull(phantomPair,
                    "Test setup: the injected incidental neighbor is missing from the imported document");

                // Sanity: the two real co-targets at this Q1 still import (confirms the 97.1 channel
                // the neighbor would steal is genuinely present in the file).
                var realPairs = importedDoc.MoleculePrecursorPairs
                    .Where(p => Math.Abs(p.NodeGroup.PrecursorMz - sharedQ1) < 0.001 &&
                                !p.NodePep.ModifiedTarget.ToString().Contains(INCIDENTAL_NEIGHBOR))
                    .ToList();
                Assert.AreNotEqual(0, realPairs.Count,
                    "Test setup: expected the real DOC / 17α-OH-P co-targets at the shared Q1");
                foreach (var real in realPairs)
                    Assert.IsTrue(GroupHasData(real.NodeGroup),
                        real.NodePep.ModifiedTarget + " (a real co-target) unexpectedly lost its data");

                // An incidental Q1 neighbor matching only a minority of its transitions must not be
                // assigned this Q1's signal — otherwise it steals the shared 97.1 chromatogram from
                // the real co-targets.
                Assert.IsFalse(GroupHasData(phantomPair.NodeGroup),
                    "An incidental Q1 neighbor (sharing only 1 of its 3 transitions with the measured " +
                    "channels) must not be assigned this Q1's chromatogram, but it received data.");
            }
        }
    }
}
