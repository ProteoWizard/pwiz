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
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Spectra;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class FullScanPropertiesTest : AbstractFunctionalTestEx
    {
        // Unit CV accessions used to build the terms in VerifyUnitFormatting
        private const string MZ = "MS:1000040";
        private const string DETECTOR_COUNTS = "MS:1000131";
        private const string INTENSITY = "MS:1000043";
        private const string PPM = "UO:0000169";
        private const string SECOND = "UO:0000010";
        private const string HERTZ = "UO:0000106";

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
            RunUI(() =>
            {
                graphFullScan.ShowPropertiesSheet = true;
                graphFullScan.SetShowAnnotations(true);
                graphFullScan.SetShowAnnotations(false);
            });
            WaitForGraphs();
            VerifyOtherMetadata(graphFullScan);
            VerifyExpansionPersistsAcrossScans(graphFullScan);
            RunUI(() => graphFullScan.ShowPropertiesSheet = false);
            VerifyUnitFormatting();
        }

        /// <summary>
        /// The formatting rules applied to an "Other Metadata" value, exercised with terms built by
        /// hand because the cases that matter cannot all be produced from the test data file: a value
        /// in a unit Skyline has a display convention for takes that convention, and everything else -
        /// including a value the convention would round away to nothing - is shown as the file wrote it.
        /// </summary>
        private void VerifyUnitFormatting()
        {
            // A value whose unit has a Skyline convention is shown in that convention, and keeps its unit.
            AssertDisplayed(FormatCurrent(422.2507, Formats.Mz) + @" m/z",
                Term(@"base peak m/z", @"422.250671386719", @"m/z", MZ));
            AssertDisplayed(FormatCurrent(65579108, Formats.PEAK_AREA) + @" number of detector counts",
                Term(@"base peak intensity", @"6.5579108e07", @"number of detector counts", DETECTOR_COUNTS));

            // Values the convention would round away to zero are shown as the file wrote them: an
            // intensity is displayed as whole counts, so a relative intensity would otherwise read "0".
            AssertDisplayed(@"0.4213 intensity unit",
                Term(@"base peak intensity", @"0.4213", @"intensity unit", INTENSITY));
            AssertDisplayed(@"0.03 parts per million",
                Term(@"mass error", @"0.03", @"parts per million", PPM));

            // A value that really is zero still shows as zero.
            AssertDisplayed(FormatCurrent(0, Formats.PEAK_AREA) + @" number of detector counts",
                Term(@"base peak intensity", @"0", @"number of detector counts", DETECTOR_COUNTS));

            // No convention for the unit, no unit at all, or a value that is not a number: shown as written.
            AssertDisplayed(@"9.4 hertz", Term(@"resonance frequency", @"9.4", @"hertz", HERTZ));
            AssertDisplayed(@"20.5108862", Term(@"mass resolving power", @"20.5108862", null, null));
            AssertDisplayed(@"not a number m/z", Term(@"odd term", @"not a number", @"m/z", MZ));

            // The second is deliberately left unformatted: Skyline's time convention is in minutes.
            AssertDisplayed(@"0.004 second", Term(@"dwell time", @"0.004", @"second", SECOND));

            // A flag term carries no value, so its row is empty - the term name says it all.
            AssertDisplayed(string.Empty, Term(@"MS1 spectrum", string.Empty, null, null));
        }

        private static SpectrumMetadataTerm Term(string name, string value, string unit, string unitAccession)
        {
            return new SpectrumMetadataTerm(@"MS:0000000", name, value, unit, unitAccession);
        }

        private static string FormatCurrent(double value, string format)
        {
            return value.ToString(format, CultureInfo.CurrentCulture);
        }

        private static void AssertDisplayed(string expected, SpectrumMetadataTerm term)
        {
            var info = new OtherMetadataInfo(new[] { term });
            var descriptor = info.GetProperties().Cast<PropertyDescriptor>().First();
            Assert.AreEqual(expected, descriptor.GetValue(info), TextUtil.SpaceSeparate(@"term:", term.Name));
        }

        /// <summary>
        /// The property sidebar should keep an expandable node (here the Other Metadata "Parameters"
        /// node) expanded when the user steps to an adjacent scan, rather than collapsing it each time.
        /// </summary>
        private void VerifyExpansionPersistsAcrossScans(GraphFullScan graphFullScan)
        {
            RunUI(() =>
            {
                var node = FindParametersNode(graphFullScan);
                Assert.IsNotNull(node, @"Other Metadata node not found");
                Assert.AreEqual(FullScanPropertiesRes.Description_OtherMetadata, node.PropertyDescriptor?.Description,
                    @"Other Metadata node is missing its help text");
                Assert.IsFalse(node.Expanded, @"Other Metadata node was expected to start collapsed");
                node.Expanded = true;
            });
            RunUI(() => graphFullScan.ChangeScan(1));
            WaitForGraphs();
            RunUI(() =>
            {
                var node = FindParametersNode(graphFullScan);
                Assert.IsNotNull(node, @"Other Metadata parameters node missing after navigating to the next scan");
                Assert.IsTrue(node.Expanded, @"Other Metadata node collapsed after navigating to the next scan");
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
                item.PropertyDescriptor != null && item.PropertyDescriptor.Name == nameof(FullScanProperties.OtherMetadata));
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
        /// they render in the property grid: their own "Other Metadata" node, one selectable row per
        /// term, each carrying the ontology definition as help text and showing its value in
        /// Skyline's display convention for that unit.
        /// </summary>
        private void VerifyOtherMetadata(GraphFullScan graphFullScan)
        {
            RunUI(() =>
            {
                var spectrumProperties = graphFullScan.MsGraphExtension.PropertiesSheet.SelectedObject as FullScanProperties;
                Assert.IsNotNull(spectrumProperties);
                Assert.IsNotNull(spectrumProperties.OtherMetadata);
                var terms = spectrumProperties.OtherMetadata.Terms;
                var otherAccessions = terms.Select(term => term.Accession).ToList();
                CollectionAssert.Contains(otherAccessions, @"MS:1000504"); // base peak m/z
                CollectionAssert.Contains(otherAccessions, @"MS:1000505"); // base peak intensity
                CollectionAssert.Contains(otherAccessions, @"MS:1000512"); // filter string
                // Interpreted terms must NOT be duplicated into the catch-all bag.
                CollectionAssert.DoesNotContain(otherAccessions, @"MS:1000285"); // total ion current
                CollectionAssert.DoesNotContain(otherAccessions, @"MS:1000511"); // ms level

                var descriptors = spectrumProperties.OtherMetadata.GetProperties().Cast<PropertyDescriptor>().ToList();

                // The captured term carries its CV definition, and that definition becomes each grid
                // row's help text (surfaced through the child property descriptors).
                var basePeakIntensityTerm = terms.First(term => term.Accession == @"MS:1000505");
                Assert.IsFalse(string.IsNullOrEmpty(basePeakIntensityTerm.Definition), @"CV definition was not captured");
                var basePeakIntensityDescriptor = descriptors.First(descriptor => descriptor.Name.EndsWith(@"MS:1000505"));
                Assert.AreEqual(basePeakIntensityTerm.Definition, basePeakIntensityDescriptor.Description,
                    @"grid row help text does not match the CV definition");

                // A value in a unit Skyline has a convention for is displayed in that convention
                // (m/z here), not at whatever precision the file happened to write it, and it keeps
                // the unit label the file gave it.
                var basePeakMzTerm = terms.First(term => term.Accession == @"MS:1000504");
                Assert.AreEqual(@"MS:1000040", basePeakMzTerm.UnitAccession); // m/z
                var basePeakMz = double.Parse(basePeakMzTerm.Value, CultureInfo.InvariantCulture);
                var basePeakMzDescriptor = descriptors.First(descriptor => descriptor.Name.EndsWith(@"MS:1000504"));
                Assert.AreEqual(
                    TextUtil.SpaceSeparate(basePeakMz.ToString(Formats.Mz, CultureInfo.CurrentCulture), basePeakMzTerm.Unit),
                    basePeakMzDescriptor.GetValue(spectrumProperties.OtherMetadata),
                    @"m/z value is not shown in Skyline's m/z format");

                // Intensities follow the peak area convention.
                var basePeakIntensity = double.Parse(basePeakIntensityTerm.Value, CultureInfo.InvariantCulture);
                Assert.AreEqual(
                    TextUtil.SpaceSeparate(basePeakIntensity.ToString(Formats.PEAK_AREA, CultureInfo.CurrentCulture),
                        basePeakIntensityTerm.Unit),
                    basePeakIntensityDescriptor.GetValue(spectrumProperties.OtherMetadata),
                    @"intensity value is not shown in Skyline's peak area format");

                // A term in no unit we have a convention for (a text value here) is passed through as written.
                var filterStringTerm = terms.First(term => term.Accession == @"MS:1000512");
                var filterStringDescriptor = descriptors.First(descriptor => descriptor.Name.EndsWith(@"MS:1000512"));
                Assert.AreEqual(filterStringTerm.Value, filterStringDescriptor.GetValue(spectrumProperties.OtherMetadata));
            });
        }
    }
}
