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

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests the tool service functionality in <see cref="ElementReorderer"/>
    /// </summary>
    [TestClass]
    public class ReorderElementsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestReorderElements()
        {
            TestFilesZip = @"TestFunctional\ReorderElementsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ReorderElementsTest.sky"));
                SkylineWindow.ShowDocumentGrid(true);
            });
            WaitForDocumentLoaded();
            var document = SkylineWindow.Document;
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(() =>
            {
                documentGrid.ChooseView("ResultFileLocators");
            });
            WaitForCondition(() => documentGrid.IsComplete);

            // Change the order of the replicates to their reverse order
            var replicateLocators = GetElementLocators(documentGrid, PropertyPath.Root.Property(nameof(Replicate.Locator))).Distinct().ToList();
            var reverseReplicateDoc = SetElementOrder(document, replicateLocators.AsEnumerable().Reverse());
            CollectionAssert.AreEqual(
                document.Settings.MeasuredResults.Chromatograms.Select(chrom => chrom.Name).Reverse().ToList(),
                reverseReplicateDoc.Settings.MeasuredResults.Chromatograms.Select(chrom => chrom.Name).ToList());

            // Change the order of the proteins to their reverse order
            RunUI(()=>documentGrid.ChooseView("DocNodeLocators"));
            WaitForCondition(() => documentGrid.IsComplete);
            var proteinLocators = GetElementLocators(documentGrid,
                PropertyPath.Root.Property(nameof(Skyline.Model.Databinding.Entities.Transition.Precursor))
                    .Property(nameof(Precursor.Peptide))
                    .Property(nameof(Skyline.Model.Databinding.Entities.Peptide.Protein))
                    .Property(nameof(Protein.Locator))).Distinct().ToList();
            var reverseProteinDoc = SetElementOrder(document, proteinLocators.AsEnumerable().Reverse());
            CollectionAssert.AreEqual(document.MoleculeGroups.Select(molGroup=>molGroup.Name).Reverse().ToList(), reverseProteinDoc.MoleculeGroups.Select(molGroup=>molGroup.Name).ToList());
        }

        private List<ElementLocator> GetElementLocators(DataboundGridForm databoundGridForm, PropertyPath propertyPath)
        {
            List<ElementLocator> values = new List<ElementLocator>();
            RunUI(() =>
            {
                var column = databoundGridForm.FindColumn(propertyPath);
                Assert.IsNotNull(column, "Unable to find column {0}", propertyPath);
                values.AddRange(databoundGridForm.DataGridView.Rows.OfType<DataGridViewRow>()
                    .Select(row => ElementLocator.Parse((string) row.Cells[column.Index].Value)));
            });
            return values;
        }

        private SrmDocument SetElementOrder(SrmDocument document, IEnumerable<ElementLocator> locators)
        {
            ElementReorderer elementReorderer = new ElementReorderer(CancellationToken.None, document);
            return elementReorderer.SetNewOrder(locators.Select(ElementRefs.FromObjectReference));
        }
    }
}
