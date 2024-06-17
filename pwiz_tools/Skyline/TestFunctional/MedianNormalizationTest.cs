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
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that median normalization and TIC normalization behave correctly.
    /// </summary>
    [TestClass]
    public class MedianNormalizationTest : AbstractFunctionalTest
    {
        private const string REPORTNAME_RESULTFILE_NORMALIZATION = "File Normalization Values";
        private const string REPORTNAME_PROTEINABUNDANCES = "Protein Abundances";
        private const string ANNOTIONNAME_DIAGNOSIS = "Diagnosis";
        [TestMethod]
        public void TestMedianNormalization()
        {
            TestFilesZip = @"TestFunctional\MedianNormalizationTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("MedianNormalizationTest.sky"));
            });
            CreateReportDefinitions();
            TestNormalizationMethod(NormalizationMethod.TIC);
            TestNormalizationMethod(NormalizationMethod.EQUALIZE_MEDIANS);
        }

        private void CreateReportDefinitions()
        {
            ViewName viewNameReplicates =
                ViewGroup.BUILT_IN.Id.ViewName(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates);
            DocumentGridForm documentGrid = null;
            RunUI(() =>
            {
                SkylineWindow.ShowDocumentGrid(true);
                documentGrid = FindOpenForm<DocumentGridForm>();
                documentGrid.DataboundGridControl.ChooseView(viewNameReplicates);
            });
            WaitForConditionUI(() => documentGrid.IsComplete);
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
                             ppResultFiles.Property(nameof(ResultFile.TicArea)),
                             ppResultFiles.Property(nameof(ResultFile.NormalizationDivisor))
                         })
                {
                    viewEditor.ChooseColumnsTab.AddColumn(propertyPath);
                }

                viewEditor.ViewName = REPORTNAME_RESULTFILE_NORMALIZATION;
                viewEditor.OkDialog();
            });
            RunUI(() => documentGrid.DataboundGridControl.ChooseView(viewNameReplicates));
            WaitForConditionUI(() => documentGrid.IsComplete);
            RunDlg<ViewEditor>(documentGrid.NavBar.CustomizeView, viewEditor =>
            {
                var ppProtein = PropertyPath.Root.Property(nameof(SkylineDocument.Proteins)).LookupAllItems();
                var ppProteinResult = ppProtein.Property(nameof(Protein.Results)).DictionaryValues();
                var ppProteinAbundance = ppProteinResult.Property(nameof(ProteinResult.Abundance));
                var ppReplicates = PropertyPath.Root.Property(nameof(SkylineDocument.Replicates)).LookupAllItems();
                var ppResultFiles = ppReplicates.Property(nameof(Replicate.Files)).LookupAllItems();
                viewEditor.ChooseColumnsTab.RemoveColumns(0, viewEditor.ChooseColumnsTab.ColumnCount);
                foreach (var propertyPath in new[]
                         {
                             ppReplicates,
                             ppResultFiles.Property(nameof(ResultFile.ExplicitGlobalStandardArea)),
                             ppResultFiles.Property(nameof(ResultFile.MedianPeakArea)),
                             ppResultFiles.Property(nameof(ResultFile.TicArea)),
                             ppResultFiles.Property(nameof(ResultFile.NormalizationDivisor)),
                             ppProtein,
                             ppProteinResult,
                             ppProteinAbundance
                         })
                {
                    viewEditor.ChooseColumnsTab.AddColumn(propertyPath);
                }
                viewEditor.ViewName = REPORTNAME_PROTEINABUNDANCES;
                viewEditor.OkDialog();
            });

        }

        private void TestNormalizationMethod(NormalizationMethod normalizationMethod) {
            AreaReplicateGraphPane areaReplicateGraphPane = null;
            PeptideGroupDocNode secondProtein = null;

            // Change the Peptide Quantification settings to use the normalization method being tested.
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Quantification;
                peptideSettingsUi.QuantNormalizationMethod = normalizationMethod;
                peptideSettingsUi.QuantMsLevel = 2;
                peptideSettingsUi.OkDialog();
            });
            Assert.AreEqual(normalizationMethod, SkylineWindow.Document.Settings.PeptideSettings.Quantification.NormalizationMethod);

            // Show the Peak Area Replicate Comparison graph, and select the normalization method being tested
            RunUI(()=>
            {
                SkylineWindow.SetDisplayTypeChrom(DisplayTypeChrom.products);
                SkylineWindow.SetNormalizationMethod(normalizationMethod);
                Settings.Default.PeakAreaDotpDisplay = DotProductDisplayOption.none.ToString();
                Settings.Default.ShowLibraryPeakArea = false;

                // Select the second protein in the document. The second protein has only one Peptide, which makes it simpler to calculate its expected
                // fold change, and compare those values to what is displayed in the Peak Area graph
                secondProtein = (PeptideGroupDocNode) SkylineWindow.Document.Children[1];
                Assert.AreEqual(1, secondProtein.Children.Count);
                SkylineWindow.SelectedPath = new IdentityPath(secondProtein.Id, secondProtein.Children[0].Id);
                SkylineWindow.ShowPeakAreaReplicateComparison();
                areaReplicateGraphPane = FindGraphPane<AreaReplicateGraphPane>();
                areaReplicateGraphPane.ExpectedVisible = AreaExpectedValue.none;
            });
            
            var groupComparison = GroupComparisonDef.EMPTY.ChangeName("GroupComparison")
                .ChangeControlAnnotation(ANNOTIONNAME_DIAGNOSIS).ChangeControlValue("AD").ChangePerProtein(true);
            var noNormalizationFoldChanges =
                GetFoldChanges(groupComparison.ChangeNormalizationMethod(NormalizationMethod.NONE));
            var normalizedFoldChanges = GetFoldChanges(groupComparison.ChangeNormalizationMethod(normalizationMethod));

            // Verify that the values displayed in the peak area graph are consistent with the fold change that the Group Comparer calculated for the
            // selected protein
            WaitForGraphs();
            RunUI(() =>
            {
                var secondProteinFoldChange = normalizedFoldChanges[new IdentityPath(secondProtein.Id)];
                Assert.AreEqual(1, secondProtein.Children.Count);
                var peptideDocNode = secondProtein.Molecules.First();
                Assert.AreEqual(1, peptideDocNode.Children.Count);
                var precursorDocNode = peptideDocNode.TransitionGroups.First();
                var productTransitions = precursorDocNode.Transitions.Where(t => !t.IsMs1).ToList();
                Assert.AreEqual(productTransitions.Count, areaReplicateGraphPane.CurveList.Count);
                var replicates = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms;
                Assert.AreEqual(2, replicates.Count);
                Assert.AreEqual("AD", replicates[0].Annotations.GetAnnotation(ANNOTIONNAME_DIAGNOSIS));
                Assert.AreEqual("PD", replicates[1].Annotations.GetAnnotation(ANNOTIONNAME_DIAGNOSIS));
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

            // Use the document grid to get the raw normalization values (i.e. Median or TIC Area) and normalization divisors for all of the Result Files.
            DocumentGridForm documentGrid = null;
            RunUI(()=>
            {
                SkylineWindow.ShowDocumentGrid(true);
                documentGrid = FindOpenForm<DocumentGridForm>();
                documentGrid.ChooseView(REPORTNAME_RESULTFILE_NORMALIZATION);
            });
            WaitForConditionUI(() => documentGrid.IsComplete);
            var rawNormalizationValues = new List<double>();
            var normalizationDivisors = new List<double>();
            RunUI(() =>
            {
                PropertyPath ppResultFiles = PropertyPath.Root.Property(nameof(Replicate.Files)).LookupAllItems();
                PropertyPath ppRawNormalizationValue;
                if (Equals(normalizationMethod, NormalizationMethod.EQUALIZE_MEDIANS))
                {
                    ppRawNormalizationValue = ppResultFiles.Property(nameof(ResultFile.MedianPeakArea));
                }
                else
                {
                    Assert.AreEqual(NormalizationMethod.TIC, normalizationMethod);
                    ppRawNormalizationValue = ppResultFiles.Property(nameof(ResultFile.TicArea));
                }
                PropertyPath ppNormalizationDivisor = ppResultFiles.Property(nameof(ResultFile.NormalizationDivisor));
                ColumnPropertyDescriptor propDescRawNormalizationValue = documentGrid.DataboundGridControl.BindingListSource
                    .ItemProperties.
                    OfType<ColumnPropertyDescriptor>().FirstOrDefault(pd => ppRawNormalizationValue.Equals(pd.PropertyPath));
                Assert.IsNotNull(propDescRawNormalizationValue);
                ColumnPropertyDescriptor propDescNormalizationDivisor = documentGrid.DataboundGridControl
                    .BindingListSource.ItemProperties.OfType<ColumnPropertyDescriptor>().FirstOrDefault(pd =>
                        ppNormalizationDivisor.Equals(pd.PropertyPath));
                Assert.IsNotNull(propDescNormalizationDivisor);
                foreach (var rowItem in documentGrid.DataboundGridControl.BindingListSource.OfType<RowItem>())
                {
                    var rawNormalizationValue = propDescRawNormalizationValue.GetValue(rowItem);
                    Assert.IsNotNull(rawNormalizationValue);
                    Assert.IsInstanceOfType(rawNormalizationValue, typeof(double));
                    rawNormalizationValues.Add((double) rawNormalizationValue);
                    var normalizationDivisor = propDescNormalizationDivisor.GetValue(rowItem);
                    Assert.IsInstanceOfType(normalizationDivisor, typeof(double));
                    Assert.IsNotNull(normalizationDivisor);
                    normalizationDivisors.Add((double) normalizationDivisor);
                }
            });

            // Verify that the Normalization Divisor values are equal to the raw normalization value (i.e. Median or TIC Area) divided by the median value across all of the replicates.
            Assert.AreEqual(SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Count, rawNormalizationValues.Count);
            Assert.AreEqual(SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Count, normalizationDivisors.Count);
            var medianMedian = rawNormalizationValues[0] / normalizationDivisors[0];
            for (int i = 0; i < rawNormalizationValues.Count; i++)
            {
                Assert.AreEqual(medianMedian, rawNormalizationValues[i] / normalizationDivisors[i], .00001);
            }


            // Use the Document Grid to get the protein abundance values for each protein
            RunUI(()=>documentGrid.ChooseView(REPORTNAME_PROTEINABUNDANCES));
            WaitForConditionUI(() => documentGrid.IsComplete);
            Assert.AreEqual(2, SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Count);
            // Since there are only two replicates in the document, we will only get two protein abundance values for each protein
            Dictionary<IdentityPath, double> proteinAbundanceNumerators =
                new Dictionary<IdentityPath, double>();
            Dictionary<IdentityPath, double> proteinAbundanceDenominators =
                new Dictionary<IdentityPath, double>();
            RunUI(() =>
            {
                var colProtein = documentGrid.FindColumn(PropertyPath.Root);
                Assert.IsNotNull(colProtein);
                PropertyPath ppProteinResult = PropertyPath.Root.Property(nameof(Protein.Results)).DictionaryValues();
                var colReplicate = documentGrid.FindColumn(ppProteinResult.Property(nameof(ProteinResult.Replicate)));
                Assert.IsNotNull(colReplicate);
                var colProteinAbundance =
                    documentGrid.FindColumn(ppProteinResult.Property(nameof(ProteinResult.Abundance)));
                Assert.IsNotNull(colProteinAbundance);
                for (int rowIndex = 0; rowIndex < documentGrid.RowCount; rowIndex++)
                {
                    var row = documentGrid.DataGridView.Rows[rowIndex];
                    var protein = (Protein) row.Cells[colProtein.Index].Value;
                    Assert.IsNotNull(protein);
                    var replicate = (Replicate) row.Cells[colReplicate.Index].Value;
                    Assert.IsNotNull(replicate);
                    var abundance = (Protein.AbundanceValue) row.Cells[colProteinAbundance.Index].Value;
                    Assert.IsNotNull(abundance);
                    var diagnosis = replicate.ChromatogramSet.Annotations.GetAnnotation(ANNOTIONNAME_DIAGNOSIS);
                    if (diagnosis == "AD")
                    {
                        proteinAbundanceDenominators.Add(protein.IdentityPath, abundance.TransitionAveraged);
                    }
                    else
                    {
                        Assert.AreEqual("PD", diagnosis);
                        proteinAbundanceNumerators.Add(protein.IdentityPath, abundance.TransitionAveraged);
                    }
                }
            });

            // Verify that when we calculate the fold changes using the Protein Abundance values we get the same fold changes
            // as the group comparison gave us
            foreach (var foldChangeResultEntry in normalizedFoldChanges)
            {
                var identityPath = foldChangeResultEntry.Key;
                Assert.IsTrue(proteinAbundanceNumerators.TryGetValue(identityPath, out double numerator),
                    "Could not find numerator for {0}", identityPath);
                Assert.IsTrue(proteinAbundanceDenominators.TryGetValue(identityPath, out double denominator),
                    "Could not find denominator for {0}", identityPath);
                var numeratorDenominatorRatio = numerator / denominator;
                Assert.AreEqual(foldChangeResultEntry.Value, numeratorDenominatorRatio, .01, "Incorrect fold change for {0}", identityPath);
            }

            // Verify that if we copy the Normalization Divisor values into the "Explicit Global Standard Area" column, and change the normalization
            // method to "Ratio to Global Standards", we get the same fold change values.
            RunUI(() =>
            {
                documentGrid.ChooseView(REPORTNAME_RESULTFILE_NORMALIZATION);
            });
            WaitForConditionUI(() => documentGrid.IsComplete);
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
            SetExplicitGlobalStandardAreas(documentGrid, normalizationDivisors);
            var normalizedToDivisorsFoldChanges =
                GetFoldChanges(groupComparison.ChangeNormalizationMethod(NormalizationMethod.GLOBAL_STANDARDS));
            Assert.AreEqual(normalizedFoldChanges.Count, normalizedToDivisorsFoldChanges.Count);
            foreach (var entry in normalizedToDivisorsFoldChanges)
            {
                var expectedFoldChange = normalizedFoldChanges[entry.Key];
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
