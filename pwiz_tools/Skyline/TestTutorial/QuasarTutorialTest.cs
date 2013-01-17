/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    [TestClass]
    public class QuasarTutorialTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestQuasarTutorial()
        {
            // Set true to look at tutorial screenshots.
            //IsPauseForScreenShots = true;

            TestFilesZip = @"http://skyline.gs.washington.edu/tutorials/QuaSAR.zip";
            RunFunctionalTest();
        }

        private string GetTestPath(string relativePath)
        {
            const string folderQuaser = "QuaSAR";
            return TestFilesDir.GetTestPath(Path.Combine(folderQuaser, relativePath));
        }

        protected override void DoTest()
        {
            // p. 1 open the file
            string documentFile = GetTestPath(@"QuaSAR_Tutorial.sky");
            WaitForCondition(() => File.Exists(documentFile));
            RunUI(() => SkylineWindow.OpenFile(documentFile));

            var document = SkylineWindow.Document;
            AssertEx.IsDocumentState(document, null, 34, 125, 250, 750);

            var annotationsDlg = ShowDialog<ChooseAnnotationsDlg>(SkylineWindow.ShowAnnotationsDialog);
            var editListDlg = ShowDialog<EditListDlg<SettingsListBase<AnnotationDef>, AnnotationDef>>(annotationsDlg.EditList);

            PauseForScreenShot("p. 2 - Define Annotations");

            var annotationDecls = new[]
                {
                    new AnnotationDecl("SampleID", AnnotationDef.AnnotationType.text, 3),
                    new AnnotationDecl("Analyte Concentration", AnnotationDef.AnnotationType.number, 4),
                    new AnnotationDecl("IS Conc", AnnotationDef.AnnotationType.number, 5)
                };
            foreach (var annotationDecl in annotationDecls)
            {
                AddAnotation(editListDlg, annotationDecl);
            }

            OkDialog(editListDlg, editListDlg.OkDialog);
            RunUI(() =>
                {
                    var checkedListBox = annotationsDlg.AnnotationsCheckedListBox;
                    for (int i = 0; i < checkedListBox.Items.Count; i++)
                    {
                        checkedListBox.SetItemChecked(i, true);
                    }                    
                });
            PauseForScreenShot("p. 6 - Annotation Settings");

            OkDialog(annotationsDlg, annotationsDlg.OkDialog);

            RunUI(() => SkylineWindow.ShowResultsGrid(true));
            RunUI(() =>
            {
                SkylineWindow.SelectedPath =
                    SkylineWindow.DocumentUI.GetPathTo((int)SrmDocument.Level.PeptideGroups, 0);
            });
            WaitForGraphs();
            ResultsGrid resultsGrid = null;
            DataGridViewColumn colSampleId = null, colConcentration = null, colIsConc = null;
            RunUI(() =>
                {
                    resultsGrid = FindOpenForm<ResultsGridForm>().ResultsGrid;
                    colSampleId =
                        resultsGrid.Columns.Cast<DataGridViewColumn>().First(col => annotationDecls[0].Name == col.HeaderText);
                    colConcentration =
                        resultsGrid.Columns.Cast<DataGridViewColumn>().First(col => annotationDecls[1].Name == col.HeaderText);
                    colIsConc =
                        resultsGrid.Columns.Cast<DataGridViewColumn>().First(col => annotationDecls[2].Name == col.HeaderText);
                });
            WaitForCondition(() => resultsGrid != null && colSampleId != null && colConcentration != null && colIsConc != null);

            float[] concentrations =
                new[] { 0f, .001f, .004f, .018f, .075f, .316f, 1.33f, 5.62f, 23.71f, 100 };
            string[] sampleIds =
                new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" };

            RunUI(() =>
            {
                ResultsGridForm.SynchronizeSelection = false;

                for (int i = 0; i < concentrations.Length; i++)
                {
                    for (int j = i*4; j < (i + 1)*4; j++)
                    {
                        SetCellValue(resultsGrid, j, colSampleId.Index, sampleIds[i]);
                        SetCellValue(resultsGrid, j, colConcentration.Index, concentrations[i]);
                        SetCellValue(resultsGrid, j, colIsConc.Index, 10);
                    }
                }
            });
            WaitForGraphs();
            PauseForScreenShot("p. 7 - Results Grid");

            var separator = TextUtil.CsvSeparator;
            var sb = new StringBuilder();
            var writer = new StringWriter(sb);
            const string headers = "SampleName,SampleID,Analyte Concentration,IS Conc,Concentration Ratio";
            foreach (var header in headers.Split(','))
            {
                if (sb.Length > 0)
                    writer.Write(separator);
                writer.WriteDsvField(header, separator);
            }
            writer.WriteLine();
            RunUI(() =>
                {
                    for (int i = 0; i < concentrations.Length * 4; i++)
                    {
                        var row = resultsGrid.Rows[i];
                        row.Cells[0].Selected = true;
                        writer.WriteDsvField(row.Cells[0].Value.ToString(), separator);
                        writer.Write(separator);
                        row.Cells[colSampleId.Index].Selected = true;
                        writer.WriteDsvField(row.Cells[colSampleId.Index].Value.ToString(), separator);
                        writer.Write(separator);
                        row.Cells[colConcentration.Index].Selected = true;
                        writer.WriteDsvField(row.Cells[colConcentration.Index].Value.ToString(), separator);
                        writer.Write(separator);
                        row.Cells[colIsConc.Index].Selected = true;
                        writer.WriteDsvField(row.Cells[colIsConc.Index].Value.ToString(), separator);
                        writer.WriteLine();
                    }
                });

            var concentrationsPath = GetTestPath(@"Concentrations.csv");
            File.WriteAllText(concentrationsPath, sb.ToString());

            if (IsPauseForScreenShots)
                Process.Start(concentrationsPath);

            PauseForScreenShot("p. 9 - Concentrations.csv in Excel");

            var quasarInputPath = GetTestPath(@"QuaSAR_Tutorial.csv");
            {
                var exportReportDlg = ShowDialog<ExportReportDlg>(SkylineWindow.ShowExportReportDialog);
                RunUI(() => exportReportDlg.ReportName = "QuaSAR Input");

                PauseForScreenShot("p. 10 - Export Report");

                OkDialog(exportReportDlg, () => exportReportDlg.OkDialog(quasarInputPath, TextUtil.CsvSeparator));
            }

            if (IsPauseForScreenShots)
                Process.Start(quasarInputPath);

            PauseForScreenShot("p. 11 - QuaSAR_Tutorial.csv in Excel");

            if (IsPauseForScreenShots)
                WebHelpers.OpenLink("http://genepattern.broadinstitute.org/gp/pages/index.jsf?lsid=QuaSAR");

            // Put input report path on the clipboard for easy setting
            if (IsPauseForScreenShots)
                Clipboard.SetText(quasarInputPath);

            PauseForScreenShot("p. 11 - QuaSAR input form - paste input path");

            PauseForScreenShot("p. 11 - QuaSAR output");
        }

        private void AddAnotation(EditListDlg<SettingsListBase<AnnotationDef>, AnnotationDef> editListDlg, AnnotationDecl annotationDecl)
        {
            var defineAnnotationDlg = ShowDialog<DefineAnnotationDlg>(editListDlg.AddItem);
            RunUI(() =>
            {
                defineAnnotationDlg.AnnotationName = annotationDecl.Name;
                defineAnnotationDlg.AnnotationType = annotationDecl.Type;
                defineAnnotationDlg.AnnotationTargets =
                    AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.replicate);
            });

            PauseForScreenShot(string.Format("p. {0} - Define Annotation", annotationDecl.Page));
            OkDialog(defineAnnotationDlg, defineAnnotationDlg.OkDialog);
        }


        private class AnnotationDecl
        {
            public string Name { get; private set; }
            public AnnotationDef.AnnotationType Type { get; private set; }
            public int Page { get; private set; }

            public AnnotationDecl(string name, AnnotationDef.AnnotationType type, int page)
            {
                Name = name;
                Type = type;
                Page = page;
            }
        }

        private void SetCellValue(DataGridView dataGridView, int rowIndex, int columnIndex, object value)
        {
            dataGridView.CurrentCell = dataGridView.Rows[rowIndex].Cells[columnIndex];
            dataGridView.BeginEdit(true);
            dataGridView.CurrentCell.Value = value;
            dataGridView.EndEdit();
        }
    }
}
