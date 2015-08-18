/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Functional test for CE Optimization.
    /// </summary>
    [TestClass]
    public class PrecursorTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPrecursorIon()
        {
            TestFilesZip = @"TestFunctional\PrecursorTest.zip";
            RunFunctionalTest();
        }

        private const string DOCUMENT_NAME = "PrecursorTest.sky";

        /// <summary>
        /// Test Skyline document sharing with libraries.
        /// </summary>
        protected override void DoTest()
        {
            // The transition list produced by the special test nodes doesn't parse as peptides, which this test expects
            TestSmallMolecules = false;

            // Open the .sky file
            string documentPath = TestFilesDir.GetTestPath(DOCUMENT_NAME);
            RunUI(() => SkylineWindow.OpenFile(documentPath));

            // Delete the last protein because its peptide has an explicit modification
            // which just gets in the way for this test.
            SelectNode(SrmDocument.Level.MoleculeGroups, SkylineWindow.Document.PeptideGroupCount - 1);
            RunUI(SkylineWindow.EditDelete);

            // Select the first precursor and inspect its graph
            SelectNode(SrmDocument.Level.TransitionGroups, 0);
            WaitForLibraries();
            WaitForGraphs();
            int ionCount = 0;
            RunUI(() => ionCount = SkylineWindow.GraphSpectrum.PeaksMatchedCount);
            RunUI(() => SkylineWindow.GraphSpectrumSettings.ShowPrecursorIon = true);
            WaitForGraphs();
            RunUI(() => Assert.AreEqual(ionCount + 1, SkylineWindow.GraphSpectrum.PeaksMatchedCount));

            string precursorPrefix = IonType.precursor.GetLocalizedString();
            string precursorLabel = precursorPrefix + Transition.GetChargeIndicator(2);

            SrmDocument docCurrent = SkylineWindow.Document;
            var pickList0 = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
            RunUI(() =>
                      {
                          Assert.IsFalse(pickList0.ItemNames.Contains(name => name.StartsWith(precursorPrefix)));
                          pickList0.ApplyFilter(false);
                          Assert.IsTrue(pickList0.ItemNames.Contains(name => name.StartsWith(precursorPrefix)));
                          pickList0.ToggleFind();
                          pickList0.SearchString = precursorPrefix;
                          pickList0.SetItemChecked(0, true);
                          pickList0.OnOk();
                      });
            WaitForDocumentChange(docCurrent);
            docCurrent = SkylineWindow.Document;
            Assert.AreEqual(IonType.precursor, new List<TransitionDocNode>(docCurrent.PeptideTransitions)[0].Transition.IonType,
                "First transition is not precursor type.");
            SelectNode(SrmDocument.Level.Transitions, 0);
            VerifySelectedIon(precursorLabel);

            SelectNode(SrmDocument.Level.TransitionGroups, 2);  // Charge 3
            docCurrent = SkylineWindow.Document;
            var pickList1 = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
            RunUI(() =>
            {
                pickList1.ApplyFilter(false);
                pickList1.SetItemChecked(0, true);
                pickList1.OnOk();
            });
            WaitForDocumentChange(docCurrent);
            RunUI(() => SkylineWindow.SaveDocument());
            RunUI(SkylineWindow.NewDocument);
            RunUI(() => SkylineWindow.OpenFile(documentPath));
            Assert.AreEqual(2, GetPrecursorTranstionCount());

            // Export a transition list
            string tranListPath = TestFilesDir.GetTestPath("TransitionList.csv");
            var exportDialog = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
            RunUI(() =>
                      {
                          exportDialog.ExportStrategy = ExportStrategy.Single;
                          exportDialog.MethodType = ExportMethodType.Standard;
                          exportDialog.OkDialog(tranListPath);
                      });
            WaitForCondition(() => File.Exists(tranListPath));

            // Save a copy of the current document
            docCurrent = SkylineWindow.Document;

            // Delete remaining 2 proteins
            SelectNode(SrmDocument.Level.MoleculeGroups, 0);
            RunUI(() =>
                      {
                          SkylineWindow.EditDelete();
                          SkylineWindow.EditDelete();
                      });

            // Paste the transition list
            SetClipboardTextUI(File.ReadAllText(tranListPath));
            RunUI(() => SkylineWindow.Paste());

            Assert.AreEqual(2, GetPrecursorTranstionCount());
            Assert.AreEqual(docCurrent.PeptideTransitionCount, SkylineWindow.Document.PeptideTransitionCount);
            Assert.AreEqual(IonType.precursor, new List<TransitionDocNode>(docCurrent.PeptideTransitions)[0].Transition.IonType,
                "First transition is not precursor type.");

            SelectNode(SrmDocument.Level.Transitions, 0);
            VerifySelectedIon(precursorLabel);
        }

        private static void VerifySelectedIon(string ionName)
        {
            WaitForGraphs();
            RunUI(() =>
                      {
                          // Unfortunately, label hiding may mean the precursor label is not present
                          string ionSelected = SkylineWindow.GraphSpectrum.SelectedIonLabel;
                          if ((ionSelected != null && ionSelected.Contains(IonType.precursor.GetLocalizedString())) ||
                                SkylineWindow.GraphSpectrum.IonLabels.Contains(IonType.precursor.GetLocalizedString()))
                          {
                              Assert.AreEqual(ionName, ionSelected);
                          }
                          else
                          {
                              Assert.IsNull(ionSelected,
                                  string.Format("Precursor label missing but selected ion label '{0}' found.", ionSelected));                              
                          }
                      });
        }

        private static int GetPrecursorTranstionCount()
        {
            int countPrecursors = 0;
            foreach (var nodeGroup in SkylineWindow.Document.PeptideTransitionGroups)
            {
                foreach (TransitionDocNode nodeTran in nodeGroup.Children)
                {
                    if (nodeTran.Transition.IsPrecursor())
                    {
                        Assert.AreSame(nodeTran.Transition.Group, nodeGroup.TransitionGroup);
                        Assert.AreEqual(nodeTran.Transition.Charge, nodeGroup.TransitionGroup.PrecursorCharge);
                        Assert.AreEqual(nodeTran.Mz, nodeGroup.PrecursorMz);
                        countPrecursors++;
                    }
                }
            }
            return countPrecursors;
        }

        private static void WaitForLibraries()
        {
            WaitForConditionUI(() => SkylineWindow.DocumentUI.Settings.PeptideSettings.Libraries.IsLoaded);
        }
    }
}
