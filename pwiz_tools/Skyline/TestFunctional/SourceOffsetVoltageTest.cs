/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.SkylineTestUtil;
using System.Globalization;
using System.Linq;
using pwiz.Skyline.Model.Hibernate;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SourceOffsetVoltageTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestSourceOffsetVoltage()
        {
            TestFilesZip = @"TestFunctional\SourceOffsetVoltageTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("SourceOffsetVoltageTest.sky"));
                SkylineWindow.SetTransformChrom(TransformChrom.raw);
            });
            WaitForDocumentLoaded();
            Assert.AreEqual(1, SkylineWindow.Document.MoleculeTransitionGroupCount);

            // Define three spectrum filters for SourceOffsetVoltage blank, 10, and 20
            RunDlg<EditSpectrumFilterDlg>(SkylineWindow.EditMenu.EditSpectrumFilter, dlg =>
            {
                dlg.CreateCopy = true;
                var row = dlg.RowBindingList.AddNew();
                Assert.IsNotNull(row);
                row.SetProperty(SpectrumClassColumn.SourceOffsetVoltage);
                row.SetOperation(FilterOperations.OP_IS_BLANK);
                dlg.OkDialog();
            });
            RunDlg<EditSpectrumFilterDlg>(SkylineWindow.EditMenu.EditSpectrumFilter, dlg =>
            {
                Assert.IsTrue(dlg.CreateCopy);
                var row = dlg.RowBindingList.AddNew();
                Assert.IsNotNull(row);
                row.SetProperty(SpectrumClassColumn.SourceOffsetVoltage);
                row.SetOperation(FilterOperations.OP_EQUALS);
                row.SetValue(10.0);
                dlg.OkDialog();
            });
            RunDlg<EditSpectrumFilterDlg>(SkylineWindow.EditMenu.EditSpectrumFilter, dlg =>
            {
                Assert.IsTrue(dlg.CreateCopy);
                var row = dlg.RowBindingList.AddNew();
                Assert.IsNotNull(row);
                row.SetProperty(SpectrumClassColumn.SourceOffsetVoltage);
                row.SetOperation(FilterOperations.OP_EQUALS);
                row.SetValue(20.0);
                dlg.OkDialog();
            });
            // There should be 4 precursors: no filter, voltage=blank, voltage=10 and voltage=20
            Assert.AreEqual(4, SkylineWindow.Document.MoleculeTransitionGroupCount);
            ImportResultsFile(TestFilesDir.GetTestPath("source_cid_test.raw"));

            // Verify that the unfiltered chromatogram has the same number of points as the sum of
            // the points in each of the filtered chromatograms
            var document = SkylineWindow.Document;
            var peptideGroupDocNode = document.MoleculeGroups.First();
            var peptideDocNode = peptideGroupDocNode.Molecules.First();
            var tolerance = (float) document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            int? fullPointCount = null;
            int totalFilteredPointCount = 0;
            foreach (var transitionGroupDocNode in peptideDocNode.TransitionGroups)
            {
                double? voltage = null;
                Assert.IsTrue(document.MeasuredResults.TryLoadChromatogram(0, peptideDocNode, transitionGroupDocNode, tolerance, out var infos));
                Assert.AreEqual(1, infos.Length);
                var chromatogramGroupInfo = infos[0];
                var firstChromatogram = chromatogramGroupInfo.GetTransitionInfo(0, TransformChrom.raw);
                int pointCount = firstChromatogram.RawTimes.Count;
                Assert.AreNotEqual(0, pointCount);
                if (transitionGroupDocNode.SpectrumClassFilter.IsEmpty)
                {
                    Assert.IsNull(fullPointCount);
                    fullPointCount = pointCount;
                }
                else
                {
                    totalFilteredPointCount += pointCount;
                    var strVoltage = transitionGroupDocNode.SpectrumClassFilter.Clauses.First().FilterSpecs.First()
                        .Predicate.InvariantOperandText;
                    if (!string.IsNullOrEmpty(strVoltage))
                    {
                        voltage = double.Parse(strVoltage, CultureInfo.InvariantCulture);
                    }
                }

                var identityPath = new IdentityPath(peptideGroupDocNode.PeptideGroup, peptideDocNode.Peptide,
                    transitionGroupDocNode.TransitionGroup);
                RunUI(() =>
                {
                    SkylineWindow.SelectedPath = identityPath;
                });
                WaitForGraphs();

                var timeIntensities = firstChromatogram.TimeIntensities;
                ClickChromatogram(timeIntensities.Times[0], timeIntensities.Intensities[0]);
                var graphFullScan = WaitForOpenForm<GraphFullScan>();
                RunUI(()=>graphFullScan.ShowPropertiesSheet = true);
                if (!transitionGroupDocNode.SpectrumClassFilter.IsEmpty)
                {
                    var fullScanProperties = graphFullScan.MsGraphExtension.PropertiesSheet.SelectedObject as FullScanProperties;
                    Assert.IsNotNull(fullScanProperties);
                    if (voltage.HasValue)
                    {
                        Assert.AreEqual(voltage.Value.ToString(Formats.OPT_PARAMETER, CultureInfo.CurrentCulture), fullScanProperties.SourceOffsetVoltage);
                    }
                    else
                    {
                        Assert.IsNull(fullScanProperties.SourceOffsetVoltage);
                    }
                }
            }
            Assert.IsNotNull(fullPointCount);
            Assert.AreEqual(fullPointCount.Value, totalFilteredPointCount);
        }

    }
}
