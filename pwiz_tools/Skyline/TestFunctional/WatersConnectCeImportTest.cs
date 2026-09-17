/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using pwiz.CommonMsData;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Imports CE optimization data acquired through waters_connect two ways: with the actual collision energy
    /// on each channel and the real product m/z (what waters_connect methods produce now), and with the product
    /// m/z stepped per optimization step (what they produced before). Both must give a full set of optimization
    /// steps with the same peak areas.
    ///
    /// Both files are the same acquisition, converted from the waters_connect dev server. In the actual-CE file
    /// each series of channels was given its center product m/z, which is what a method exported without the
    /// product m/z shift acquires.
    /// </summary>
    [TestClass]
    public class WatersConnectCeImportTest : AbstractFunctionalTest
    {
        private const int STEP_COUNT = 5;       // The document's CE regression: 11 steps per transition
        private const int TRANSITION_COUNT = 11;

        [TestMethod]
        public void TestWatersConnectCeImport()
        {
            TestFilesZip = @"TestFunctional\WatersConnectCeImportTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("6Mix.sky")));
            WaitForDocumentLoaded();

            ImportOptimizationReplicate("ActualCE", "6Mix-CEsteps-actual-ce.mzML");
            ImportOptimizationReplicate("ShiftedMz", "6Mix-CEsteps-shifted.mzML");

            var document = SkylineWindow.Document;
            Assert.AreEqual(2, document.Settings.MeasuredResults.Chromatograms.Count);
            var areasActualCe = GetStepAreas(document, 0);
            var areasShiftedMz = GetStepAreas(document, 1);
            Assert.AreEqual(TRANSITION_COUNT, areasActualCe.Count);
            AssertEx.AreEqualDeep(areasShiftedMz.Keys.ToList(), areasActualCe.Keys.ToList());

            foreach (var transition in areasActualCe)
            {
                // Reading the collision energies gives every step of the series, at the transition's own product m/z
                var steps = transition.Value;
                Assert.AreEqual(STEP_COUNT * 2 + 1, steps.Count, "Incorrect number of optimization steps for {0}", transition.Key);
                for (int step = -STEP_COUNT; step <= STEP_COUNT; step++)
                    Assert.IsTrue(steps.ContainsKey(step), "Missing optimization step {0} for {1}", step, transition.Key);

                // and the same areas as the same data acquired with the product m/z stepped per step
                var stepsShiftedMz = areasShiftedMz[transition.Key];
                foreach (var step in steps)
                    AssertEx.AreEqual(stepsShiftedMz[step.Key], step.Value, step.Value * 1e-5, $@"{transition.Key} step {step.Key}");
            }
        }

        private void ImportOptimizationReplicate(string replicateName, string dataFileName)
        {
            RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResults =>
            {
                importResults.OptimizationName = ExportOptimize.CE;
                importResults.NamedPathSets = new[]
                {
                    new KeyValuePair<string, MsDataFileUri[]>(replicateName,
                        new MsDataFileUri[] { new MsDataFilePath(TestFilesDir.GetTestPath(dataFileName)) })
                };
                importResults.OkDialog();
            });
            WaitForDocumentLoaded();
        }

        /// <summary>
        /// Returns the peak area of every optimization step of every transition in one replicate, keyed by
        /// the transition's precursor and product m/z and then by optimization step.
        /// </summary>
        private static Dictionary<string, Dictionary<int, float>> GetStepAreas(SrmDocument document, int replicateIndex)
        {
            var areas = new Dictionary<string, Dictionary<int, float>>();
            foreach (var nodeGroup in document.MoleculeTransitionGroups)
            {
                foreach (var nodeTran in nodeGroup.Transitions)
                {
                    var key = string.Format(@"{0:F04} -> {1:F04}", nodeGroup.PrecursorMz, nodeTran.Mz);
                    var steps = new Dictionary<int, float>();
                    foreach (var chromInfo in nodeTran.Results[replicateIndex])
                        steps.Add(chromInfo.OptimizationStep, chromInfo.Area);
                    areas.Add(key, steps);
                }
            }
            return areas;
        }
    }
}
