/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class DocumentGridChromatogramDataTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestDocumentGridChromatogramData()
        {
            TestFilesZip = @"TestFunctional\DocumentGridChromatogramDataTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("DocumentGridChromatogramDataTest.sky"));
                SkylineWindow.ShowDocumentGrid(true);
            });
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(()=>
            {
                documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Transitions);
            });
            var viewEditor = ShowDialog<ViewEditor>(documentGrid.NavBar.CustomizeView);
            const string viewName = "ChromatogramData";
            RunUI(()=>
            {
                viewEditor.ChooseColumnsTab.RemoveColumns(0, viewEditor.ChooseColumnsTab.ColumnCount);
                var propertyPathTransitions = PropertyPath.Root
                    .Property(nameof(SkylineDocument.Proteins)).LookupAllItems()
                    .Property(nameof(Protein.Peptides)).LookupAllItems()
                    .Property(nameof(Peptide.Precursors)).LookupAllItems()
                    .Property(nameof(Precursor.Transitions)).LookupAllItems();
                viewEditor.ChooseColumnsTab.AddColumn(propertyPathTransitions);
                viewEditor.ChooseColumnsTab.AddColumn(PropertyPath.Root.Property(nameof(SkylineDocument.Replicates)).LookupAllItems());
                var propertyPathChromatogram = propertyPathTransitions
                    .Property(nameof(Transition.Results)).LookupAllItems().Property(nameof(KeyValuePair<object, object>.Value))
                    .Property(nameof(TransitionResult.Chromatogram));
                foreach (var dataType in new[] { nameof(Chromatogram.RawData), nameof(Chromatogram.InterpolatedData) })
                {
                    var propertyPathData = propertyPathChromatogram.Property(dataType);
                    viewEditor.ChooseColumnsTab.AddColumn(propertyPathData.Property(nameof(Chromatogram.Data.Times)));
                    viewEditor.ChooseColumnsTab.AddColumn(propertyPathData.Property(nameof(Chromatogram.Data.Intensities)));
                    viewEditor.ChooseColumnsTab.AddColumn(propertyPathData.Property(nameof(Chromatogram.Data.MassErrors)));
                    viewEditor.ChooseColumnsTab.AddColumn(propertyPathData.Property(nameof(Chromatogram.Data.SpectrumIds)));
                }
                viewEditor.ViewName = viewName;
            });
            OkDialog(viewEditor, viewEditor.OkDialog);
            WaitForConditionUI(() => documentGrid.IsComplete);
            VerifyDocumentGrid(documentGrid, CultureInfo.CurrentCulture);
            OkDialog(documentGrid, documentGrid.Close);
            var exportReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
            RunUI(() =>
            {
                exportReportDlg.SetUseInvariantLanguage(true);
                exportReportDlg.ReportName = viewName;
            });
            documentGrid = ShowDialog<DocumentGridForm>(exportReportDlg.ShowPreview);
            WaitForConditionUI(() => documentGrid.IsComplete);
            VerifyDocumentGrid(documentGrid, CultureInfo.InvariantCulture);
            OkDialog(documentGrid, documentGrid.Close);
            OkDialog(exportReportDlg, exportReportDlg.Close);
        }

        private void VerifyDocumentGrid(DocumentGridForm documentGridForm, CultureInfo cultureInfo)
        {
            var colTransition = documentGridForm.FindColumn(PropertyPath.Root);
            Assert.IsNotNull(colTransition);
            var pathTransitionResult = PropertyPath.Root
                .Property(nameof(Transition.Results)).LookupAllItems()
                .Property(nameof(KeyValuePair<object, object>.Value));
            var colReplicate = documentGridForm.FindColumn(pathTransitionResult
                .Property(nameof(TransitionResult.PrecursorResult))
                .Property(nameof(PrecursorResult.PeptideResult))
                .Property(nameof(PeptideResult.ResultFile))
                .Property(nameof(ResultFile.Replicate)));
            Assert.IsNotNull(colReplicate);
            var dataGridView = documentGridForm.DataGridView;
            RunUI(() =>
            {
                for (int iRow = 0; iRow < dataGridView.RowCount; iRow++)
                {
                    var row = dataGridView.Rows[iRow];
                    var transition = (Transition) row.Cells[colTransition.Index].Value;
                    var replicate = (Replicate) row.Cells[colReplicate.Index].Value;

                    ChromatogramGroupInfo[] chromatogramGroupInfos;
                    Assert.IsTrue(SkylineWindow.Document.MeasuredResults.TryLoadChromatogram(replicate.ChromatogramSet,
                        transition.Precursor.Peptide.DocNode, transition.Precursor.DocNode, 0, true,
                        out chromatogramGroupInfos));
                    Assert.AreEqual(1, chromatogramGroupInfos.Length);
                    var chromatogramGroup = chromatogramGroupInfos[0];
                    foreach (bool interpolated in new[] {true, false})
                    {
                        var pathData = pathTransitionResult.Property(nameof(TransitionResult.Chromatogram))
                            .Property(interpolated
                                ? nameof(Chromatogram.InterpolatedData)
                                : nameof(Chromatogram.RawData));
                        var colTimes = documentGridForm.FindColumn(pathData.Property(nameof(Chromatogram.Data.Times)));
                        var colIntensities =
                            documentGridForm.FindColumn(pathData.Property(nameof(Chromatogram.Data.Intensities)));
                        var colMassErrors =
                            documentGridForm.FindColumn(pathData.Property(nameof(Chromatogram.Data.MassErrors)));
                        var colSpectrumIds =
                            documentGridForm.FindColumn(pathData.Property(nameof(Chromatogram.Data.SpectrumIds)));

                        var chromatogramInfo =
                            chromatogramGroup.GetTransitionInfo(transition.DocNode, .0001f, interpolated ? TransformChrom.interpolated : TransformChrom.raw, null);
                        Assert.IsNotNull(chromatogramInfo);
                        VerifyChromatogramData(cultureInfo, chromatogramInfo, row, colTimes, colIntensities, colMassErrors, colSpectrumIds);
                    }
                }
            });
        }

        private void VerifyChromatogramData(CultureInfo cultureInfo, ChromatogramInfo chromatogram, DataGridViewRow row, DataGridViewColumn colTimes,
            DataGridViewColumn colIntensities, DataGridViewColumn colMassErrors, DataGridViewColumn colSpectrumIds)
        {
            var csvSeparator = TextUtil.GetCsvSeparator(cultureInfo);
            var strTimes = (string)row.Cells[colTimes.Index].FormattedValue;
            Assert.IsNotNull(strTimes);
            var times = strTimes.Split(csvSeparator)
                .Select(value => float.Parse(value, cultureInfo));
            VerifyFloatsEqual(chromatogram.Times, times, colTimes.DefaultCellStyle.Format);
            var strIntensities = (string)row.Cells[colIntensities.Index].FormattedValue;
            Assert.IsNotNull(strIntensities);
            var rawIntensities = strIntensities.Split(csvSeparator)
                .Select(value => float.Parse(value, cultureInfo));
            VerifyFloatsEqual(chromatogram.Intensities, rawIntensities,
                colIntensities.DefaultCellStyle.Format);
            var strMassErrors = (string)row.Cells[colMassErrors.Index].FormattedValue;
            Assert.IsNotNull(strMassErrors);
            var rawMassErrors = strMassErrors.Split(csvSeparator)
                .Select(value => float.Parse(value, cultureInfo));
            VerifyFloatsEqual(chromatogram.TimeIntensities.MassErrors, rawMassErrors,
                colMassErrors.DefaultCellStyle.Format);
            var strRawSpectrumIds = (string)row.Cells[colSpectrumIds.Index].FormattedValue;
            Assert.IsNotNull(strRawSpectrumIds);
            var rawSpectrumIds = strRawSpectrumIds.Split(csvSeparator).ToArray();
            var msDataFileScanIds = SkylineWindow.Document.Settings.MeasuredResults.LoadMSDataFileScanIds(
                chromatogram.FilePath, out _);
            var expectedSpectrumIds = chromatogram.TimeIntensities.ScanIds
                .Select(msDataFileScanIds.GetMsDataFileSpectrumId).ToArray();
            CollectionAssert.AreEqual(expectedSpectrumIds, rawSpectrumIds);
        }

        private void VerifyFloatsEqual(IEnumerable<float> floats1, IEnumerable<float> floats2, string format)
        {
            using (var en1 = floats1.GetEnumerator())
            using (var en2 = floats2.GetEnumerator())
            {
                while (en1.MoveNext())
                {
                    Assert.IsTrue(en2.MoveNext());
                    var str1 = en1.Current.ToString(format, CultureInfo.InvariantCulture);
                    var str2 = en2.Current.ToString(format, CultureInfo.InvariantCulture);
                    Assert.AreEqual(str1, str2);
                }
                Assert.IsFalse(en2.MoveNext());
            }
        }
    }
}
