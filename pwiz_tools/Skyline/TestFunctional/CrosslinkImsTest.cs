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
    public class CrosslinkImsTest : AbstractFunctionalTest
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
            var ionMobilityLibraryDlg = ShowDialog<EditIonMobilityLibraryDlg>(() => transitionSettingsUi.IonMobilityControl.AddIonMobilityLibrary());
            RunUI(() =>
            {
                ionMobilityLibraryDlg.LibraryName = "Test Crosslink IMS Library";
                ionMobilityLibraryDlg.CreateDatabaseFile(TestFilesDir.GetTestPath("ImsLibrary.imsdb"));
                ionMobilityLibraryDlg.GetIonMobilitiesFromResults();
            });
            OkDialog(ionMobilityLibraryDlg, ionMobilityLibraryDlg.OkDialog);
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
        }
    }
}
