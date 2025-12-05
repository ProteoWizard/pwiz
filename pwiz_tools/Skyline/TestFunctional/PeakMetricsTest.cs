/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.CommonMsData;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PeakMetricsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPeakMetrics()
        {
            TestFilesZip = @"TestFunctional\PeakMetricsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("PeakMetrics.sky"));
                SkylineWindow.ShowDocumentGrid(true);
            });
            var dataFilePath = TestFilesDir.GetTestPath("peakMetrics.mzML");
            ImportResultsFile(dataFilePath);
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunDlg<ViewEditor>(documentGrid.NavBar.CustomizeView, viewEditor =>
            {
                viewEditor.ViewName = "PeakMetrics";
                viewEditor.ChooseColumnsTab.RemoveColumns(0, viewEditor.ChooseColumnsTab.ColumnCount);
                var ppPrecursorResult = PropertyPath.Root.Property(nameof(SkylineDocument.Proteins)).LookupAllItems()
                    .Property(nameof(Protein.Peptides)).LookupAllItems().Property(nameof(Peptide.Precursors))
                    .LookupAllItems().Property(nameof(Precursor.Results)).DictionaryValues();
                var ppMs1 = ppPrecursorResult.Property(nameof(PrecursorResult.LcPeakIonMetricsMS1));
                var ppMs2 = ppPrecursorResult.Property(nameof(PrecursorResult.LcPeakIonMetricsFragment));
                foreach (var ppMetrics in new[] { ppMs1, ppMs2 })
                {
                    Assert.IsTrue(viewEditor.ChooseColumnsTab.TrySelect(ppMetrics), "Unable to select {0}", ppMetrics);
                    viewEditor.ChooseColumnsTab.AddSelectedColumn();
                    foreach (var name in new[]
                             {
                                 nameof(LcPeakIonMetrics.ApexSpectrumId),
                                 nameof(LcPeakIonMetrics.ApexAnalyteIonCount),
                                 nameof(LcPeakIonMetrics.ApexTotalIonCount),
                                 nameof(LcPeakIonMetrics.LcPeakAnalyteIonCount),
                                 nameof(LcPeakIonMetrics.LcPeakTotalIonCount),
                                 nameof(LcPeakIonMetrics.LcPeakTotalIonCurrentArea),
                             })
                    {
                        var propertyPath = ppMetrics.Property(name);
                        Assert.IsTrue(viewEditor.ChooseColumnsTab.TrySelect(propertyPath), "Unable to select {0}",
                            propertyPath);
                        viewEditor.ChooseColumnsTab.AddSelectedColumn();
                    }
                }

                viewEditor.OkDialog();
            });
            WaitForConditionUI(() => documentGrid.IsComplete);
            RunUI(() =>
            {
                Assert.AreEqual(1, documentGrid.RowCount);
                var ppPrecursorResult = PropertyPath.Root.Property(nameof(Precursor.Results)).DictionaryValues();
                var ppMs1 = ppPrecursorResult.Property(nameof(PrecursorResult.LcPeakIonMetricsMS1));
                var colMs1 = FindColumn(documentGrid, ppMs1);
                var ms1Metrics = (LcPeakIonMetrics)documentGrid.DataGridView.Rows[0].Cells[colMs1.Index].Value;
                Assert.IsNotNull(ms1Metrics);
                Assert.IsNotNull(ms1Metrics.ApexSpectrumId);

                using var msDataFile = new MsDataFilePath(dataFilePath).OpenMsDataFile(new OpenMsDataFileParams());
                var apexSpectrumIndexMs1 = msDataFile.GetSpectrumIndex(ms1Metrics.ApexSpectrumId);
                AssertEx.IsGreaterThanOrEqual(apexSpectrumIndexMs1, 0);
                var apexSpectrumMs1 = msDataFile.GetSpectrum(apexSpectrumIndexMs1);
                Assert.AreEqual(1, apexSpectrumMs1.Level);
                AssertApexMetrics(ms1Metrics, apexSpectrumMs1);

                var ppMs2 = ppPrecursorResult.Property(nameof(PrecursorResult.LcPeakIonMetricsFragment));
                var colMs2 = FindColumn(documentGrid, ppMs2);
                var ms2Metrics = (LcPeakIonMetrics)documentGrid.DataGridView.Rows[0].Cells[colMs2.Index].Value;
                Assert.IsNotNull(ms2Metrics);
                Assert.IsNotNull(ms2Metrics.ApexSpectrumId);
                var apexSpectrumIndexMs2 = msDataFile.GetSpectrumIndex(ms2Metrics.ApexSpectrumId);
                AssertEx.IsGreaterThanOrEqual(apexSpectrumIndexMs2, 0);
                var apexSpectrumMs2 = msDataFile.GetSpectrum(apexSpectrumIndexMs2);
                Assert.AreEqual(2, apexSpectrumMs2.Level);
                AssertApexMetrics(ms2Metrics, apexSpectrumMs2);
            });

            RunDlg<ViewEditor>(documentGrid.NavBar.CustomizeView, viewEditor =>
            {
                var ppTransition = PropertyPath.Root.Property(nameof(SkylineDocument.Proteins)).LookupAllItems()
                    .Property(nameof(Protein.Peptides)).LookupAllItems().Property(nameof(Peptide.Precursors))
                    .LookupAllItems().Property(nameof(Precursor.Transitions)).LookupAllItems();
                var ppTransitionIonMetrics = ppTransition.Property(nameof(Transition.Results)).DictionaryValues().Property(nameof(TransitionResult.TransitionIonMetrics));
                foreach (var ppColumn in new[]
                         {
                             ppTransition,
                             ppTransitionIonMetrics.Property(nameof(TransitionIonMetrics.ApexTransitionIonCount)),
                             ppTransitionIonMetrics.Property(nameof(TransitionIonMetrics.LcPeakTransitionIonCount))
                         })
                {
                    Assert.IsTrue(viewEditor.ChooseColumnsTab.TrySelect(ppColumn), "Unable to select {0}", ppColumn);
                    viewEditor.ChooseColumnsTab.AddSelectedColumn();
                }
                viewEditor.OkDialog();
            });
            WaitForConditionUI(() => documentGrid.IsComplete);
            RunUI(() =>
            {
                var colTransition = FindColumn(documentGrid, PropertyPath.Root);
                var ppTransitionResult = PropertyPath.Root.Property(nameof(Transition.Results)).DictionaryValues();
                var ppPrecursorResult = ppTransitionResult.Property(nameof(TransitionResult.PrecursorResult));
                var ppTransitionMetrics = ppTransitionResult.Property(nameof(TransitionResult.TransitionIonMetrics));
                var colTransitionApex = FindColumn(documentGrid,
                    ppTransitionMetrics.Property(nameof(TransitionIonMetrics.ApexTransitionIonCount)));
                var colTransitionLcPeak = FindColumn(documentGrid,
                    ppTransitionMetrics.Property(nameof(TransitionIonMetrics.LcPeakTransitionIonCount)));

                foreach (bool fragment in new []{false, true})
                {
                    var transitionApexValues = new List<double>();
                    var transitionLcPeakValues = new List<double>();
                    var apexValues = new List<double>();
                    var lcPeakValues = new List<double>();

                    var ppLcPeakMetrics = ppPrecursorResult.Property(fragment
                        ? nameof(PrecursorResult.LcPeakIonMetricsFragment)
                        : nameof(PrecursorResult.LcPeakIonMetricsMS1));
                    var colApex = FindColumn(documentGrid,
                        ppLcPeakMetrics.Property(nameof(LcPeakIonMetrics.ApexAnalyteIonCount)));
                    var colLcPeak = FindColumn(documentGrid,
                        ppLcPeakMetrics.Property(nameof(LcPeakIonMetrics.LcPeakAnalyteIonCount)));

                    foreach (DataGridViewRow row in documentGrid.DataGridView.Rows)
                    {
                        var transitionValue = row.Cells[colTransition.Index].Value;
                        Assert.IsInstanceOfType(transitionValue, typeof(Transition));
                        var transition = (Transition)transitionValue;
                        if (fragment == transition.DocNode.IsMs1)
                        {
                            continue;
                        }
                        transitionApexValues.Add(GetDouble(row, colTransitionApex));
                        transitionLcPeakValues.Add(GetDouble(row, colTransitionLcPeak));
                        apexValues.Add(GetDouble(row, colApex));
                        lcPeakValues.Add(GetDouble(row, colLcPeak));
                    }

                    var lcPeakValue = lcPeakValues.Distinct().Single();
                    var sumLcPeak = transitionLcPeakValues.Sum();
                    AssertEx.AreEqual(lcPeakValue, sumLcPeak, .01);

                    // The individual transition metrics were calculated at the apex for that transition.
                    // The sum of them will be greater than the value that was calculated from looking at
                    // the apex of the sum of the transitions
                    var apexValue = apexValues.Distinct().Single();
                    var sumApex = transitionApexValues.Sum();
                    AssertEx.IsGreaterThanOrEqual(sumApex, apexValue);
                }
            });
        }

        private void AssertApexMetrics(LcPeakIonMetrics peakIonMetrics, MsDataSpectrum spectrum)
        {
            Assert.IsNotNull(peakIonMetrics.ApexTotalIonCount);
            var metadata = spectrum.Metadata;
            Assert.IsNotNull(metadata.TotalIonCurrent);
            Assert.IsNotNull(metadata.InjectionTime);
            var expectedTotalIonCount = (metadata.TotalIonCurrent * metadata.InjectionTime / 1000).Value;
            Assert.AreEqual(expectedTotalIonCount, peakIonMetrics.ApexTotalIonCount.Value, .0001);
        }

        private DataGridViewColumn FindColumn(DataboundGridForm grid, PropertyPath propertyPath)
        {
            var column = grid.FindColumn(propertyPath);
            Assert.IsNotNull(column, "Unable to find column {0}", propertyPath);
            return column;
        }

        private double GetDouble(DataGridViewRow row, DataGridViewColumn column)
        {
            var value = row.Cells[column.Index].Value;
            Assert.IsInstanceOfType(value, typeof(double));
            return (double) value;
        }
    }
}
