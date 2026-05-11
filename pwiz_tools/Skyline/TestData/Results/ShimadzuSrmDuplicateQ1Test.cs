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

using System.Collections.Generic;
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
                    bool anyHasData = members.Any(p => p.NodeGroup.HasResults
                                                       && !p.NodeGroup.Results[0].IsEmpty);
                    if (!anyHasData)
                        continue; // No data for this Q1 in the file; not a collapse.

                    // Group-level: every same-Q1 peptide must have at least one
                    // chromatogram (covers "same Q1, different Q3" — the user-
                    // visible "compound is missing" symptom in the support thread).
                    foreach (var pair in members)
                    {
                        var name = pair.NodePep.ModifiedTarget.ToString();
                        if (!pair.NodeGroup.HasResults)
                            failures.Add(string.Format("Q1 {0} ({1}): no Results object",
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
                            if (!nodeTran.HasResults || nodeTran.Results[0].IsEmpty)
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
                    // ai/todos/backlog/TODO-peak_picker_honor_explicit_rt.md.
                }
                Assert.AreEqual(0, failures.Count,
                    "Same-Q1 compounds dropped during import:\n  " +
                    string.Join("\n  ", failures));
            }
        }
    }
}
