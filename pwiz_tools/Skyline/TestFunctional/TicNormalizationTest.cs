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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;
using ZedGraph;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class TicNormalizationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestTicNormalization()
        {
            TestFilesZip = @"TestFunctional\TicNormalizationTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            const string viewName = "TicNormalizationTestView";
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("TicNormalizationTest.sky"));
                SkylineWindow.ShowDocumentGrid(true);
            });
            WaitForDocumentLoaded();
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(()=>documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Proteins));
            WaitForConditionUI(() => documentGrid.IsComplete);
            RunDlg<ViewEditor>(documentGrid.NavBar.CustomizeView, viewEditor=>
            {
                viewEditor.ChooseColumnsTab.RemoveColumns(0, viewEditor.ChooseColumnsTab.ColumnCount);
                var ppProteins = PropertyPath.Root.Property(nameof(SkylineDocument.Proteins)).LookupAllItems();
                var ppPeptides = ppProteins.Property(nameof(Protein.Peptides)).LookupAllItems();
                var ppReplicates = PropertyPath.Root.Property(nameof(SkylineDocument.Replicates)).LookupAllItems();
                var ppPeptideResult = ppPeptides.Property(nameof(Peptide.Results)).DictionaryValues();
                var ppFiles = ppReplicates.Property(nameof(Replicate.Files)).LookupAllItems();
                viewEditor.ChooseColumnsTab.AddColumn(ppPeptides);
                viewEditor.ChooseColumnsTab.AddColumn(ppReplicates);
                viewEditor.ChooseColumnsTab.AddColumn(ppFiles);
                viewEditor.ChooseColumnsTab.AddColumn(ppFiles.Property(nameof(ResultFile.TicArea)));
                viewEditor.ChooseColumnsTab.AddColumn(ppPeptideResult.Property(nameof(PeptideResult.Quantification))
                    .Property(nameof(QuantificationResult.NormalizedArea)));
                viewEditor.ChooseColumnsTab.AddColumn(ppProteins.Property(nameof(Protein.Results)).DictionaryValues().Property(nameof(ProteinResult.Abundance)));
                viewEditor.ViewName = viewName;
                viewEditor.OkDialog();
            });
            var unnormalizedAreas = ReadNormalizedAreas(documentGrid)
                .ToDictionary(tuple => Tuple.Create(tuple.Item1.IdentityPath, tuple.Item2.FilePath),
                    tuple=>tuple.Item3);
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Quantification;
                peptideSettingsUi.QuantNormalizationMethod = NormalizationMethod.TIC;
                peptideSettingsUi.OkDialog();
            });
            var medianTicArea = new Statistics(SkylineWindow.Document.MeasuredResults.Chromatograms
                .SelectMany(chromSet => chromSet.MSDataFileInfos.Select(chromFileInfo => chromFileInfo.TicArea))
                .OfType<double>()).Median();
            foreach (var tuple in ReadNormalizedAreas(documentGrid))
            {
                var key = Tuple.Create(tuple.Item1.IdentityPath, tuple.Item2.FilePath);
                string message = key.ToString();
                ResultFile resultFile = tuple.Item2;
                double? unnormalizedArea;
                Assert.IsTrue(unnormalizedAreas.TryGetValue(key, out unnormalizedArea), message);
                if (!unnormalizedArea.HasValue)
                {
                    Assert.IsNull(tuple.Item3, message);
                }
                else
                {
                    Assert.IsNotNull(tuple.Item3, message);
                    var expectedArea = unnormalizedArea * medianTicArea / resultFile.TicArea;
                    Assert.IsNotNull(expectedArea, message);
                    Assert.AreEqual(expectedArea.Value, tuple.Item3.Value, 1e-4, message);
                }
            }

            RunUI(()=>SkylineWindow.ShowPeakAreaCVHistogram());
            var graphHistogram = FindGraph<AreaCVHistogramGraphPane>();
            Assert.IsNotNull(graphHistogram);
            RunUI(() =>
            {
                var areaCvToolbar = (AreaCVToolbar) graphHistogram.Toolbar;
                int indexTic = areaCvToolbar.NormalizationMethods.ToList()
                    .IndexOf(NormalizationMethod.TIC.NormalizeToCaption);
                Assert.IsTrue(indexTic >= 0);
                areaCvToolbar.SetNormalizationIndex(indexTic);
                SkylineWindow.UpdateGraphPanes();
            });
            WaitForGraphs();
            AreaCVHistogramGraphPane areaCvHistogramGraphPane;
            Assert.IsTrue(graphHistogram.TryGetGraphPane(out areaCvHistogramGraphPane));
            WaitForConditionUI(() => areaCvHistogramGraphPane.CurrentData != null);
            foreach (var cvData in areaCvHistogramGraphPane.CurrentData.Data)
            {
                foreach (var peptideAnnotationPair in cvData.PeptideAnnotationPairs)
                {
                    var values = new List<double>();
                    var results = peptideAnnotationPair.TransitionGroup.Results;
                    for (int replicateIndex = 0; replicateIndex < results.Count; replicateIndex++)
                    {
                        foreach (var transitionGroupChromInfo in results[replicateIndex])
                        {
                            if (!transitionGroupChromInfo.Area.HasValue)
                            {
                                continue;
                            }
                            var fileInfo = SkylineWindow.Document.MeasuredResults.Chromatograms[replicateIndex]
                                .GetFileInfo(transitionGroupChromInfo.FileId);
                            Assert.IsNotNull(fileInfo);
                            Assert.IsNotNull(fileInfo.TicArea);
                            values.Add(transitionGroupChromInfo.Area.Value / fileInfo.TicArea.Value);
                        }
                    }
                    var stats = new Statistics(values);
                    var expectedCV = stats.StdDev() / stats.Mean();
                    Assert.AreEqual(expectedCV, peptideAnnotationPair.CVRaw, .001, 
                        "TransitionGroup: {0}", peptideAnnotationPair.TransitionGroup);
                }
            }

            Settings.Default.PeakAreaDotpDisplay = DotProductDisplayOption.none.ToString();
            RunUI(SkylineWindow.ShowPeakAreaReplicateComparison);
            RunUI(() =>
            {
                SkylineWindow.SetDisplayTypeChrom(DisplayTypeChrom.single);
                SkylineWindow.SelectedPath =
                    SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.TransitionGroups, 0);
            });
            WaitForGraphs();
            var areaReplicateGraphPane = FindGraph<AreaReplicateGraphPane>();
            RunUI(() =>
            {
                var transitionGroup = SkylineWindow.Document.MoleculeTransitionGroups.First();
                var graphPane = areaReplicateGraphPane.GraphControl.GraphPane;
                var textLabels = graphPane.XAxis.Scale.TextLabels;
                var chromatogramSets = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms;
                Assert.AreEqual(Resources.AreaReplicateGraphPane_InitFromData_Expected, textLabels[0]);
                foreach (var curve in graphPane.CurveList)
                {
                    var identityPath = curve.Tag as IdentityPath;
                    Assert.IsNotNull(identityPath);
                    var transition = transitionGroup.Transitions.FirstOrDefault(t => ReferenceEquals(t.Id, identityPath.Child));
                    Assert.IsNotNull(transition);
                    for (int i = 1; i < textLabels.Length; i++)
                    {
                        var replicateIndex = i - 1;
                        var chromatogramSet = chromatogramSets[replicateIndex];
                        var chromFileInfo = chromatogramSet.MSDataFileInfos[0];
                        Assert.IsTrue(replicateIndex >= 0);
                        var actualValue = curve.Points[i].Y;
                        var rawArea = transition.Results[replicateIndex].FirstOrDefault()?.Area;
                        var expectedValue = rawArea * medianTicArea / chromFileInfo.TicArea;
                        if (!expectedValue.HasValue)
                        {
                            Assert.AreEqual(PointPairBase.Missing, actualValue);
                        }
                        else
                        {
                            Assert.AreEqual(expectedValue.Value, actualValue, expectedValue.Value / 1e6);
                        }
                    }
                }
            });
        }

        IEnumerable<Tuple<Peptide, ResultFile, double?>> ReadNormalizedAreas(DocumentGridForm documentGrid)
        {
            WaitForConditionUI(() => documentGrid.IsComplete);
            var list = new List<Tuple<Peptide, ResultFile, double?>>();
            PropertyPath ppPeptideResults = PropertyPath.Root.Property(nameof(Peptide.Results)).DictionaryValues();
            RunUI(() =>
            {
                var colPeptide = documentGrid.FindColumn(PropertyPath.Root);
                var colResultFile =
                    documentGrid.FindColumn(ppPeptideResults.Property(nameof(PeptideResult.ResultFile)));
                var colNormalizedArea = documentGrid.FindColumn(ppPeptideResults
                    .Property(nameof(PeptideResult.Quantification))
                    .Property(nameof(QuantificationResult.NormalizedArea)));
                var colProteinAbundance = documentGrid.FindColumn(ppPeptideResults
                    .Property(nameof(PeptideResult.ProteinResult)).Property(nameof(ProteinResult.Abundance)));
                Assert.IsNotNull(colProteinAbundance);
                for (int iRow = 0; iRow < documentGrid.RowCount; iRow++)
                {
                    var row = documentGrid.DataGridView.Rows[iRow];
                    list.Add(Tuple.Create((Peptide) row.Cells[colPeptide.Index].Value,
                        (ResultFile) row.Cells[colResultFile.Index].Value,
                        (double?) row.Cells[colNormalizedArea.Index].Value));
                }
            });
            return list;
        }

        static GraphSummary FindGraph<TGraphPane>() where TGraphPane : SummaryGraphPane
        {
            GraphSummary result = null;
            RunUI(() =>
            {
                foreach (var form in FormUtil.OpenForms.OfType<GraphSummary>())
                {
                    TGraphPane graphPane;
                    if (form.TryGetGraphPane(out graphPane))
                    {
                        result = form;
                        break;
                    }
                }
            });
            return result;
        }
    }
}
