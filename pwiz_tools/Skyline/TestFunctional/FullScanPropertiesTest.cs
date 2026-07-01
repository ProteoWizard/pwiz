/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class FullScanPropertiesTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestFullScanProperties()
        {
            TestFilesZip = @"TestFunctional\FullScanPropertiesTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("NeutralLoss.sky")));
            ImportResultsFile(TestFilesDir.GetTestPath("S_3.mzML"));
            RunUI(()=>SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Molecules, 0));
            WaitForGraphs();
            RunUI(()=>
            {
                SkylineWindow.ShowSplitChromatogramGraph(true);
                SkylineWindow.SetTransformChrom(TransformChrom.interpolated);
                SkylineWindow.ShowChromatogramLegends(false);
            });
            WaitForGraphs();
            ClickChromatogram(31.1123047521535, 43338.2577592845, PaneKey.PRODUCTS);
            var graphFullScan = WaitForOpenForm<GraphFullScan>();
            RunUI(() => graphFullScan.ShowPropertiesSheet = true);
            WaitForGraphs();
            VerifyRawMetadata(graphFullScan);
            VerifyExpansionPersistsAcrossScans(graphFullScan);
            RunUI(() => graphFullScan.ShowPropertiesSheet = false);
        }

        /// <summary>
        /// The property sidebar should keep an expandable node (here the Raw Metadata "Parameters"
        /// node) expanded when the user steps to an adjacent scan, rather than collapsing it each time.
        /// </summary>
        private void VerifyExpansionPersistsAcrossScans(GraphFullScan graphFullScan)
        {
            RunUI(() =>
            {
                var node = FindParametersNode(graphFullScan);
                Assert.IsNotNull(node, @"Raw Metadata parameters node not found");
                Assert.IsFalse(node.Expanded, @"Raw Metadata parameters node was expected to start collapsed");
                node.Expanded = true;
            });
            RunUI(() => graphFullScan.ChangeScan(1));
            WaitForGraphs();
            RunUI(() =>
            {
                var node = FindParametersNode(graphFullScan);
                Assert.IsNotNull(node, @"Raw Metadata parameters node missing after navigating to the next scan");
                Assert.IsTrue(node.Expanded, @"Raw Metadata node collapsed after navigating to the next scan");
            });
        }

        private static GridItem FindParametersNode(GraphFullScan graphFullScan)
        {
            var root = graphFullScan.MsGraphExtension.PropertiesSheet.SelectedGridItem;
            if (root == null)
            {
                return null;
            }
            while (root.Parent != null)
            {
                root = root.Parent;
            }
            return EnumerateGridItems(root).FirstOrDefault(item =>
                item.PropertyDescriptor != null && item.PropertyDescriptor.Name == nameof(FullScanProperties.RawMetadata));
        }

        private static IEnumerable<GridItem> EnumerateGridItems(GridItem parent)
        {
            foreach (GridItem child in parent.GridItems)
            {
                yield return child;
                foreach (var descendant in EnumerateGridItems(child))
                {
                    yield return descendant;
                }
            }
        }

        /// <summary>
        /// The full-scan viewer surfaces mzML CV/user parameters that Skyline does not interpret
        /// into its own fields. S_3.mzML carries several at the spectrum/scan level. Verify both the
        /// model (which terms were captured, keyed by translation-stable CV accession) and the way
        /// they render in the property grid: their own "Raw Metadata" category, one selectable row
        /// per term, each carrying the ontology definition as help text.
        /// </summary>
        private void VerifyRawMetadata(GraphFullScan graphFullScan)
        {
            RunUI(() =>
            {
                var spectrumProperties = graphFullScan.MsGraphExtension.PropertiesSheet.SelectedObject as FullScanProperties;
                Assert.IsNotNull(spectrumProperties);
                Assert.IsNotNull(spectrumProperties.RawMetadata);
                var terms = spectrumProperties.RawMetadata.Terms;
                var rawAccessions = terms.Select(term => term.Accession).ToList();
                CollectionAssert.Contains(rawAccessions, @"MS:1000505"); // base peak intensity
                CollectionAssert.Contains(rawAccessions, @"MS:1000512"); // filter string
                // Interpreted terms must NOT be duplicated into the raw bag.
                CollectionAssert.DoesNotContain(rawAccessions, @"MS:1000285"); // total ion current
                CollectionAssert.DoesNotContain(rawAccessions, @"MS:1000511"); // ms level

                // The captured term carries its CV definition, and that definition becomes each grid
                // row's help text (surfaced through the child property descriptors).
                var basePeakTerm = terms.First(term => term.Accession == @"MS:1000505");
                Assert.IsFalse(string.IsNullOrEmpty(basePeakTerm.Definition), @"CV definition was not captured");
                var basePeakDescriptor = spectrumProperties.RawMetadata.GetProperties().Cast<PropertyDescriptor>()
                    .First(descriptor => descriptor.Name.EndsWith(@"MS:1000505"));
                Assert.AreEqual(basePeakTerm.Definition, basePeakDescriptor.Description, @"grid row help text does not match the CV definition");
            });
        }
    }
}
