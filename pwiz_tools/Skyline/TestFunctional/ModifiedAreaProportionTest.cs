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
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.SkylineTestUtil;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ModifiedAreaProportionTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestModifiedAreaProportion()
        {
            TestFilesZip = @"TestFunctional\ModifiedAreaProportionTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Modifications.sky"));
                SkylineWindow.ShowDocumentGrid(true);
            });
            WaitForDocumentLoaded();
            var documentGrid = FindOpenForm<DocumentGridForm>();
            WaitForConditionUI(() => documentGrid.IsComplete);
            var viewEditor = ShowDialog<ViewEditor>(documentGrid.NavBar.CustomizeView);
            const string viewName = "PtmPercentageReport";
            RunUI(() =>
            {
                viewEditor.ChooseColumnsTab.RemoveColumns(0, viewEditor.ChooseColumnsTab.ColumnCount);
                var ppPeptide = PropertyPath.Root.Property(nameof(SkylineDocument.Proteins)).LookupAllItems()
                    .Property(nameof(Protein.Peptides)).LookupAllItems();
                var ppPeptideResult = ppPeptide.Property(nameof(Peptide.Results))
                    .LookupAllItems().Property(nameof(KeyValuePair<object, object>.Value));
                viewEditor.ChooseColumnsTab.AddColumn(ppPeptide);
                viewEditor.ChooseColumnsTab.AddColumn(ppPeptideResult
                    .Property(nameof(PeptideResult.ModifiedAreaProportion)));
                viewEditor.ChooseColumnsTab.AddColumn(ppPeptide.Property(nameof(Peptide.AttributeGroupId)));
                viewEditor.ChooseColumnsTab.AddColumn(ppPeptideResult.Property(nameof(PeptideResult.AttributeAreaProportion)));
                viewEditor.ViewName = viewName;
            });
            var pivotWidget = viewEditor.ViewEditorWidgets.OfType<PivotReplicateAndIsotopeLabelWidget>()
                .FirstOrDefault();
            Assert.IsNotNull(pivotWidget);
            RunUI(()=>pivotWidget.SetPivotReplicate(true));
            OkDialog(viewEditor, viewEditor.OkDialog);
            RunUI(()=>documentGrid.ChooseView(viewName));
            WaitForConditionUI(() => documentGrid.IsComplete);
            var colAttributeGroupId =
                documentGrid.FindColumn(PropertyPath.Root.Property(nameof(Peptide.AttributeGroupId)));
            Assert.IsNotNull(colAttributeGroupId);
            RunUI(() =>
            {
                for (int i = 0; i < documentGrid.RowCount; i++)
                {
                    var row = documentGrid.DataGridView.Rows[i];
                    if (i % 6 != 0)
                    {
                        row.Cells[colAttributeGroupId.Index].Value = (i % 6).ToString();
                    }
                }
            });
            RunUI(()=> VerifyDocumentGrid(documentGrid));
            var docRoundTrip = AssertEx.RoundTrip(SkylineWindow.Document);
            var peptideDocNodes = docRoundTrip.Molecules.ToArray();
            for (int i = 0; i < peptideDocNodes.Length; i++)
            {
                var peptideDocNode = peptideDocNodes[i];
                if (i % 6 == 0)
                {
                    Assert.IsNull(peptideDocNode.AttributeGroupId);
                }
                else
                {
                    Assert.AreEqual((i%6).ToString(), peptideDocNode.AttributeGroupId);
                }
            }
        }

        /// <summary>
        /// Verifies that the "ModifiedAreaProportion" values in the document grid are what are expected.
        /// </summary>
        /// <param name="documentGrid"></param>
        private void VerifyDocumentGrid(DocumentGridForm documentGrid)
        {
            var document = Program.MainWindow.Document;
            var dataGridView = documentGrid.DataGridView;
            Assert.AreEqual(document.MoleculeCount, documentGrid.RowCount);
            var colPeptide = documentGrid.FindColumn(PropertyPath.Root);
            // The report is pivoted on replicate name. Find all of the columns for the ModifiedAreaProportion
            var modifiedAreaProperties = new Dictionary<string, DataGridViewColumn>();
            var attributeAreaProperties = new Dictionary<string, DataGridViewColumn>();
            foreach (ColumnPropertyDescriptor pd in documentGrid.DataboundGridControl.BindingListSource.ItemProperties)
            {
                if (pd.PropertyPath.Name == nameof(PeptideResult.ModifiedAreaProportion))
                {
                    string replicateName = pd.PropertyPath.Parent.Parent.Name;
                    modifiedAreaProperties.Add(replicateName, documentGrid.FindColumn(pd.PropertyPath));
                }
                else if (pd.PropertyPath.Name == nameof(PeptideResult.AttributeAreaProportion))
                {
                    string replicateName = pd.PropertyPath.Parent.Parent.Name;
                    attributeAreaProperties.Add(replicateName, documentGrid.FindColumn(pd.PropertyPath));
                }
            }
            Assert.AreEqual(document.MeasuredResults.Chromatograms.Count, modifiedAreaProperties.Count);
            for (int i = 0; i < documentGrid.RowCount; i++)
            {
                var row = dataGridView.Rows[i];
                var peptide = (Peptide) row.Cells[colPeptide.Index].Value;
                foreach (var entry in modifiedAreaProperties)
                {
                    var chromatogramSet =
                        document.MeasuredResults.Chromatograms.FirstOrDefault(c => c.Name == entry.Key);
                    Assert.IsNotNull(chromatogramSet);
                    int replicateIndex = document.MeasuredResults.Chromatograms.IndexOf(chromatogramSet);
                    var area = GetTotalArea(peptide.DocNode, replicateIndex);
                    var actualAreaProportion = (double?) row.Cells[entry.Value.Index].Value;
                    if (peptide.DocNode.IsDecoy)
                    {
                        Assert.IsNull(actualAreaProportion);
                        continue;
                    }
                    Assert.IsTrue(actualAreaProportion <= 1.0);
                    double totalArea = 0;
                    foreach (var peptideDocNode in peptide.Protein.DocNode.Molecules)
                    {
                        if (peptideDocNode.Peptide.Sequence == peptide.Sequence)
                        {
                            totalArea += GetTotalArea(peptideDocNode, replicateIndex);
                        }
                    }
                    Assert.AreEqual(area / totalArea, actualAreaProportion.Value, .01);
                }

                foreach (var entry in attributeAreaProperties)
                {
                    var chromatogramSet =
                        document.MeasuredResults.Chromatograms.FirstOrDefault(c => c.Name == entry.Key);
                    Assert.IsNotNull(chromatogramSet);
                    int replicateIndex = document.MeasuredResults.Chromatograms.IndexOf(chromatogramSet);
                    var area = GetTotalArea(peptide.DocNode, replicateIndex);
                    var actualAreaProportion = (double?)row.Cells[entry.Value.Index].Value;
                    if (peptide.DocNode.IsDecoy)
                    {
                        Assert.IsNull(actualAreaProportion);
                        continue;
                    }

                    if (string.IsNullOrEmpty(peptide.AttributeGroupId))
                    {
                        Assert.IsNull(actualAreaProportion);
                        continue;
                    }
                    Assert.IsTrue(actualAreaProportion <= 1.0);
                    double totalArea = 0;
                    foreach (var peptideDocNode in document.Molecules)
                    {
                        if (peptideDocNode.IsDecoy)
                        {
                            continue;
                        }
                        if (peptideDocNode.AttributeGroupId == peptide.AttributeGroupId)
                        {
                            totalArea += GetTotalArea(peptideDocNode, replicateIndex);
                        }
                    }
                    Assert.AreEqual(area / totalArea, actualAreaProportion.Value, .01);
                }
            }
        }

        private double GetTotalArea(PeptideDocNode peptideDocNode, int replicateIndex)
        {
            double total = 0;
            foreach (var precursor in peptideDocNode.TransitionGroups)
            {
                total += precursor.Results[replicateIndex][0].Area.GetValueOrDefault();
            }

            return total;
        }
    }
}
