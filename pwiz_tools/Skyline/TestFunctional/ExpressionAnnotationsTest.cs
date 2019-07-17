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
using System.IO;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ExpressionAnnotationsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestExpressionAnnotations()
        {
            TestFilesZip = @"TestFunctional\ExpressionAnnotationsTest.zip";
            RunFunctionalTest();
        }


        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ExpressionAnnotationsTest.sky")));
            var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            var defineAnnotationDlg = ShowDialog<DefineAnnotationDlg>(documentSettingsDlg.AddAnnotation);
            RunUI(() =>
            {
                defineAnnotationDlg.AnnotationName = "PeptideCount";
                defineAnnotationDlg.IsCalculated = true;
                defineAnnotationDlg.AnnotationTargets =
                    AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.protein);
                defineAnnotationDlg.SelectPropertyPath(PropertyPath.Root
                    .Property(nameof(Protein.Peptides)).LookupAllItems());
                defineAnnotationDlg.AggregateOperation = AggregateOperation.Count;
            });
            OkDialog(defineAnnotationDlg, defineAnnotationDlg.OkDialog);
            OkDialog(documentSettingsDlg, documentSettingsDlg.OkDialog);
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(() => documentGrid.DataboundGridControl.ChooseView(
                ViewGroup.BUILT_IN.Id.ViewName(Resources.SkylineViewContext_GetDocumentGridRowSources_Proteins)));
            WaitForConditionUI(() => documentGrid.IsComplete);
            var colProtein = documentGrid.FindColumn(PropertyPath.Root);
            Assert.IsNotNull(colProtein);
            var colPeptideCount =
                documentGrid.FindColumn(PropertyPath.Root.Property(AnnotationDef.ANNOTATION_PREFIX + "PeptideCount"));
            Assert.AreEqual(typeof(double), colPeptideCount.ValueType);
            Assert.IsNotNull(colPeptideCount);
            RunUI(() =>
            {
                for (int iRow = 0; iRow < documentGrid.RowCount; iRow++)
                {
                    var row = documentGrid.DataGridView.Rows[iRow];
                    var protein = (Protein) row.Cells[colProtein.Index].Value;
                    var peptideCountObject = row.Cells[colPeptideCount.Index].Value;
                    Assert.IsInstanceOfType(peptideCountObject, typeof(double));
                    var peptideCount = (int) (double) peptideCountObject;
                    Assert.AreEqual(protein.Peptides.Count, peptideCount);
                }
            });
            var saveAsFileName = TestFilesDir.GetTestPath("result.sky");
            RunUI(()=>SkylineWindow.SaveDocument(saveAsFileName));
            var documentReader = new DocumentReader()
            {
                RemoveCalculatedAnnotationValues = false
            };
            using (var stream = File.Open(saveAsFileName, FileMode.Open))
            using (var reader = new XmlTextReader(stream) {WhitespaceHandling = WhitespaceHandling.Significant})
            {
                while (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read();
                }
                documentReader.ReadXml(reader);
                foreach (var protein in documentReader.Children)
                {
                    var peptideCount = Convert.ToInt32(protein.Annotations.GetAnnotation("PeptideCount"));
                    Assert.AreEqual(protein.Children.Count, peptideCount);
                }
            }
        }
    }
}
