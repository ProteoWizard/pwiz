/*
 * Original author: Kaipo Tamura <kaipot .at. uw.edu>,
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

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class LibraryMatchViewTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestLibraryMatchView()
        {
            TestFilesZip = @"TestFunctional\LibraryMatchViewTest.zip";
            RunFunctionalTest();
        }

        private Dictionary<Identity, SpectrumPeaksInfo> _seen;

        protected override void DoTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, TestFilesZip);
            var docPath = testFilesDir.GetTestPath("MPDS.sky");

            // Open document
            RunUI(() => SkylineWindow.OpenFile(docPath));

            // Add spectral library
            var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var libraryListDlg = ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsDlg.EditLibraryList);
            RunDlg<EditLibraryDlg>(libraryListDlg.AddItem, dlg =>
            {
                dlg.LibraryName = "MPDS";
                dlg.LibraryPath = testFilesDir.GetTestPath("library.blib");
                dlg.OkDialog();
            });
            OkDialog(libraryListDlg, libraryListDlg.OkDialog);
            RunUI(() =>
            {
                Assert.AreEqual(1, peptideSettingsDlg.AvailableLibraries.Length);
                peptideSettingsDlg.SetLibraryChecked(0, true);
            });
            OkDialog(peptideSettingsDlg, peptideSettingsDlg.OkDialog);

            // Save document
            RunUI(() => SkylineWindow.SaveDocument());

            RunUI(() => Assert.IsTrue(SkylineWindow.IsGraphSpectrumVisible));
            WaitForGraphs();

            PeptideGroupDocNode[] nodePepGroups = null;
            RunUI(() => nodePepGroups = SkylineWindow.DocumentUI.PeptideGroups.Take(2).ToArray());
            int pepGroupIdx = 0, pepIdx = 0, tranGroupIdx = 0;
            _seen = new Dictionary<Identity, SpectrumPeaksInfo>();
            foreach (var nodePepGroup in nodePepGroups)
            {
                CheckNode(SrmDocument.Level.MoleculeGroups, pepGroupIdx++);
                foreach (var nodePep in nodePepGroup.Peptides)
                {
                    CheckNode(SrmDocument.Level.Molecules, pepIdx++);
                    foreach (var nodeTranGroup in nodePep.TransitionGroups)
                    {
                        CheckNode(SrmDocument.Level.TransitionGroups, tranGroupIdx++);
                    }
                }
            }
        }

        private void CheckNode(SrmDocument.Level level, int i)
        {
            SelectNode(level, i);
            WaitForGraphs();
            RunUI(() =>
            {
                var selectedNode = SkylineWindow.SelectedNode;
                TransitionGroupDocNode[] precursors = null;
                switch (level)
                {
                    case SrmDocument.Level.MoleculeGroups:
                        Assert.IsTrue(selectedNode is PeptideGroupTreeNode);
                        var nodePepGroup = ((PeptideGroupTreeNode)selectedNode).DocNode;
                        precursors = nodePepGroup.Peptides.SelectMany(pep => pep.TransitionGroups).ToArray();
                        break;
                    case SrmDocument.Level.Molecules:
                        Assert.IsTrue(selectedNode is PeptideTreeNode);
                        var nodePep = ((PeptideTreeNode)selectedNode).DocNode;
                        precursors = nodePep.TransitionGroups.ToArray();
                        break;
                    case SrmDocument.Level.TransitionGroups:
                        Assert.IsTrue(selectedNode is TransitionGroupTreeNode tranGroup);
                        var nodeTranGroup = ((TransitionGroupTreeNode)selectedNode).DocNode;
                        precursors = new[] { nodeTranGroup };
                        break;
                    case SrmDocument.Level.Transitions:
                        Assert.IsTrue(selectedNode is TransitionTreeNode tran);
                        var nodeTran = ((TransitionTreeNode)selectedNode).DocNode;
                        precursors = Array.Empty<TransitionGroupDocNode>();
                        break;
                }

                var graphSpectrum = SkylineWindow.GraphSpectrum;
                Assert.IsNotNull(precursors);
                Assert.AreEqual(precursors.Length > 1, graphSpectrum.PrecursorComboVisible);

                var spectrum = graphSpectrum.SelectedSpectrum;
                var spectrumId = spectrum.Precursor.Id;
                var spectrumPeaks = spectrum.SpectrumPeaksInfo;
                if (!_seen.TryGetValue(spectrumId, out var seenPeaks))
                {
                    _seen[spectrumId] = spectrumPeaks;
                }
                else
                {
                    Assert.AreEqual(seenPeaks, spectrumPeaks);
                }
            });
        }
    }
}
