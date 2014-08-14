/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Summary description for ExportChromatogramTest
    /// </summary>
    [TestClass]
    public class ShareSettingsTest : AbstractFunctionalTest
    {
        private const string ALTERED_SETTINGS_NAME = "10mz_nonoverlap_mod";
        [TestMethod]
        public void TestShareSettings()
        {
            TestFilesZip = @"TestFunctional\ShareSettingsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var doc = TestFilesDir.GetTestPath("ShareSettingsTest.sky");
            RunUI(() => SkylineWindow.OpenFile(doc));
            WaitForDocumentLoaded();
            // Import bad settings leads to exception
            string badSettings = TestFilesDir.GetTestPath("BadSettings.skys");
            RunDlg<MessageDlg>(() => ShareListDlg<SrmSettingsList, SrmSettings>.ImportFile(SkylineWindow, Settings.Default.SrmSettingsList, badSettings),
                messageDlg =>
            {
                Assert.AreEqual(TextUtil.LineSeparate(string.Format(Resources.ShareListDlg_ImportFile_Failure_loading__0__, badSettings),
                                                      string.Format(Resources.XmlUtil_GetInvalidDataMessage_The_file_contains_an_error_on_line__0__at_column__1__, 1, 1)),
                                messageDlg.Message);
                messageDlg.OkDialog();
            });

            // Test round trip import-share
            string originalExpected = TestFilesDir.GetTestPath("OriginalExpected.skys");
            string originalActual = TestFilesDir.GetTestPath("OriginalActual.skys");
            Assert.AreEqual(Settings.Default.SrmSettingsList.Count, 1);
            RunUI(() => ShareListDlg<SrmSettingsList, SrmSettings>.ImportFile(SkylineWindow, Settings.Default.SrmSettingsList, originalExpected)); // Backward compatibility
            Assert.AreEqual(Settings.Default.SrmSettingsList.Count, 3);
            var settingsOld = SkylineWindow.Document.Settings;
            RunDlg<ShareListDlg<SrmSettingsList, SrmSettings>>(SkylineWindow.ShareSettings,
                 shareSettingsDlg =>
                     {
                         Assert.AreEqual(shareSettingsDlg.List.Count, 3);
                         shareSettingsDlg.SelectAll(true);
                         shareSettingsDlg.OkDialog(originalActual);
                     });
            RunDlg<MultiButtonMsgDlg>(() => ShareListDlg<SrmSettingsList, SrmSettings>.ImportFile(SkylineWindow, Settings.Default.SrmSettingsList, originalActual),
                messageDlg => messageDlg.Btn0Click());
            var settingsNew = SkylineWindow.Document.Settings;
            Assert.AreSame(settingsOld, settingsNew);

            // Change settings, save, share again, and import
            IonType[] ionList =
                {
                    IonType.b, IonType.y, IonType.precursor
                };
            RunUI(() => SkylineWindow.Document.Settings.TransitionSettings.Libraries.ChangeIonCount(6));
            RunUI(() => SkylineWindow.Document.Settings.TransitionSettings.Filter.ChangeIonTypes(ionList));
            RunDlg<SaveSettingsDlg>(() => SkylineWindow.SaveSettings(),
                saveSettingsDlg =>
                    {
                        saveSettingsDlg.SettingsName = ALTERED_SETTINGS_NAME;
                        saveSettingsDlg.OkDialog();
                    });
            Assert.AreEqual(Settings.Default.SrmSettingsList.Count, 4);
            var settingsChanged = SkylineWindow.Document.Settings;
            string alteredExpected = TestFilesDir.GetTestPath("AlteredExpected.skys");
            string alteredActual = TestFilesDir.GetTestPath("AlteredActual.skys");
            // Test that sharing twice leads to same resutls
            RunDlg<ShareListDlg<SrmSettingsList, SrmSettings>>(SkylineWindow.ShareSettings,
                shareSettingsDlg =>
                    {
                        Assert.AreEqual(shareSettingsDlg.List.Count, 4);
                        shareSettingsDlg.SelectAll(true);
                        shareSettingsDlg.OkDialog(alteredActual);
                    });
            RunDlg<ShareListDlg<SrmSettingsList, SrmSettings>>(SkylineWindow.ShareSettings,
                shareSettingsDlg =>
                    {
                        Assert.AreEqual(shareSettingsDlg.List.Count, 4);
                        shareSettingsDlg.SelectAll(true);
                        shareSettingsDlg.OkDialog(alteredExpected);
                    });
            AssertFileEquals(alteredActual, alteredExpected);
            Settings.Default.SrmSettingsList.Clear();
            Assert.AreEqual(Settings.Default.SrmSettingsList.Count, 0);
            RunUI(() => ShareListDlg<SrmSettingsList, SrmSettings>.ImportFile(SkylineWindow, Settings.Default.SrmSettingsList, alteredActual));
            var settingsChangedCopy = SkylineWindow.Document.Settings;
            Assert.AreSame(settingsChanged, settingsChangedCopy);

        }

        private static void AssertFileEquals(string path1, string path2)
        {
            string file1 = File.ReadAllText(path1);
            string file2 = File.ReadAllText(path2);
            AssertEx.NoDiff(file1, file2);
        }
    }
}
