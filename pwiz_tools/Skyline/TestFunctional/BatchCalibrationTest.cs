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
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs.Calibration;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class BatchCalibrationTest : AbstractFunctionalTest
    {
        private const string BatchNamesView = "BatchNames";
        [TestMethod]
        public void TestBatchCalibration()
        {
            TestFilesZip = @"TestFunctional\BatchCalibrationTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("BatchCalibrationTest.sky"));
                SkylineWindow.ShowDocumentGrid(true);
                SkylineWindow.ShowCalibrationForm();
            });
            VerifyQuantificationResults(false);
            SetBatchNames();
            VerifyQuantificationResults(true);
        }

        private void SetBatchNames()
        {
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(() => documentGrid.DataboundGridControl.ChooseView(
                ViewGroup.BUILT_IN.Id.ViewName(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates)));
            WaitForConditionUI(() => documentGrid.IsComplete);
            var viewEditor = ShowDialog<ViewEditor>(documentGrid.NavBar.CustomizeView);
            var filesPropertyPath = PropertyPath.Root.Property(nameof(SkylineDocument.Replicates)).LookupAllItems()
                .Property(nameof(Replicate.Files)).LookupAllItems();
            RunUI(() =>
            {
                viewEditor.ChooseColumnsTab.AddColumn(PropertyPath.Root.Property(nameof(SkylineDocument.Replicates)).LookupAllItems().Property(nameof(Replicate.BatchName)));
                viewEditor.ChooseColumnsTab.AddColumn(filesPropertyPath.Property(nameof(ResultFile.TicArea)));
                viewEditor.ChooseColumnsTab.AddColumn(filesPropertyPath.Property(nameof(ResultFile.ExplicitGlobalStandardArea)));
                viewEditor.ViewName = BatchNamesView;
            });
            OkDialog(viewEditor, viewEditor.OkDialog);
            WaitForConditionUI(() => documentGrid.IsComplete);
            var colReplicate = documentGrid.FindColumn(PropertyPath.Root);
            RunUI(() =>
            {
                for (int iRow = 0; iRow < documentGrid.RowCount; iRow++)
                {
                    var replicate = (Replicate) documentGrid.DataGridView.Rows[iRow].Cells[colReplicate.Index].Value;
                    if (replicate.Name == "PooledGlomeruliSample")
                    {
                        replicate.BatchName = "glomeruli";
                        Assert.AreEqual(SampleType.STANDARD, replicate.SampleType);
                        Assert.AreEqual(1, replicate.AnalyteConcentration);
                    }
                    else if (replicate.Name == "PooledCortexSample")
                    {
                        replicate.BatchName = "cortex";
                        Assert.AreEqual(SampleType.STANDARD, replicate.SampleType);
                        Assert.AreEqual(1, replicate.AnalyteConcentration);
                    }
                    else if (replicate.Name.StartsWith("glom-"))
                    {
                        replicate.BatchName = "glomeruli";
                        Assert.AreEqual(SampleType.UNKNOWN, replicate.SampleType);
                        Assert.IsNull(replicate.AnalyteConcentration);
                    }
                    else if (replicate.Name.StartsWith("cort-"))
                    {
                        replicate.BatchName = "cortex";
                        Assert.AreEqual(SampleType.UNKNOWN, replicate.SampleType);
                        Assert.IsNull(replicate.AnalyteConcentration);
                    }
                    else
                    {
                        Assert.Fail("Unexpected replicate name {0}", replicate.Name);
                    }
                    Assert.AreEqual(1, replicate.Files.Count);
                    var file = replicate.Files[0];
                    Assert.AreEqual(file.TicArea, file.ExplicitGlobalStandardArea);
                }
            });
        }

        private void VerifyQuantificationResults(bool hasBatchNames)
        {
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(()=>documentGrid.ChooseView("PeptideQuantificationResults"));
            WaitForConditionUI(() => documentGrid.IsComplete);
            var propertyPathResults = PropertyPath.Root
                .Property(nameof(Peptide.Results)).LookupAllItems()
                .Property(nameof(KeyValuePair<ResultKey, PeptideResult>.Value));
            var colPeptide = documentGrid.FindColumn(PropertyPath.Root);
            var colReplicate = documentGrid.FindColumn(propertyPathResults
                .Property(nameof(PeptideResult.ResultFile))
                .Property(nameof(ResultFile.Replicate)));
            var colCalibrationCurve =
                documentGrid.FindColumn(PropertyPath.Root.Property(nameof(Peptide.CalibrationCurve)));
            var colReplicateCalibrationCurve =
                documentGrid.FindColumn(propertyPathResults.Property(nameof(PeptideResult.ReplicateCalibrationCurve)));
            var colQuantification =
                documentGrid.FindColumn(propertyPathResults.Property(nameof(PeptideResult.Quantification)));
            for (int iRow = 0; iRow < documentGrid.RowCount; iRow++)
            {
                Peptide peptide = null;
                Replicate replicate = null;
                var calibrationCurve = default(LinkValue<CalibrationCurve>);
                var replicateCalibrationCurve = default(LinkValue<CalibrationCurve>);
                RunUI(() =>
                {
                    var row = documentGrid.DataGridView.Rows[iRow];
                    peptide = (Peptide)row.Cells[colPeptide.Index].Value;
                    replicate = (Replicate)row.Cells[colReplicate.Index].Value;
                    calibrationCurve = (LinkValue<CalibrationCurve>)row.Cells[colCalibrationCurve.Index].Value;
                    replicateCalibrationCurve =
                        (LinkValue<CalibrationCurve>)row.Cells[colReplicateCalibrationCurve.Index].Value;
                });
                if (hasBatchNames)
                {
                    Assert.AreNotEqual(calibrationCurve.Value.PointCount, replicateCalibrationCurve.Value.PointCount);
                }
                else
                {
                    Assert.AreEqual(calibrationCurve.Value.PointCount, replicateCalibrationCurve.Value.PointCount);
                    Assert.AreEqual(calibrationCurve.Value.Slope, replicateCalibrationCurve.Value.Slope);
                }

                int selectedResultsIndexOld = SkylineWindow.SelectedResultsIndex;
                RunUI(()=>calibrationCurve.ClickEventHandler(new object(), new EventArgs()));
                var calibrationForm = FindOpenForm<CalibrationForm>();
                Assert.IsNotNull(calibrationForm);
                WaitForGraphs();
                Assert.AreEqual(peptide.IdentityPath, SkylineWindow.SelectedPath);
                Assert.AreEqual(selectedResultsIndexOld, SkylineWindow.SelectedResultsIndex);
                RunUI(()=>replicateCalibrationCurve.ClickEventHandler(new object(), new EventArgs()));
                Assert.AreEqual(peptide.IdentityPath, SkylineWindow.SelectedPath);
                Assert.AreEqual(replicate.ReplicateIndex, SkylineWindow.SelectedResultsIndex);
            }
        }
    }
}
