/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;


namespace pwiz.SkylineTestTutorial
{
    /// <summary>
    /// Testing the tutorial for Skyline Targeted Method Refinement
    /// </summary>
    [TestClass]
    public class MethodRefinementTutorialTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMethodRefinementTutorial()
        {
            string supplementZip = (ExtensionTestContext.CanImportThermoRaw ?
                @"https://brendanx-uw1.gs.washington.edu/tutorials/MethodRefineSupplement.zip" :
                @"https://brendanx-uw1.gs.washington.edu/tutorials/MethodRefineSupplementMzml.zip");

            // Need to deal with this issue.
            TestFilesZipPaths = new[] { supplementZip,
                    @"https://brendanx-uw1.gs.washington.edu/tutorials/MethodRefine.zip" 
                 };
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
                  {
                      SkylineWindow.OpenFile(TestFilesDirs[1].GetTestPath(@"MethodRefine\WormUnrefined.sky"));
                      SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[0];
                  });
           RunDlg<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List), exportDlg =>
                {
                    exportDlg.ExportStrategy = ExportStrategy.Buckets;
                    exportDlg.MethodType = ExportMethodType.Standard;
                    exportDlg.OptimizeType = ExportOptimize.NONE;
                    exportDlg.MaxTransitions = 59;
                    exportDlg.OkDialog(TestFilesDirs[1].GetTestPath(@"MethodRefine\worm"));
                });
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
                 {
                     manageResultsDlg.Remove();
                     manageResultsDlg.OkDialog();
                 });
            RunUI(() => SkylineWindow.SaveDocument());
            RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg =>
                  {
                      importResultsDlg.RadioAddNewChecked = true;
                      importResultsDlg.NamedPathSets = ImportResultsDlg.GetDataSourcePathsDir(TestFilesDirs[0].FullPath).Take(15).ToArray();
                      importResultsDlg.NamedPathSets[0] = 
                          new KeyValuePair<string, string[]>("Unrefined", importResultsDlg.NamedPathSets[0].Value);  
                      importResultsDlg.OptimizationName = ExportOptimize.CE;
                      importResultsDlg.OkDialog();
                  });
            WaitForCondition(() => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);
            RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg =>
            {
                importResultsDlg.RadioAddExistingChecked = true;
                importResultsDlg.NamedPathSets = ImportResultsDlg.GetDataSourcePathsDir(TestFilesDirs[0].FullPath).Skip(15).ToArray();
                importResultsDlg.OptimizationName = ExportOptimize.CE;
                importResultsDlg.OkDialog();
            });
            WaitForCondition(() => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);
            RunUI(() =>
                      {
                          SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[0];
                          SkylineWindow.AutoZoomNone();
                          SkylineWindow.AutoZoomBestPeak();
                          SkylineWindow.EditDelete();
                          SkylineWindow.ShowRTLinearRegressionGraph();
                      });
            RunDlg<ShowRTThresholdDlg>(SkylineWindow.ShowRTThresholdDlg, rtThresholdDlg =>
                     {
                         rtThresholdDlg.Threshold = 0.95;
                         rtThresholdDlg.OkDialog();
                     });
            WaitForConditionUI(() => SkylineWindow.RTGraphController.RegressionRefined != null);
            RunDlg<EditRTDlg>(SkylineWindow.CreateRegression, editRTDlg => editRTDlg.OkDialog());

            RunUI(() =>
                      {
                          SkylineWindow.RTGraphController.SelectPeptide(SkylineWindow.Document.GetPathTo(1, 163));
                          Assert.AreEqual("YLAEVASEDR", SkylineWindow.SequenceTree.SelectedNode.Text);
                          var nodePep = (PeptideDocNode) ((SrmTreeNode) SkylineWindow.SequenceTree.SelectedNode).Model;
                          Assert.AreEqual(null,
                                          nodePep.GetPeakCountRatio(
                                              SkylineWindow.SequenceTree.GetDisplayResultsIndex(nodePep)));
                          SkylineWindow.RTGraphController.GraphSummary.DocumentUIContainer.FocusDocument();
                          SkylineWindow.SequenceTree.SelectedPath = SkylineWindow.Document.GetPathTo(1, 157);
                          Assert.AreEqual("VTVVDDQSVILK", SkylineWindow.SequenceTree.SelectedNode.Text);
                        });    
            WaitForGraphs();
            RunUI(() =>
                      {
                          var graphChrom = SkylineWindow.GetGraphChromatogram("Unrefined");
                          Assert.AreEqual(2, graphChrom.Files.Count);
                          SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0];
                          SkylineWindow.RTGraphController.GraphSummary.Close();
                          SkylineWindow.ExpandPeptides();
                          SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[0];
                          Assert.IsTrue(SkylineWindow.SequenceTree.SelectedNode.Nodes[0].Text.Contains("0.78"));
                          SkylineWindow.EditDelete();
                          SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[0];
                          Assert.IsTrue(SkylineWindow.SequenceTree.SelectedNode.Nodes[0].Text.Contains("0.63"));
                          SkylineWindow.EditDelete();
                          for (int i = 0; i < 3; i++)
                          {
                              PeptideTreeNode nodePep = (PeptideTreeNode) SkylineWindow.SequenceTree.Nodes[0].Nodes[i];
                              nodePep.ExpandAll();
                              foreach(TransitionTreeNode nodeTran in nodePep.Nodes[0].Nodes)
                              {
                                 TransitionDocNode nodeTranDoc = (TransitionDocNode) nodeTran.Model;
                                 Assert.AreEqual((int) SequenceTree.StateImageId.peak, 
                                      TransitionTreeNode.GetPeakImageIndex(nodeTranDoc, 
                                      (PeptideDocNode) nodePep.Model, SkylineWindow.SequenceTree));
                                  var resultsIndex = SkylineWindow.SequenceTree.GetDisplayResultsIndex(nodePep);
                                  var rank = nodeTranDoc.GetPeakRank(resultsIndex);
                                  if(rank == null || rank > 3)
                                      SkylineWindow.SequenceTree.SelectedNode = nodeTran;
                                  SkylineWindow.SequenceTree.KeysOverride = Keys.Control;
                              }
                          }
                          SkylineWindow.SequenceTree.KeysOverride = null;
                          SkylineWindow.EditDelete();
                          for (int i = 0; i < 3; i++)
                              Assert.IsTrue(SkylineWindow.SequenceTree.Nodes[0].Nodes[i].Nodes[0].Nodes.Count == 3);
                          SkylineWindow.AutoZoomNone();
                        });
            RunDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, refineDlg =>
               {
                   refineDlg.MaxTransitionPeakRank = "3";
                   refineDlg.PreferLargerIons = true;
                   refineDlg.RemoveMissingResults = true;
                   refineDlg.RTRegressionThreshold = "0.95";
                   refineDlg.DotProductThreshold = "0.95";
                   refineDlg.OkDialog();
               });
            WaitForCondition(() => SkylineWindow.Document.PeptideCount == 72);
            RunUI(() =>
                  {
                      Assert.AreEqual(72, SkylineWindow.Document.PeptideCount);
                      Assert.AreEqual(216, SkylineWindow.Document.TransitionCount);
                      SkylineWindow.CollapsePeptides();
                      SkylineWindow.Undo();
                  });
            RunDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, refineDlg =>
               {
                   refineDlg.MaxTransitionPeakRank = "6";
                   refineDlg.RemoveMissingResults = true;
                   refineDlg.RTRegressionThreshold = "0.90";
                   refineDlg.DotProductThreshold = "0.90";
                   refineDlg.OkDialog();
               });
            WaitForCondition(() => SkylineWindow.Document.PeptideCount == 110);
            RunUI(() =>
              {
                  Assert.AreEqual(110, SkylineWindow.Document.PeptideCount);
                  SkylineWindow.Undo();
              });
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
                {
                    manageResultsDlg.Remove();
                    manageResultsDlg.OkDialog();
                });
            //var importResultsDlg0 = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            //RunUI(() =>
            //          {
            //              importResultsDlg0.RadioCreateMultipleMultiChecked = true;
            //              importResultsDlg0.NamedPathSets = importResultsDlg0.GetDataSourcePathsDir();
            //          });
            //var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importResultsDlg0.OkDialog);
            //RunUI(importResultsNameDlg.NoDialog);
            //WaitForDocumentChange(SkylineWindow.Document);
        }
    }
}
