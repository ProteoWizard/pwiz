/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that changing the "Quantitative" property of transitions has the same effect as 
    /// deleting the transitions in terms of calculating area, etc.
    /// 
    /// Note that since non-quantitative transitions do affect peak finding, it is necessary to
    /// import the original peak boundaries in order to get exactly the same results.
    /// </summary>
    [TestClass]
    public class QuantitativeTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestQuantitativeProperty()
        {
            TestFilesZip = @"TestFunctional\QuantitativeTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            OpenTestDocument();
            var exportLiveReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
            RunUI(() =>
            {
                exportLiveReportDlg.SetUseInvariantLanguage(true);
                exportLiveReportDlg.ReportName = "QuantitativeTestPeakBoundaries";
            });
            
            OkDialog(exportLiveReportDlg, ()=>exportLiveReportDlg.OkDialog(GetOriginalPeakBoundariesPath(), TextUtil.CsvSeparator));
            VerifyQuantitativeProperty("OnlyMs2", tran => !tran.IsMs1);
            VerifyQuantitativeProperty("OnlyMs1", tran => tran.IsMs1);
        }

        private void OpenTestDocument()
        {
            RunUI(() =>
            {
                SkylineWindow.NewDocument(true);
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("quantitativetest.sky"));
            });
            WaitForDocumentLoaded();
        }

        private string GetOriginalPeakBoundariesPath()
        {
            return TestFilesDir.GetTestPath("OriginalPeakBoundaries.csv");
        }

        /// <summary>
        /// Verifies that setting the "Quantititative" property on transitions has the same effect as deleting those
        /// transitions.
        /// </summary>
        private void VerifyQuantitativeProperty(string scenarioName, Predicate<TransitionDocNode> transitionPredicate)
        {
            OpenTestDocument();
            RunUI(()=>SkylineWindow.ModifyDocument("Set QuantitativeProperty", doc =>
            {
                return ReplaceTransitionGroups(doc,
                    transitionGroup => (TransitionGroupDocNode) transitionGroup.ChangeChildren(transitionGroup
                        .Transitions.Select(tran => tran.ChangeQuantitative(transitionPredicate(tran)))
                        .ToArray()));
            }));
            ImportOriginalPeakBoundaries();
            string quantitativePropertyPath = TestFilesDir.GetTestPath(scenarioName + "QuantitativeProperty.csv");
            ExportQuantitativeTestResults(quantitativePropertyPath);
            OpenTestDocument();
            RunUI(() => SkylineWindow.ModifyDocument("Delete NonQuantitative", doc =>
            {
                return ReplaceTransitionGroups(doc,
                    transitionGroup => (TransitionGroupDocNode)transitionGroup.ChangeChildren(transitionGroup
                        .Transitions.Where(tran=>transitionPredicate(tran)).ToArray()));
            }));
            ImportOriginalPeakBoundaries();
            string deleteNonQuantitativePath = TestFilesDir.GetTestPath(scenarioName + "DeleteNonQuantitative.csv");
            ExportQuantitativeTestResults(deleteNonQuantitativePath);
            AssertEx.FileEquals(quantitativePropertyPath, deleteNonQuantitativePath);
        }

        private SrmDocument ReplaceTransitionGroups(
            SrmDocument document, Func<TransitionGroupDocNode, TransitionGroupDocNode> transitionGroupTransformFunc)
        {
            var newMoleculeGroups = new List<PeptideGroupDocNode>();
            foreach (var moleculeGroup in document.MoleculeGroups)
            {
                var newMolecules = new List<PeptideDocNode>();
                foreach (var molecule in moleculeGroup.Molecules)
                {
                    var newTransitionGroups = new List<TransitionGroupDocNode>();
                    foreach (var transitionGroup in molecule.TransitionGroups)
                    {
                        var newTransitionGroup = transitionGroupTransformFunc(transitionGroup);
                        newTransitionGroups.Add(newTransitionGroup);
                    }
                    var newMolecule = (PeptideDocNode) molecule.ChangeChildren(
                        newTransitionGroups.ToArray());
                    newMolecules.Add(newMolecule);
                }
                newMoleculeGroups.Add((PeptideGroupDocNode) moleculeGroup.ChangeChildren(newMolecules.ToArray()));
            }
            return (SrmDocument) document.ChangeChildren(newMoleculeGroups.ToArray());
        }

        private void ImportOriginalPeakBoundaries()
        {
            WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.ImportPeakBoundariesFile(GetOriginalPeakBoundariesPath()));
        }

        private void ExportQuantitativeTestResults(string path)
        {
            var exportLiveReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
            RunUI(() =>
            {
                exportLiveReportDlg.SetUseInvariantLanguage(true);
                exportLiveReportDlg.ReportName = "QuantitativeTestResults";
            });

            OkDialog(exportLiveReportDlg, () => exportLiveReportDlg.OkDialog(path, ','));
        }
    }
}
