/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs.Calibration;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SurrogateStandardTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSurrogateStandards()
        {
            TestFilesZip = "TestFunctional\\SurrogateStandardTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("p180test_calibration_DukeApril2016.sky")));
            var surrogateStandards = new Dictionary<string, string>
            {
                {"Leu","Ile" },
                {"Lys","Orn"},
                {"Ac-Orn","Orn"},
                {"SDMA","ADMA"},
                {"alpha-AAA","Orn"},
                {"Carnosine","His"},
                {"Histamine", "His"},
                {"Kynurenine", "Tyr"},
                {"Met-SO", "Met"},
                {"Nitro-Tyr","Tyr"},
                {"c4-OH-Pro","Pro"},
                {"t4-OH-Pro","Pro"},
            };
            
            // Set the standard type of the surrogate standards to StandardType.SURROGATE_STANDARD
            RunUI(() =>
            {
                List<IdentityPath> pathsToSelect = SkylineWindow.SequenceTree.Nodes.OfType<PeptideGroupTreeNode>()
                    .SelectMany(peptideGroup => peptideGroup.Nodes.OfType<PeptideTreeNode>())
                    .Where(peptideTreeNode => surrogateStandards.Values.Contains(peptideTreeNode.DocNode.RawTextId))
                    .Select(treeNode => treeNode.Path)
                    .ToList();
                SkylineWindow.SequenceTree.SelectedPaths = pathsToSelect;
                SkylineWindow.SetStandardType(StandardType.SURROGATE_STANDARD);
            });

            // Use the document grid to set the Normalization Method of the molecules that have surrogate standards
            var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
            RunUI(()=>documentGrid.ChooseView(Resources.Resources_ReportSpecList_GetDefaults_Peptide_Quantification));
            IDataGridViewEditingControl editingControl = null;
            DataGridViewEditingControlShowingEventHandler onEditingControlShowing =
                (sender, args) => editingControl = args.Control as IDataGridViewEditingControl;
            documentGrid.DataGridView.EditingControlShowing += onEditingControlShowing;

            // ReSharper disable AccessToForEachVariableInClosure
            foreach (var entry in surrogateStandards)
            {
                WaitForConditionUI(() => documentGrid.IsComplete);
                RunUI(() =>
                {
                    var colPeptide = documentGrid.FindColumn(PropertyPath.Root);
                    for (int iRow = 0; iRow < documentGrid.RowCount; iRow++)
                    {
                        var row = documentGrid.DataGridView.Rows[iRow];
                        if (!entry.Key.Equals(row.Cells[colPeptide.Index].FormattedValue))
                        {
                            continue;
                        }
                        var colNormalizationMethod = documentGrid.FindColumn(PropertyPath.Root.Property("NormalizationMethod"));
                        documentGrid.DataGridView.CurrentCell = row.Cells[colNormalizationMethod.Index];
                        documentGrid.DataGridView.BeginEdit(false);
                        editingControl.EditingControlFormattedValue =
                            string.Format(Resources.RatioToSurrogate_ToString_Ratio_to_surrogate__0____1__, entry.Value,
                                "Heavy");
                        documentGrid.DataGridView.EndEdit();
                    }
                });
            }
            
            // Make sure that the Y-Axis on the CalibrationForm reflects the normalization method of the selected molecule
            var calibrationForm = ShowDialog<CalibrationForm>(()=>SkylineWindow.ShowCalibrationForm());
            foreach (var peptideGroupTreeNode in SkylineWindow.SequenceTree.Nodes.OfType<PeptideGroupTreeNode>())
            {
                foreach (var peptideTreeNode in peptideGroupTreeNode.Nodes.OfType<PeptideTreeNode>())
                {
                    RunUI(() => SkylineWindow.SequenceTree.SelectedPath = peptideTreeNode.Path);
                    WaitForGraphs();
                    string yAxisText = calibrationForm.ZedGraphControl.GraphPane.YAxis.Title.Text;
                    if (null != peptideTreeNode.DocNode.NormalizationMethod)
                    {
                        Assert.IsInstanceOfType(peptideTreeNode.DocNode.NormalizationMethod, typeof(NormalizationMethod.RatioToSurrogate));
                        Assert.AreEqual(QuantificationStrings.CalibrationCurveFitter_GetYAxisTitle_Normalized_Peak_Area,
                            yAxisText);
                    }
                    else
                    {
                        Assert.IsInstanceOfType(SkylineWindow.Document.Settings.PeptideSettings.Quantification.NormalizationMethod, typeof(NormalizationMethod.RatioToLabel));
                        string expectedTitle = CalibrationCurveFitter.PeakAreaRatioText(IsotopeLabelType.light,
                            IsotopeLabelType.heavy);
                        Assert.AreEqual(expectedTitle, yAxisText);
                    }
                }
            }
        }
    }
}
