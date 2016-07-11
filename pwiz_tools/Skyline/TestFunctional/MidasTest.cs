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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib.Midas;
using pwiz.Skyline.Properties;
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

        protected override void DoTest()
        {
            // CONSIDER(kaipot): Support for mzML?
            if (!ExtensionTestContext.CanImportAbWiff)
                return;

            var doc = SkylineWindow.Document;
            var documentPath = TestFilesDir.GetTestPath("Bg test MIDAS.sky");
            RunUI(() => SkylineWindow.OpenFile(documentPath));
            WaitForDocumentChangeLoaded(doc);

            var wiffPath = TestFilesDir.GetTestPath("070215 BG LM 40f MIDAS 2.wiff");
            ImportResultsFile(wiffPath);
            WaitForCondition(() => SkylineWindow.Document.Settings.PeptideSettings.Libraries.HasMidasLibrary);
            doc = SkylineWindow.Document;

            Assert.AreEqual(1, doc.Settings.MeasuredResults.Chromatograms.Count);

            var libraries = doc.Settings.PeptideSettings.Libraries;
            var midasLibs = libraries.MidasLibraries.ToArray();
            var midasLibSpecs = libraries.MidasLibrarySpecs.ToArray();
            Assert.AreEqual(1, midasLibs.Length);
            Assert.AreEqual(1, midasLibSpecs.Length);
            var midasLib = midasLibs[0];
            var midasLibSpec = midasLibSpecs[0];
            Assert.AreEqual(MidasLibSpec.DEFAULT_NAME, midasLibSpec.Name);
            Assert.IsTrue(File.Exists(midasLibSpec.FilePath));
            Assert.AreEqual(1, midasLib.FileCount);
            Assert.AreEqual(10, midasLib.SpectrumCount);

            CheckMidasRts(450.6959, 4.0);
            CheckMidasRts(503.2368, 3.5);
            CheckMidasRts(542.2645, 5.2);
            CheckMidasRts(550.2802, 4.9);
            CheckMidasRts(550.7940, 2.6);
            CheckMidasRts(607.8588);
            CheckMidasRts(671.3379, 4.7);
            CheckMidasRts(697.8694);
            CheckMidasRts(714.8469, 4.7);
            CheckMidasRts(729.3652, 4.2);
            CheckMidasRts(871.9516);
            CheckMidasRts(879.4339);
            CheckMidasRts(433.8791);

            var peptideSettings = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var midasBuildCancel = ShowDialog<MultiButtonMsgDlg>(peptideSettings.ShowBuildLibraryDlg);
            Assert.AreEqual(Resources.PeptideSettingsUI_ShowBuildLibraryDlg_This_document_has_a_MIDAS_library__Would_you_like_to_build_a_spectral_library_from_it_, midasBuildCancel.Message);
            OkDialog(midasBuildCancel, midasBuildCancel.BtnCancelClick);
            var midasBuildNo = ShowDialog<MultiButtonMsgDlg>(peptideSettings.ShowBuildLibraryDlg);
            var normalBuild = ShowDialog<BuildLibraryDlg>(midasBuildNo.ClickNo);
            OkDialog(normalBuild, normalBuild.CancelDialog);
            var midasBlib = TestFilesDir.GetTestPath("midas.blib");
            RunUI(() => peptideSettings.ShowBuildLibraryDlg(midasBlib));
            WaitForConditionUI(10000, () => peptideSettings.LibraryDriver.List.Count == 2);
            RunUI(() =>
            {
                var libSpec = peptideSettings.LibraryDriver.List.Last();
                Assert.AreEqual(midasBlib, libSpec.FilePath); 
                Assert.AreEqual(MidasBlibBuilder.BLIB_NAME_SKYLINE, libSpec.Name);
            });
            // Test that the normal library build form appears if we already have a MIDAS blib file
            var normalBuild2 = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);
            OkDialog(normalBuild2, normalBuild2.CancelDialog);

            OkDialog(peptideSettings, peptideSettings.OkDialog);

            var manageResults = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
            var doc1 = doc;
            RunUI(() =>
            {
                manageResults.IsRemoveCorrespondingLibraries = true;
                manageResults.SelectedChromatograms = new[] { doc1.Settings.MeasuredResults.Chromatograms[0] };
                manageResults.RemoveReplicates();
            });
            OkDialog(manageResults, manageResults.OkDialog);

            doc = WaitForDocumentChange(doc);
            Assert.IsFalse(doc.Settings.PeptideSettings.Libraries.HasMidasLibrary);
        }

        private static void CheckMidasRts(double precursorMz, params double[] expectedRts)
        {
            if (!SelectPrecursor(precursorMz))
                Assert.Fail("Precursor {0} not found", precursorMz);
            WaitForGraphs();
            var graphChromatograms = SkylineWindow.GraphChromatograms.ToArray();
            if (graphChromatograms.Length != 1)
                Assert.Fail("Missing GraphChromatogram");
            var midasRts = graphChromatograms.First().MidasRetentionMsMs.ToList();
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
                    Assert.Fail("Didn't find expected MIDAS retention time {0}", expectedRt);
            }
            if (midasRts.Any())
            {
                var sb = new StringBuilder();
                sb.Append("Found unexpected MIDAS retention times:");
                foreach (var midasRt in midasRts)
                    sb.Append(" " + midasRt.ToString(CultureInfo.InvariantCulture));
                Assert.Fail(sb.ToString());
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
