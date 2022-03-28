/*
 * Original author: Kaipo Tamura <kaipot .at. proteinms dot net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib.Midas;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class MidasTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMidas()
        {
            TestFilesZip = @"TestFunctional\MidasTest.zip";
            RunFunctionalTest();
        }

        private static bool DisableTest => true; // Remove once test is fixed

        protected override void DoTest()
        {
            // CONSIDER(kaipot): Support for mzML?
            if (DisableTest || !ExtensionTestContext.CanImportAbWiff)
                return;

            var doc = SkylineWindow.Document;
            var documentPath = TestFilesDir.GetTestPath("102816 ApoB MIDAS testing v2.sky");
            RunUI(() => SkylineWindow.OpenFile(documentPath));
            WaitForDocumentChangeLoaded(doc);

            var wiffPath = TestFilesDir.GetTestPath("102816 Plas ApoB MIDAS testing 2.wiff");
            var askDecoysDlg = ShowDialog<MultiButtonMsgDlg>(SkylineWindow.ImportResults);
            var importResults = ShowDialog<ImportResultsDlg>(askDecoysDlg.ClickNo);
            RunUI(() =>
            {
                importResults.NamedPathSets =
                    new[] {"MIDAS1", "MIDAS2", "MIDAS3"}.Select((sampleName, index) => new KeyValuePair<string, MsDataFileUri[]>(sampleName,
                        new MsDataFileUri[] {new MsDataFilePath(wiffPath, sampleName, index)})).ToArray();
            });
            var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importResults.OkDialog);
            OkDialog(importResultsNameDlg, importResultsNameDlg.NoDialog);
            
            WaitForCondition(() => SkylineWindow.Document.Settings.PeptideSettings.Libraries.HasMidasLibrary);
            doc = SkylineWindow.Document;

            Assert.AreEqual(3, doc.Settings.MeasuredResults.Chromatograms.Count);

            var libraries = doc.Settings.PeptideSettings.Libraries;
            var midasLibs = libraries.MidasLibraries.ToArray();
            var midasLibSpecs = libraries.MidasLibrarySpecs.ToArray();
            Assert.AreEqual(1, midasLibs.Length);
            Assert.AreEqual(1, midasLibSpecs.Length);
            var midasLib = midasLibs[0];
            var midasLibSpec = midasLibSpecs[0];
            Assert.AreEqual(MidasLibSpec.PREFIX + Path.GetFileNameWithoutExtension(documentPath), midasLibSpec.Name);
            Assert.IsTrue(File.Exists(midasLibSpec.FilePath));
            Assert.AreEqual(1, midasLib.FileCount);
            Assert.AreEqual(218, midasLib.SpectrumCount);

            CheckMidasRts(570.2720, 12.9, 15.0, 26.0, 16.6);
            CheckMidasRts(506.8237, 24.6, 13.0, 14.9, 8.9, 21.5, 21.8, 14.5, 10.7, 22.5, 23.1, 13.8, 25.0);
            CheckMidasRts(704.8666, 18.1, 21.6, 25.9);
            CheckMidasRts(470.2468, 3.3, 19.5, 11.9, 19.1, 7.7);
            CheckMidasRts(567.9618, 19.1, 9.3, 14.9, 14.6, 12.9);
            CheckMidasRts(862.4725, 32.9, 12.8, 24.2, 18.6);
            CheckMidasRts(575.3174, 24.2, 15.2);
            CheckMidasRts(636.3457, 8.1, 32.8, 18.0, 18.3);
            CheckMidasRts(873.9567, 29.2, 28.9, 28.6);
            CheckMidasRts(582.9736, 24.4, 29.0, 9.3, 20.3);
            CheckMidasRts(654.8454, 13.8, 31.7, 22.3, 31.0, 30.7);
            CheckMidasRts(569.7800, 21.4, 13.8, 15.1, 16.4, 16.7);
            CheckMidasRts(816.9227, 22.2, 20.5);
            CheckMidasRts(544.9509, 6.1, 14.9, 13.7, 7.9, 17.0, 13.4, 9.0);
            CheckMidasRts(589.8139, 13.5, 15.1, 6.7, 12.4, 18.5);
            CheckMidasRts(1027.0700, 31.2, 25.8, 33.1);
            CheckMidasRts(685.0491, 20.5, 19.5, 25.7, 31.2, 16.0);
            CheckMidasRts(770.9383, 20.9, 15.0, 17.6);
            CheckMidasRts(514.2946, 17.7, 12.7, 19.3, 15.8, 19.6, 13.3, 20.0, 14.8, 14.1);
            CheckMidasRts(640.8641, 10.4, 13.9, 21.9, 20.2, 20.8, 19.8);
            CheckMidasRts(427.5785, 19.9);
            CheckMidasRts(619.8300, 14.0, 13.7, 19.8);
            CheckMidasRts(413.5558, 13.9, 13.6, 9.7);
            CheckMidasRts(691.6919, 32.7, 19.6, 21.4, 28.1);
            CheckMidasRts(535.8159, 16.5, 13.4, 11.0, 26.3, 12.5);
            CheckMidasRts(653.8557, 30.5, 27.6, 25.7, 26.1, 20.1, 22.0);
            CheckMidasRts(436.2396, 16.3);
            CheckMidasRts(687.3384, 32.4, 26.5, 32.0, 25.7, 19.7, 24.3, 20.7, 21.3);
            CheckMidasRts(458.5614, 14.2);
            CheckMidasRts(843.4157, 25.8, 26.3);
            CheckMidasRts(562.6129, 16.9, 17.5, 30.6, 26.3, 21.3, 13.6, 32.1);
            CheckMidasRts(710.0267, 29.3, 22.9, 18.73, 22.57, 23.8, 26.0, 21.9, 22.1);
            CheckMidasRts(680.8044, 26.6, 13.5, 12.3, 27.9, 12.0, 28.2, 21.2);
            CheckMidasRts(454.2054, 22.1, 6.3, 15.6, 12.1);
            CheckMidasRts(753.3939, 27.3, 11.0, 11.2);
            CheckMidasRts(502.5984, 10.3, 13.0, 8.2, 11.0, 11.5, 20.4);
            CheckMidasRts(689.3445, 24.5, 32.8, 25.6, 32.5, 27.9, 27.1, 26.8, 26.5);
            CheckMidasRts(611.8068, 31.9, 14.0, 21.5, 18.8, 21.2, 29.5, 14.3, 13.4, 13.1);
            CheckMidasRts(408.2069, 15.1, 9.0);
            CheckMidasRts(791.4172, 31.3, 25.0);
            CheckMidasRts(527.9472, 16.0, 30.0, 15.7, 5.6, 25.6, 18.3, 19.6, 16.5, 12.7, 11.1, 12.0);
            CheckMidasRts(801.9174, 28.0, 10.4, 32.8);
            CheckMidasRts(534.9473, 19.0, 16.4, 18.3, 7.0, 12.5, 6.1, 19.6, 21.6);
            CheckMidasRts(509.2489, 13.7);
            CheckMidasRts(801.4303, 18.8, 32.7, 25.7);
            CheckMidasRts(534.6226, 7.0, 18.9, 16.5);

            var peptideSettings = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                peptideSettings.SelectedTab = PeptideSettingsUI.TABS.Library;
                Assert.AreEqual(1, peptideSettings.PickedLibrarySpecs.OfType<MidasLibSpec>().Count());
                Assert.IsTrue(peptideSettings.FilterLibraryEnabled);
            });
            var filterDlg = ShowDialog<FilterMidasLibraryDlg>(peptideSettings.ShowFilterMidasDlg);
            RunUI(() =>
            {
                Assert.AreEqual("MIDAS_Filter_102816 ApoB MIDAS testing v2", filterDlg.LibraryName);
                Assert.AreEqual(TestFilesDir.GetTestPath("102816 ApoB MIDAS testing v2.midas.blib"), filterDlg.FileName);
            });
            OkDialog(filterDlg, filterDlg.OkDialog);
            OkDialog(peptideSettings, peptideSettings.OkDialog);

            // Get transition rankings
            var ranks = new Dictionary<Identity, Dictionary<double, int>>();
            foreach (var nodeTranGroup in doc.PeptideTransitionGroups)
            {
                foreach (var nodeTran in nodeTranGroup.Transitions)
                {
                    var rank = nodeTran.GetRank(null, false);
                    if (rank != null)
                    {
                        Dictionary<double, int> tranGroupRanks;
                        if (!ranks.TryGetValue(nodeTranGroup.Id, out tranGroupRanks))
                        {
                            tranGroupRanks = new Dictionary<double, int>();
                            ranks[nodeTranGroup.Id] = tranGroupRanks;
                        }
                        tranGroupRanks[nodeTran.Mz] = rank.Value;
                    }
                }
            }

            var transitionSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionSettings.SetAutoSelect = true;
                transitionSettings.UseLibraryPick = true;
                transitionSettings.IonCount = 2;
            });

            OkDialog(transitionSettings, transitionSettings.OkDialog);
            doc = WaitForDocumentChange(doc);

            foreach (var nodeTranGroup in doc.PeptideTransitionGroups)
            {
                Dictionary<double, int> tranGroupRanks;
                if (!ranks.TryGetValue(nodeTranGroup.Id, out tranGroupRanks))
                {
                    tranGroupRanks = new Dictionary<double, int>();
                }
                foreach (var nodeTran in nodeTranGroup.Transitions.Where(nodeTran => nodeTran.HasLibInfo && tranGroupRanks.ContainsKey(nodeTran.Mz)))
                {
                    var rank = nodeTran.GetRank(null, false);
                    Assert.IsTrue(rank == null || rank <= 2);
                    Assert.AreEqual(tranGroupRanks[nodeTran.Mz], rank);
                }
            }

            var manageResults = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
            var doc1 = doc;
            RunUI(() =>
            {
                manageResults.IsRemoveCorrespondingLibraries = true;
                manageResults.SelectedChromatograms = new[] { doc1.Settings.MeasuredResults.Chromatograms[2] }; // This list serves as an MRU with most recently used at tail
                manageResults.RemoveReplicates();
            });
            OkDialog(manageResults, manageResults.OkDialog);

            doc = WaitForDocumentChange(doc);
            Assert.IsFalse(doc.Settings.PeptideSettings.Libraries.HasMidasLibrary);
        }

        private void CheckMidasRts(double precursorMz, params double[] expectedRts)
        {
            if (!SelectPrecursor(precursorMz))
                Assert.Fail("Precursor {0} not found", precursorMz);
            WaitForGraphs();
            var graphChromatograms = SkylineWindow.GraphChromatograms.ToArray();
            if (graphChromatograms.Length < 1)
                Assert.Fail("Missing GraphChromatogram");
            var midasRts = (graphChromatograms.Last().MidasRetentionMsMs ?? new double[0]).ToList(); // List serves as an MRU with most recently used at tail
            foreach (var expectedRt in expectedRts)
            {
                var foundRt = false;
                for (var i = 0; i < midasRts.Count; i++)
                {
                    if (Math.Abs(midasRts[i] - expectedRt) < 0.1)
                    {
                        foundRt = true;
                        midasRts.RemoveAt(i);
                        break;
                    }
                }
                if (!foundRt)
                    Assert.Fail("Didn't find expected MIDAS retention time {0} for precursor {1}", expectedRt, precursorMz);
            }
            if (midasRts.Any())
            {
                var sb = new StringBuilder();
                sb.Append("Found unexpected MIDAS retention times for precursor {0}:");
                foreach (var midasRt in midasRts)
                    sb.Append(" " + midasRt.ToString(CultureInfo.InvariantCulture));
                Assert.Fail(sb.ToString(), precursorMz);
            }
        }

        private static bool SelectPrecursor(double mz)
        {
            var foundPrecursor = false;
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.ExpandAll();
                foreach (PeptideGroupDocNode nodePepGroup in SkylineWindow.Document.MoleculeGroups)
                {
                    foreach (PeptideDocNode nodePep in nodePepGroup.Children)
                    {
                        foreach (TransitionGroupDocNode nodeTranGroup in nodePep.Children)
                        {
                            if (Math.Abs(nodeTranGroup.PrecursorMz.Value - mz) < 0.0001)
                            {
                                SkylineWindow.SelectedPath = new IdentityPath(nodePepGroup.Id, nodePep.Id, nodeTranGroup.Id);
                                foundPrecursor = true;
                                return;
                            }
                        }
                    }
                }
            });
            return foundPrecursor;
        }
    }
}
