/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests doing things to a document that cause the total global standard area to change,
    /// and makes sure that the values get updated in all of the places that they need to be.
    /// That is, makes sure that <see cref="SrmDocument.UpdateResultsSummaries" /> gets called
    /// all the times that it needs to be.
    /// </summary>
    [TestClass]
    public class UpdateGlobalStandardTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestUpdateGlobalStandard()
        {
            TestFilesZip = @"TestFunctional\UpdateGlobalStandardTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("UpdateGlobalStandardTest.sky")));
            WaitForDocumentLoaded();
            foreach (var fileId in GetAllChromFileInfoIds(SkylineWindow.Document))
            {
                AssertValuesEqual(0, CalculateGlobalStandardArea(SkylineWindow.Document, fileId));
            }
            VerifyCalculatedAreas();

            // Set the first peptide in the document to be the global standard
            IdentityPath globalStandardPath = null;
            RunUI(()=>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Molecules, 0);
                SkylineWindow.SetStandardType(StandardType.GLOBAL_STANDARD);
                globalStandardPath = SkylineWindow.SelectedPath;
            });
            Assert.AreEqual(StandardType.GLOBAL_STANDARD, ((PeptideDocNode) SkylineWindow.Document.FindNode(globalStandardPath)).GlobalStandardType);
            foreach (var fileId in GetAllChromFileInfoIds(SkylineWindow.Document))
            {
                Assert.AreNotEqual(0, CalculateGlobalStandardArea(SkylineWindow.Document, fileId));
            }
            VerifyCalculatedAreas();
            RunUI(SkylineWindow.Undo);
            VerifyCalculatedAreas();
            RunUI(SkylineWindow.Redo);
            VerifyCalculatedAreas();


            // Choose different peaks for the global standard peptide, and make sure that everyone's are gets
            // updated appropriately.
            RunUI(()=>Assert.AreEqual(globalStandardPath, SkylineWindow.SelectedPath));
            foreach (var graphChromatogram in SkylineWindow.GraphChromatograms)
            {
                WaitForGraphs();
                var chromGroupInfo = graphChromatogram.ChromGroupInfos[0];
                for (int iPeak = 0; iPeak < chromGroupInfo.NumPeaks; iPeak++)
                {
                    var globalStandardPeptide = (PeptideDocNode) SkylineWindow.Document.FindNode(globalStandardPath);
                    var transitionGroup = globalStandardPeptide.TransitionGroups.First();
                    var transition = transitionGroup.Transitions.First();
                    var chromInfo = chromGroupInfo.GetAllTransitionInfo(transition, 0, null, TransformChrom.raw)
                        .GetChromatogramForStep(0);
                    var peak = chromInfo.Peaks.Skip(iPeak).First();
                    RunUI(()=>graphChromatogram.FirePickedPeak(transitionGroup, transition, new ScaledRetentionTime(peak.RetentionTime)));
                    VerifyCalculatedAreas();
                }
            }

            SetNormalizationMethodsInDocumentGrid();

            var normalizedAreasForm = FindNormalizedAreasForm();
            if (normalizedAreasForm != null)
            {
                OkDialog(normalizedAreasForm, normalizedAreasForm.Close);
            }
        }

        /// <summary>
        /// Use the document grid to change the StandardType and NormalizationMethod values for
        /// several peptides in the document.
        /// </summary>
        private void SetNormalizationMethodsInDocumentGrid()
        {
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = FormUtil.OpenForms.OfType<DocumentGridForm>()
                .FirstOrDefault(form => form.ShowViewsMenu);
            Assert.IsNotNull(documentGrid);
            RunUI(() => documentGrid.ChooseView("NormalizationMethods"));
            WaitForConditionUI(() => documentGrid.IsComplete);
            var colModifiedSequence =
                documentGrid.FindColumn(PropertyPath.Root.Property("ModifiedSequence").Property("MonoisotopicMasses"));
            Assert.IsNotNull(colModifiedSequence);
            string[] modifiedSequences = null;
            RunUI(() =>
            {
                modifiedSequences = documentGrid.DataGridView.Rows.OfType<DataGridViewRow>()
                    .Select(row => row.Cells[colModifiedSequence.Index].Value.ToString()).ToArray();
            });
            string[] expectedPeptides =
            {
                "GNPTVEVELTTEK",
                "SIVPSGASTGVHEALEMR",
                "NVNDVIAPAFVK",
                "AVDDFLISLDGTANK",
                "LGANAILGVSLAASR",
                "TSPYVLPVPFLNVLNGGSHAGGALALQEFMIAPTGAK",
                "IGSEVYHNLK",
                "YGASAGNVGDEGGVAPNIQTAEEALDLIVDAIK",
                "IGLDC[+57.021464]ASSEFFK",
                "WLTGPQLADLYHSLMK",
                "YPIVSIEDPFAEDDWEAWSHFFK",
                "TAGIQIVADDLTVTNPK",
                "VNQIGTLSESIK",
                "SGETEDTFIADLVVGLR",
                "IEEELGDNAVFAGENFHHGDK",
            };
            CollectionAssert.AreEqual(expectedPeptides, modifiedSequences);
            StandardType[] newStandardTypes =
            {
                null,
                null,
                StandardType.GLOBAL_STANDARD, // NVNDVIAPAFVK
                StandardType.SURROGATE_STANDARD, // AVDDFLISLDGTANK
                null,
                null,
                null,
                null,
                StandardType.SURROGATE_STANDARD, // IGLDC[+57.021464]ASSEFFK
                null,
                null,
                StandardType.GLOBAL_STANDARD, // TAGIQIVADDLTVTNPK
                null,
                null,
                null
            };
            Assert.AreEqual(expectedPeptides.Length, newStandardTypes.Length);
            var colStandardType = documentGrid.FindColumn(PropertyPath.Root.Property("StandardType"));
            Assert.IsNotNull(colStandardType);
            RunUI(() =>
            {
                documentGrid.DataGridView.CurrentCell =
                    documentGrid.DataGridView.Rows[0].Cells[colStandardType.Index];
                string strStandardTypes = string.Join(Environment.NewLine, newStandardTypes.Cast<object>()
                    .Select(standardType => (standardType ?? string.Empty).ToString()));
                ClipboardEx.SetText(strStandardTypes);
                documentGrid.DataGridView.SendPaste();
            });
            VerifyCalculatedAreas();
            RunUI(SkylineWindow.Undo);
            VerifyCalculatedAreas();
            RunUI(SkylineWindow.Redo);
            VerifyCalculatedAreas();

            WaitForConditionUI(() => documentGrid.IsComplete);
            NormalizationMethod[] normalizationMethods =
            {
                new NormalizationMethod.RatioToSurrogate("IGLDC[+57.021464]ASSEFFK"),
                NormalizationMethod.GLOBAL_STANDARDS,
                null,
                null,
                new NormalizationMethod.RatioToSurrogate("AVDDFLISLDGTANK")
            };
            var colNormalizationMethod = documentGrid.FindColumn(PropertyPath.Root.Property("NormalizationMethod"));
            Assert.IsNotNull(colNormalizationMethod);
            RunUI(() =>
            {
                documentGrid.DataGridView.CurrentCell =
                    documentGrid.DataGridView.Rows[0].Cells[colNormalizationMethod.Index];
                string strNormalizationMethods = string.Join(Environment.NewLine, normalizationMethods.Cast<object>()
                    .Select(normalizationMethod => (normalizationMethod ?? string.Empty).ToString()));
                ClipboardEx.SetText(strNormalizationMethods);
                documentGrid.DataGridView.SendPaste();
            });
            var actualNormalizationMethods = SkylineWindow.Document.Molecules.Select(mol => mol.NormalizationMethod)
                .ToArray();
            var expectedNormalizationMethods = normalizationMethods.Concat(Enumerable.Repeat((NormalizationMethod) null,
                actualNormalizationMethods.Length - normalizationMethods.Length)).ToArray();
            CollectionAssert.AreEqual(expectedNormalizationMethods, actualNormalizationMethods);
            VerifyCalculatedAreas();
        }

        /// <summary>
        /// Verify that every ChromInfo in the document has the right values.
        /// </summary>
        private void VerifyCalculatedAreas()
        {
            var document = SkylineWindow.Document;
            foreach (var chromFileInfoId in GetAllChromFileInfoIds(document))
            {
                var globalStandardArea = CalculateGlobalStandardArea(document, chromFileInfoId);
                foreach (var peptide in document.Molecules)
                {
                    VerifyTotalAreas(peptide, chromFileInfoId);
                    var peptideChromInfo = FindChromInfo(peptide.Results, chromFileInfoId);
                    if (peptideChromInfo == null)
                    {
                        continue;
                    }
                    foreach (var labelRatio in peptideChromInfo.LabelRatios)
                    {
                        Assert.IsNotNull(labelRatio.LabelType);
                        double numerator = GetLabelArea(peptide, labelRatio.LabelType, chromFileInfoId);
                        double denominator;
                        if (null == labelRatio.StandardType)
                        {
                            denominator = globalStandardArea;
                        }
                        else
                        {
                            denominator = GetLabelArea(peptide, labelRatio.StandardType, chromFileInfoId);
                        }
                        var transitionGroups = peptide.TransitionGroups
                            .Where(tg => Equals(tg.LabelType, labelRatio.LabelType)).ToArray();
                        if (labelRatio.Ratio == null)
                        {
                            Assert.IsTrue(0 == transitionGroups.Length || 0 == denominator);
                        }
                        else 
                        {
                            Assert.AreNotEqual(0, transitionGroups.Length);
                            AssertValuesEqual(numerator / denominator, labelRatio.Ratio.Ratio);
                        }
                    }
                }
            }
            VerifyNormalizedAreaForm();
        }

        /// <summary>
        /// Examine the values in the DocumentGrid which is showing the "NormalizedAreas" form,
        /// and make sure that all the numbers are correct.
        /// </summary>
        private void VerifyNormalizedAreaForm()
        {
            var normalizedAreasGrid = EnsureNormalizedAreasForm();
            var dataGridView = normalizedAreasGrid.DataGridView;
            WaitForConditionUI(() => normalizedAreasGrid.IsComplete);
            var document = SkylineWindow.Document;
            Assert.AreSame(document, ((SkylineDataSchema)normalizedAreasGrid.ViewInfo.DataSchema).Document);
            RunUI(() =>
            {
                var colPeptideLocator = FindColumn(dataGridView, "PeptideLocator");
                var colResultFileLocator = FindColumn(dataGridView, "ResultFileLocator");
                var colRatioLightToGlobalStandard = FindColumn(dataGridView, "RatioLightToGlobalStandards");
                var colNormalizedArea = FindColumn(dataGridView, "NormalizedArea");
                for (int iRow = 0; iRow < dataGridView.Rows.Count; iRow++)
                {
                    var row = dataGridView.Rows[iRow];
                    var moleculeRef = (MoleculeRef)
                        ElementRefs.FromObjectReference(
                            ElementLocator.Parse((string) row.Cells[colPeptideLocator.Index].Value));
                    var peptideDocNode = (PeptideDocNode) moleculeRef.FindNode(document);
                    var resultFileRef = (ResultFileRef)
                        ElementRefs.FromObjectReference(
                            ElementLocator.Parse((string) row.Cells[colResultFileLocator.Index].Value));
                    var chromFileInfo = document.Settings.MeasuredResults.Chromatograms
                        .SelectMany(c => c.MSDataFileInfos)
                        .FirstOrDefault(fileInfo => resultFileRef.Matches(fileInfo.FilePath));
                    Assert.IsNotNull(chromFileInfo);
                    var peptideChromInfo = FindChromInfo(peptideDocNode.Results, chromFileInfo.FileId);
                    var ratioToGlobalStandard = (double?) row.Cells[colRatioLightToGlobalStandard.Index].Value;
                    double? normalizedArea = (double?)row.Cells[colNormalizedArea.Index].Value;
                    var normalizationMethod = peptideDocNode.NormalizationMethod;
                    double expectedNormalizedArea = double.NaN;
                    double totalArea = peptideDocNode.TransitionGroups.Sum(tg =>
                        FindChromInfo(tg.Results, chromFileInfo.FileId)?.Area ?? 0);
                    if (normalizationMethod == null)
                    {
                        expectedNormalizedArea = totalArea;
                    }
                    else if (NormalizationMethod.GLOBAL_STANDARDS.Equals(normalizationMethod))
                    {
                        expectedNormalizedArea = ratioToGlobalStandard ?? 0;
                    }
                    else if (normalizationMethod is NormalizationMethod.RatioToSurrogate)
                    {
                        var ratioToSurrogate = (NormalizationMethod.RatioToSurrogate) normalizationMethod;
                        double surrogateArea = CalculateStandardArea(document, chromFileInfo.FileId,
                            peptide => peptide.ModifiedTarget.InvariantName == ratioToSurrogate.SurrogateName);
                        expectedNormalizedArea = totalArea / surrogateArea;
                    }
                    else
                    {
                        Assert.Fail("Unexpected normalization method {0}", normalizationMethod);
                    }
                    AssertValuesEqual(expectedNormalizedArea, normalizedArea ?? 0);
                    if (peptideChromInfo == null)
                    {
                        Assert.IsNull(ratioToGlobalStandard);
                        Assert.IsNull(normalizedArea);
                    }
                    else
                    {
                        var ratioValue = peptideChromInfo.LabelRatios.FirstOrDefault(ratio =>
                            IsotopeLabelType.light.Equals(ratio.LabelType) && null == ratio.StandardType).Ratio;
                        if (ratioValue == null)
                        {
                            Assert.IsNull(ratioToGlobalStandard);
                        }
                        else
                        {
                            Assert.IsNotNull(normalizedArea);
                            AssertValuesEqual(ratioValue.Ratio, ratioToGlobalStandard.Value);
                        }
                    }

                }
            });
        }

        private DataGridViewColumn FindColumn(DataGridView dataGridView, string caption)
        {
            var column = dataGridView.Columns.OfType<DataGridViewColumn>()
                .FirstOrDefault(col => caption == col.HeaderText);
            return column;
        }

        private double GetLabelArea(PeptideDocNode peptideDocNode, IsotopeLabelType labelType, ChromFileInfoId chromFileInfoId)
        {
            return peptideDocNode.TransitionGroups.Where(tg => Equals(labelType, tg.LabelType))
                .Sum(tg => FindChromInfo(tg.Results, chromFileInfoId)?.Area ?? 0);
        }

        private double CalculateGlobalStandardArea(SrmDocument document, ChromFileInfoId chromFileInfoId)
        {
            return CalculateStandardArea(document, chromFileInfoId,
                peptide => peptide.GlobalStandardType == StandardType.GLOBAL_STANDARD);
        }

        private double CalculateStandardArea(SrmDocument document, ChromFileInfoId chromFileInfoId, Predicate<PeptideDocNode> predicate)
        {
            double result = 0;
            foreach (var peptide in document.Molecules)
            {
                if (!predicate(peptide))
                {
                    continue;
                }
                VerifyTotalAreas(peptide, chromFileInfoId);
                foreach (var transitionGroup in peptide.TransitionGroups)
                {
                    var transitionGroupChromInfo = FindChromInfo(transitionGroup.Results, chromFileInfoId);
                    result += transitionGroupChromInfo?.Area ?? 0;
                }
            }
            return result;
        }

        private static IEnumerable<ChromFileInfoId> GetAllChromFileInfoIds(SrmDocument document)
        {
            var all = document.Molecules.SelectMany(peptideDocNode=>GetChromFileInfoIds(peptideDocNode.Results)
                .Concat(peptideDocNode.TransitionGroups.SelectMany(tg => GetChromFileInfoIds(tg.Results)
                    .Concat(tg.Transitions.SelectMany(t => GetChromFileInfoIds(t.Results))))));
            return all.Distinct(new IdentityEqualityComparer<ChromFileInfoId>());
        }

        private static IEnumerable<ChromFileInfoId> GetChromFileInfoIds<TItem>(Results<TItem> results) where TItem : ChromInfo
        {
            if (results == null)
            {
                return new ChromFileInfoId[0];
            }
            return results.SelectMany(result => result.Select(chromInfo => chromInfo.FileId));
        }

        private static TItem FindChromInfo<TItem>(Results<TItem> results, ChromFileInfoId fileId) where TItem : ChromInfo
        {
            if (results == null)
            {
                return null;
            }
            return results.SelectMany(r => r).FirstOrDefault(chromInfo => ReferenceEquals(chromInfo.FileId, fileId));
        }

        private DocumentGridForm FindNormalizedAreasForm()
        {
            return FormUtil.OpenForms.OfType<DocumentGridForm>().FirstOrDefault(form => form.ViewInfo.Name == "NormalizedAreas");
        }

        /// <summary>
        /// Do File > Export Report > Preview to show the view "NormalizedAreas".
        /// This view gets shown with the Invariant language, so all of the columns have US names and all
        /// of the numbers are full precision.
        /// This view stays open for the duration of the test.
        /// </summary>
        private DocumentGridForm EnsureNormalizedAreasForm()
        {
            var form = FindNormalizedAreasForm();
            if (form == null)
            {
                var exportReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
                RunUI(() =>
                {
                    exportReportDlg.SetUseInvariantLanguage(true);
                    exportReportDlg.ReportName = "NormalizedAreas";
                    exportReportDlg.ShowPreview();
                });
                form = FindNormalizedAreasForm();
                Assert.IsNotNull(form);
                OkDialog(exportReportDlg, exportReportDlg.CancelClick);
            }
            return form;
        }

        /// <summary>
        /// Verifies that the total area on the TransitionGroupChromInfo is equal to the sum of the
        /// areas on the TransitionChromInfo.
        /// </summary>
        public static void VerifyTotalAreas(PeptideDocNode peptideDocNode, ChromFileInfoId chromFileInfoId)
        {
            foreach (var transitionGroup in peptideDocNode.TransitionGroups)
            {
                var precursorTotalArea = 0.0;
                var precursorTotalBackground = 0.0;
                foreach (var transition in transitionGroup.Transitions)
                {
                    var transitionChromInfo = FindChromInfo(transition.Results, chromFileInfoId);
                    if (transitionChromInfo != null)
                    {
                        precursorTotalArea += transitionChromInfo.Area;
                        precursorTotalBackground += transitionChromInfo.BackgroundArea;
                    }
                }
                var precursorChromInfo = FindChromInfo(transitionGroup.Results, chromFileInfoId);
                AssertValuesEqual(precursorTotalArea, precursorChromInfo?.Area??0);
                AssertValuesEqual(precursorTotalBackground, precursorChromInfo?.BackgroundArea??0);
            }
        }

        public static void AssertValuesEqual(double value1, double value2)
        {
            double delta = Math.Min(Math.Abs(value1), Math.Abs(value2)) / 100;
            Assert.AreEqual(value1, value2, delta);
        }
    }
}
