using System;
using System.IO;
using System.Linq;
using System.Xml;
using Ionic.Zip;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SaveAsPreviousVersionTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSaveAsPreviousVersion()
        {
            TestFilesZip = @"TestFunctional\SaveAsPreviousVersionTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Rat_Plasma.sky")));
            var doc = WaitForDocumentLoaded();
            String v36Path = TestFilesDir.GetTestPath("Version36.sky.zip");
            // Verify handling of ion mobility info, which moved from PeptideSettings to TransitionSettings in v19_2
            var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() => transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.IonMobility);
            var calibrationDlg = ShowDialog<EditIonMobilityCalibrationDlg>(transitionSettingsUI.AddIonMobilityCalibration);
            const string imcName = "Test50";
            RunUI(() =>
            {
                calibrationDlg.SetPredictorName(imcName);
                calibrationDlg.SetResolvingPower(50);
            });
            OkDialog(calibrationDlg, () => calibrationDlg.OkDialog());
            OkDialog(transitionSettingsUI, () => transitionSettingsUI.OkDialog());
            doc = WaitForDocumentChange(doc);

            RunUI(()=>SkylineWindow.ShareDocument(v36Path, ShareType.COMPLETE.ChangeSkylineVersion(SkylineVersion.V3_6)));
            using (ZipFile zipFile = new ZipFile(v36Path))
            {
                ZipEntry zipEntryDocument = zipFile.Entries.First(entry => entry.FileName == "Rat_Plasma.sky");
                using (var stream = zipEntryDocument.OpenReader())
                {
                    using (var xmlReader = new XmlTextReader(stream))
                    {
                        var xmlDocument = new XmlDocument();
                        xmlDocument.Load(xmlReader);
                        Assert.IsNotNull(xmlDocument.DocumentElement);
                        Assert.AreEqual("3.6", xmlDocument.DocumentElement.GetAttribute("format_version"));

                        // Did ion mobility stuff serialize to old location and element name?
                        var elemList = xmlDocument.GetElementsByTagName(DriftTimePredictor.EL.predict_drift_time.ToString());
                        Assert.AreEqual(1, elemList.Count);
                    }
                }
                ZipEntry zipEntrySkyd = zipFile.Entries.First(entry => entry.FileName == "Rat_Plasma.skyd");
                using (var stream = zipEntrySkyd.OpenReader())
                {
                    var memoryStream = new MemoryStream();
                    CopyStreamTo(stream, memoryStream);
                    CacheHeaderStruct cacheHeader = CacheHeaderStruct.Read(memoryStream);
                    Assert.AreEqual(CacheFormatVersion.Eleven, cacheHeader.formatVersion);
                }
            }

            // And verify reading it back in again
            RunUI(() => SkylineWindow.OpenSharedFile(v36Path));
            var newdoc = WaitForDocumentLoaded();
            Assert.AreEqual(doc.Settings.PeptideSettings, newdoc.Settings.PeptideSettings);
            Assert.IsTrue(doc.Settings.TransitionSettings.Equals(newdoc.Settings.TransitionSettings));
            Assert.AreEqual(imcName, doc.Settings.TransitionSettings.IonMobility.IonMobilityCalibration.Name);

        }

        private static void CopyStreamTo(Stream input, Stream output)
        {
            byte[] buffer = new byte[65536];
            int bytesRead;
            while (0 != (bytesRead = input.Read(buffer, 0, buffer.Length)))
            {
                output.Write(buffer, 0, bytesRead);
            }
        }
    }
}
