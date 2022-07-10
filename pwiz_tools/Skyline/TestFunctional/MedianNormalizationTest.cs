/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class MedianNormalizationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMedianNormalization()
        {
            TestFilesZip = @"TestFunctional\MedianNormalizationTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            AreaReplicateGraphPane areaReplicateGraphPane = null;
            PeptideGroupDocNode secondProtein = null;

            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("MedianNormalizationTest.sky"));
                Assert.AreEqual(NormalizationMethod.EQUALIZE_MEDIANS, SkylineWindow.Document.Settings.PeptideSettings.Quantification.NormalizationMethod);
                SkylineWindow.SetDisplayTypeChrom(DisplayTypeChrom.products);
                SkylineWindow.SetNormalizationMethod(NormalizationMethod.EQUALIZE_MEDIANS);
                Settings.Default.PeakAreaDotpDisplay = DotProductDisplayOption.none.ToString();
                Settings.Default.ShowLibraryPeakArea = false;
                secondProtein = (PeptideGroupDocNode) SkylineWindow.Document.Children[1];
                Assert.AreEqual(1, secondProtein.Children.Count);
                SkylineWindow.SelectedPath = new IdentityPath(secondProtein.Id, secondProtein.Children[0].Id);
                SkylineWindow.ShowPeakAreaReplicateComparison();
                areaReplicateGraphPane = FindGraphPane<AreaReplicateGraphPane>();
                areaReplicateGraphPane.ExpectedVisible = AreaExpectedValue.none;
            });
            var groupComparison = GroupComparisonDef.EMPTY.ChangeName("GroupComparison")
                .ChangeControlAnnotation("Diagnosis").ChangeControlValue("AD").ChangePerProtein(true);
            var noNormalizationFoldChanges =
                GetFoldChanges(groupComparison.ChangeNormalizationMethod(NormalizationMethod.NONE));
            var medianNormalizedFoldChanges =
                GetFoldChanges(groupComparison.ChangeNormalizationMethod(NormalizationMethod.EQUALIZE_MEDIANS));
            WaitForGraphs();
            RunUI(() =>
            {
                var secondProteinFoldChange = medianNormalizedFoldChanges[new IdentityPath(secondProtein.Id)];
                Assert.AreEqual(1, secondProtein.Children.Count);
                var peptideDocNode = secondProtein.Molecules.First();
                Assert.AreEqual(1, peptideDocNode.Children.Count);
                var precursorDocNode = peptideDocNode.TransitionGroups.First();
                var productTransitions = precursorDocNode.Transitions.Where(t => !t.IsMs1).ToList();
                Assert.AreEqual(productTransitions.Count, areaReplicateGraphPane.CurveList.Count);
                var replicates = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms;
                Assert.AreEqual(2, replicates.Count);
                Assert.AreEqual("AD", replicates[0].Annotations.GetAnnotation("Diagnosis"));
                Assert.AreEqual("PD", replicates[1].Annotations.GetAnnotation("Diagnosis"));
                double adArea = 0;
                double pdArea = 0;
                foreach (var curve in areaReplicateGraphPane.CurveList)
                {
                    Assert.AreEqual(replicates.Count, curve.Points.Count);
                    adArea += curve.Points[0].Y;
                    pdArea += curve.Points[1].Y;
                }

                var ratio = pdArea / adArea;
                AssertEx.AreEqual(secondProteinFoldChange, ratio, 1e-6);
            });
            DocumentGridForm documentGrid = null;
            RunUI(()=>
            {
                SkylineWindow.ShowDocumentGrid(true);
                documentGrid = FindOpenForm<DocumentGridForm>();
                documentGrid.DataboundGridControl.ChooseView(
                    ViewGroup.BUILT_IN.Id.ViewName(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates));
            });
            WaitForConditionUI(() => documentGrid.IsComplete);
            const string replicateMediansReportName = "Replicate Median Data";
            RunDlg<ViewEditor>(documentGrid.NavBar.CustomizeView, viewEditor =>
            {
                var ppReplicates = PropertyPath.Root.Property(nameof(SkylineDocument.Replicates)).LookupAllItems();
                var ppResultFiles = ppReplicates.Property(nameof(Replicate.Files)).LookupAllItems();
                viewEditor.ChooseColumnsTab.RemoveColumns(0, viewEditor.ChooseColumnsTab.ColumnCount);
                foreach (var propertyPath in new[]
                         {
                             ppReplicates,
                             ppResultFiles.Property(nameof(ResultFile.ExplicitGlobalStandardArea)),
                             ppResultFiles.Property(nameof(ResultFile.MedianPeakArea)),
                             ppResultFiles.Property(nameof(ResultFile.NormalizationDivisor))
                         })
                {
                    viewEditor.ChooseColumnsTab.AddColumn(propertyPath);
                }

                viewEditor.ViewName = replicateMediansReportName;
                viewEditor.OkDialog();
            });
            WaitForConditionUI(() => documentGrid.IsComplete);
            var medianAreas = new List<double>();
            var medianNormalizationDivisors = new List<double>();
            RunUI(() =>
            {
                PropertyPath ppResultFiles = PropertyPath.Root.Property(nameof(Replicate.Files)).LookupAllItems();
                PropertyPath ppMedianPeakArea = ppResultFiles.Property(nameof(ResultFile.MedianPeakArea));
                PropertyPath ppNormalizationDivisor = ppResultFiles.Property(nameof(ResultFile.NormalizationDivisor));
                ColumnPropertyDescriptor pdMedianPeakArea = documentGrid.DataboundGridControl.BindingListSource
                    .ItemProperties.
                    OfType<ColumnPropertyDescriptor>().FirstOrDefault(pd => ppMedianPeakArea.Equals(pd.PropertyPath));
                Assert.IsNotNull(pdMedianPeakArea);
                ColumnPropertyDescriptor pdMedianNormalizationDivisor = documentGrid.DataboundGridControl
                    .BindingListSource.ItemProperties.OfType<ColumnPropertyDescriptor>().FirstOrDefault(pd =>
                        ppNormalizationDivisor.Equals(pd.PropertyPath));
                Assert.IsNotNull(pdMedianNormalizationDivisor);
                foreach (var rowItem in documentGrid.DataboundGridControl.BindingListSource.OfType<RowItem>())
                {
                    var medianArea = pdMedianPeakArea.GetValue(rowItem);
                    Assert.IsNotNull(medianArea);
                    Assert.IsInstanceOfType(medianArea, typeof(double));
                    medianAreas.Add((double) medianArea);
                    var medianNormalizationDivisor = pdMedianNormalizationDivisor.GetValue(rowItem);
                    Assert.IsInstanceOfType(medianNormalizationDivisor, typeof(double));
                    Assert.IsNotNull(medianNormalizationDivisor);
                    medianNormalizationDivisors.Add((double) medianNormalizationDivisor);
                }
            });
            Assert.AreEqual(SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Count, medianAreas.Count);
            Assert.AreEqual(SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Count, medianNormalizationDivisors.Count);
            var medianMedian = medianAreas[0] / medianNormalizationDivisors[0];
            for (int i = 0; i < medianAreas.Count; i++)
            {
                Assert.AreEqual(medianMedian, medianAreas[i] / medianNormalizationDivisors[i], .00001);
            }

            SetExplicitGlobalStandardAreas(documentGrid,
                Enumerable.Repeat(100.0, SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Count)
                    .ToList());
            var normalizedToConstantFoldChanges =
                GetFoldChanges(groupComparison.ChangeNormalizationMethod(NormalizationMethod.GLOBAL_STANDARDS));
            Assert.AreEqual(noNormalizationFoldChanges.Count, normalizedToConstantFoldChanges.Count);
            foreach (var entry in normalizedToConstantFoldChanges)
            {
                var expectedFoldChange = noNormalizationFoldChanges[entry.Key];
                Assert.AreEqual(expectedFoldChange, entry.Value);
            }
            SetExplicitGlobalStandardAreas(documentGrid, medianNormalizationDivisors);
            var normalizedToMedianDivisorsFoldChanges =
                GetFoldChanges(groupComparison.ChangeNormalizationMethod(NormalizationMethod.GLOBAL_STANDARDS));
            Assert.AreEqual(medianNormalizedFoldChanges.Count, normalizedToMedianDivisorsFoldChanges.Count);
            foreach (var entry in normalizedToMedianDivisorsFoldChanges)
            {
                var expectedFoldChange = medianNormalizedFoldChanges[entry.Key];
                Assert.AreEqual(expectedFoldChange, entry.Value, .00001);
            }
        }

        private T FindGraphPane<T>() where T : class
        {
            foreach (var graphSummary in FormUtil.OpenForms.OfType<GraphSummary>())
            {
                if (graphSummary.TryGetGraphPane(out T pane))
                {
                    return pane;
                }
            }
            return null;
        }

        private IDictionary<IdentityPath, double> GetFoldChanges(GroupComparisonDef groupComparisonDef)
        {
            FoldChangeGrid foldChangeGrid = null;
            RunUI(()=>
            {
                foreach (var foldChangeForm in FindFoldChangeForms(groupComparisonDef.Name))
                {
                    foldChangeForm.Close();
                }

                if (!SkylineWindow.Document.Settings.DataSettings.GroupComparisonDefs.Contains(groupComparisonDef))
                {
                    SkylineWindow.ModifyDocument("Swap group comparison", doc =>
                    {
                        var dataSettings = doc.Settings.DataSettings;
                        var newGroupComparisons = dataSettings.GroupComparisonDefs
                            .Where(def => def.Name != groupComparisonDef.Name).Append(groupComparisonDef).ToList();
                        return doc.ChangeSettings(
                            doc.Settings.ChangeDataSettings(dataSettings.ChangeGroupComparisonDefs(newGroupComparisons)));
                    }, AuditLogEntry.SettingsLogFunction);
                }
                SkylineWindow.ShowGroupComparisonWindow(groupComparisonDef.Name);
                foldChangeGrid = FindFoldChangeForms(groupComparisonDef.Name).OfType<FoldChangeGrid>()
                    .FirstOrDefault();
            });
            Assert.IsNotNull(foldChangeGrid);
            WaitForConditionUI(() => 0 != foldChangeGrid.DataboundGridControl.RowCount);
            return GetFoldChanges(foldChangeGrid);
        }

        private IEnumerable<FoldChangeForm> FindFoldChangeForms(string groupComparisonName)
        {
            return FindOpenForms<FoldChangeForm>().Where(form =>
                form.FoldChangeBindingSource.GroupComparisonModel.GroupComparisonName == groupComparisonName);
        }

        private IDictionary<IdentityPath, double> GetFoldChanges(FoldChangeGrid foldChangeGrid)
        {
            var dictionary = new Dictionary<IdentityPath, double>();
            foreach (RowItem rowItem in foldChangeGrid.DataboundGridControl.BindingListSource)
            {
                var foldChangeRow = (FoldChangeBindingSource.FoldChangeRow) rowItem.Value;
                if (foldChangeRow.MsLevel == 2)
                {
                    var identityPath = foldChangeRow.Peptide?.IdentityPath ?? foldChangeRow.Protein.IdentityPath;
                    dictionary.Add(identityPath, foldChangeRow.FoldChangeResult.FoldChange);
                }
            }

            return dictionary;
        }

        private void SetExplicitGlobalStandardAreas(DocumentGridForm documentGridForm, IList<double> values)
        {
            RunUI(() =>
            {
                Assert.AreEqual(documentGridForm.RowCount, values.Count);
                var colExplicitGlobalStandardArea = documentGridForm.FindColumn(PropertyPath.Root.Property(nameof(Replicate.Files)).LookupAllItems()
                    .Property(nameof(ResultFile.ExplicitGlobalStandardArea)));
                Assert.IsNotNull(colExplicitGlobalStandardArea);
                documentGridForm.DataGridView.CurrentCell =
                    documentGridForm.DataGridView.Rows[0].Cells[colExplicitGlobalStandardArea.Index];
                SetClipboardText(TextUtil.LineSeparate(values.Select(value=>value.ToString(Formats.RoundTrip))));
                documentGridForm.DataGridView.SendPaste();
            });
        }
    }
}
