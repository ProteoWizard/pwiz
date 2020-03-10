/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs.Calibration;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ExplicitAnalyteConcentrationTest : AbstractFunctionalTest
    {
        private const string ExplictionConcentrationsReportName = "ExplicitConcentrations";
        [TestMethod]
        public void TestExplicitAnalyteConcentration()
        {
            TestFilesZip = @"TestFunctional\ExplicitAnalyteConcentrationTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            string skylinePath = TestFilesDir.GetTestPath("ExplicitAnalyteConcentrationTest.sky");
            RunUI(() => SkylineWindow.OpenFile(skylinePath));
            SetSampleTypes();
            CreatExplicitConcentrationReport();
            FillInExplicitConcentrations();
            CheckCalibrationCurves();
            RunUI(()=>SkylineWindow.SaveDocument());
            RunUI(()=>SkylineWindow.NewDocument());
            RunUI(()=>SkylineWindow.OpenFile(skylinePath));
            WaitForDocumentLoaded();
            CheckCalibrationCurves();
        }

        private void SetSampleTypes()
        {
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(() => documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates));
            WaitForConditionUI(() => documentGrid.IsComplete);
            List<SampleType> sampleTypes = new List<SampleType>();
            DataGridViewColumn colReplicate = documentGrid.FindColumn(PropertyPath.Root);
            DataGridViewColumn colStandardType =
                documentGrid.FindColumn(PropertyPath.Root.Property(nameof(Replicate.SampleType)));
            RunUI(() =>
            {
                for (int irow = 0; irow < documentGrid.RowCount; irow++)
                {
                    var row = documentGrid.DataGridView.Rows[irow];
                    var replicate = row.Cells[colReplicate.Index].Value as Replicate;
                    Assert.IsNotNull(replicate);
                    SampleType sampleType = SampleType.UNKNOWN;
                    if (replicate.Name.StartsWith("Standard"))
                    {
                        sampleType = SampleType.STANDARD;
                    }
                    else if (replicate.Name.Contains("Control") || replicate.Name == "Negative")
                    {
                        sampleType = SampleType.QC;
                    }
                    else if (replicate.Name == "ACNWATER")
                    {
                        sampleType = SampleType.SOLVENT;
                    }
                    else if (replicate.Name == "Internal Standard Only")
                    {
                        sampleType = SampleType.BLANK;
                    }
                    sampleTypes.Add(sampleType);
                }

                var clipboardText = string.Join(Environment.NewLine,
                    sampleTypes.Select(sampleType => sampleType.ToString()));
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[0].Cells[colStandardType.Index];
                SetClipboardText(clipboardText);
                documentGrid.DataGridView.SendPaste();
            });

        }

        private void CreatExplicitConcentrationReport()
        {
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(() => documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates));
            WaitForConditionUI(() => documentGrid.IsComplete);
            var viewEditor = ShowDialog<ViewEditor>(documentGrid.NavBar.CustomizeView);
            RunUI(() =>
            {
                viewEditor.ViewName = ExplictionConcentrationsReportName;
                PropertyPath ppMolecules = PropertyPath.Root.Property(nameof(SkylineDocument.Proteins))
                    .LookupAllItems()
                    .Property(nameof(Protein.Peptides)).LookupAllItems();
                viewEditor.ChooseColumnsTab.RemoveColumns(0, viewEditor.ChooseColumnsTab.ColumnCount);
                viewEditor.ChooseColumnsTab.AddColumn(PropertyPath.Root.Property(nameof(SkylineDocument.Proteins)).LookupAllItems());
                viewEditor.ChooseColumnsTab.AddColumn(ppMolecules
                    .Property(nameof(Skyline.Model.Databinding.Entities.Peptide.Results)).LookupAllItems()
                    .Property(nameof(KeyValuePair<object, object>.Value))
                    .Property(nameof(PeptideResult.AnalyteConcentration)));
                viewEditor.TabControl.SelectTab(1);
                Assert.IsTrue(viewEditor.FilterTab.TrySelectColumn(ppMolecules.Property(nameof(Skyline.Model.Databinding.Entities.Peptide.StandardType))));
                viewEditor.FilterTab.AddSelectedColumn();
                viewEditor.FilterTab.SetFilterOperation(0, FilterOperations.OP_IS_BLANK);

                Assert.IsTrue(viewEditor.FilterTab.TrySelectColumn(PropertyPath.Root
                    .Property(nameof(SkylineDocument.Replicates)).LookupAllItems()
                    .Property(nameof(Replicate.SampleType))));
                viewEditor.FilterTab.AddSelectedColumn();
                viewEditor.FilterTab.SetFilterOperation(1, FilterOperations.OP_EQUALS);
                viewEditor.FilterTab.SetFilterOperand(1, SampleType.STANDARD.ToString());
                var pivotWidget = viewEditor.ViewEditorWidgets.OfType<PivotReplicateAndIsotopeLabelWidget>()
                    .FirstOrDefault();
                Assert.IsNotNull(pivotWidget);
                pivotWidget.SetPivotReplicate(true);
            });
            OkDialog(viewEditor, viewEditor.OkDialog);
        }

        private void FillInExplicitConcentrations()
        {
            var concentrations = new Dictionary<string, double[]>();

            concentrations.Add("1,25 dihydroxy VitD3", new[] {6.7, 19.83, 79.71, 164.05, 318.09});
            concentrations.Add("1,25 dihydroxy VitD2", new[] {4.51, 16.39, 63.86, 122.73, 226.86});
            concentrations.Add("25 hydroxy VitD3", new[] {0.99, 11.33, 55.64, 108.69, 201.91});
            concentrations.Add("25 hydroxy VitD2", new[] {1, 11.66, 56.18, 108.89, 198.27});
            concentrations.Add("24,25 dihydroxy VitD3", new[] {0.47, 5.36, 26.98, 54.05, 103.63});
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(() => documentGrid.ChooseView(ExplictionConcentrationsReportName));
            WaitForConditionUI(() => documentGrid.IsComplete);
            RunUI(() =>
            {
                var colPeptideGroup =
                    documentGrid.DataboundGridControl.FindColumn(PropertyPath.Root.Property(nameof(Skyline.Model.Databinding.Entities.Peptide.Protein)));
                for (int iRow = 0; iRow < documentGrid.RowCount; iRow++)
                {
                    var row = documentGrid.DataGridView.Rows[iRow];
                    var moleculeList = row.Cells[colPeptideGroup.Index].Value as Protein;
                    Assert.IsNotNull(moleculeList);
                    double[] moleculeConcentrations;
                    Assert.IsTrue(concentrations.TryGetValue(moleculeList.Name, out moleculeConcentrations));
                    documentGrid.DataGridView.CurrentCell = row.Cells[colPeptideGroup.Index + 1];
                    string clipboardText = string.Join(TextUtil.SEPARATOR_TSV_STR,
                        moleculeConcentrations.Select(c => c.ToString(Formats.RoundTrip)));
                    SetClipboardText(clipboardText);
                    documentGrid.DataGridView.SendPaste();
                }
            });
        }

        private void CheckCalibrationCurves()
        {
            RunUI(()=>SkylineWindow.ShowCalibrationForm());
            var calibrationForm = FindOpenForm<CalibrationForm>();
            foreach (var moleculeList in SkylineWindow.Document.MoleculeGroups)
            {
                foreach (var molecule in moleculeList.Molecules)
                {
                    var idPath = new IdentityPath(moleculeList.Id, molecule.Id);
                    RunUI(()=>SkylineWindow.SequenceTree.SelectedPath = idPath);
                    WaitForGraphs();
                    if (molecule.GlobalStandardType == null)
                    {
                        var calibrationCurve = calibrationForm.CalibrationCurve;
                        Assert.IsTrue(calibrationCurve.RSquared > 0.99);
                        Assert.AreEqual(5, calibrationCurve.PointCount);
                    }
                }
            }
        }
    }
}
