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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests creating an ion mobility library with entries for crosslinked peptides.
    /// </summary>
    [TestClass]
    public class CrosslinkImsTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestCrosslinkIms()
        {
            TestFilesZip = @"TestFunctional\CrosslinkImsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("CrosslinkImsTest.sky")));
            ImportResultsFile(TestFilesDir.GetTestPath("CrosslinkImsTest.mzML"));
            bool transitionSettingsClosed = false;
            var transitionSettingsUi = ShowDialog<TransitionSettingsUI>(()=>
            {
                SkylineWindow.ShowTransitionSettingsUI();
                transitionSettingsClosed = true;
            });
            SetIonMobilityResolvingPowerUI(transitionSettingsUi, 50);
            RunDlg<EditIonMobilityLibraryDlg>(() => transitionSettingsUi.IonMobilityControl.AddIonMobilityLibrary(), ionMobilityLibraryDlg=>
            {
                ionMobilityLibraryDlg.LibraryName = "Test Crosslink IMS Library";
                ionMobilityLibraryDlg.CreateDatabaseFile(TestFilesDir.GetTestPath("ImsLibrary.imsdb"));
                ionMobilityLibraryDlg.GetIonMobilitiesFromResults();
                Assert.AreEqual(1, ionMobilityLibraryDlg.LibraryMobilitiesFlatCount);
                ionMobilityLibraryDlg.OkDialog();
            });
            OkDialog(transitionSettingsUi, transitionSettingsUi.OkDialog);
            WaitForConditionUI(() => transitionSettingsClosed);
            Assert.AreEqual(1, SkylineWindow.Document.PeptideCount);
            var peptideDocNode = SkylineWindow.Document.Peptides.First();
            Assert.AreEqual(1, peptideDocNode.TransitionGroupCount);
            var libKey = peptideDocNode.TransitionGroups.First()
                .GetLibKey(SkylineWindow.Document.Settings, peptideDocNode);
            Assert.IsInstanceOfType(libKey.LibraryKey, typeof(CrosslinkLibraryKey));
            var ionMobilityLibrary = SkylineWindow.Document.Settings.TransitionSettings.IonMobilityFiltering.IonMobilityLibrary;
            var ionMobilityInfo = ionMobilityLibrary.GetIonMobilityInfo(libKey);
            Assert.IsNotNull(ionMobilityInfo);
            Assert.AreEqual(1, ionMobilityInfo.Count);
            var ionMobilityAndCcs = ionMobilityInfo.First();
            Assert.IsFalse(ionMobilityAndCcs.IsEmpty);

            // Duplicate the one molecule in the document and make sure that Skyline does not get confused
            RunUI(()=>
            {
                Assert.AreEqual(1, SkylineWindow.Document.MoleculeCount);
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
                SkylineWindow.Copy();
                SkylineWindow.Paste();
                Assert.AreEqual(2, SkylineWindow.Document.MoleculeCount);
            });

            // Bring up the EditIonMobilityLibraryDlg again, and make sure that pushing the "Use Results" button does not result in any
            // additional values being added to the database.
            transitionSettingsUi = ShowDialog<TransitionSettingsUI>(() =>
            {
                SkylineWindow.ShowTransitionSettingsUI();
            });
            RunUI(() => transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.IonMobility);
            RunDlg<EditIonMobilityLibraryDlg>(() => transitionSettingsUi.IonMobilityControl.EditIonMobilityLibrary(), ionMobilityLibraryDlg=>
            {
                Assert.AreEqual(1, ionMobilityLibraryDlg.LibraryMobilitiesFlatCount);
                ionMobilityLibraryDlg.GetIonMobilitiesFromResults();
                Assert.AreEqual(1, ionMobilityLibraryDlg.LibraryMobilitiesFlatCount);
                ionMobilityLibraryDlg.OkDialog();
            });
            OkDialog(transitionSettingsUi, transitionSettingsUi.OkDialog);
        }
    }
}
