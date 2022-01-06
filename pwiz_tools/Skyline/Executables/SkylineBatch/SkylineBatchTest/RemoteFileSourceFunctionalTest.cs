using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;
using SkylineBatch;

namespace SkylineBatchTest
{
    [TestClass]
    public class RemoteFileSourceFunctionalTest : AbstractSkylineBatchFunctionalTest
    {
        public static string CONFIG_FOLDER;
        public static string TEST_FOLDER;

        [TestMethod]
        public void RemoteFileSourceTest()
        {
            /*TestFilesZipPaths = new[]
               {@"SkylineBatchTest\RemoteFileSourceFunctionalTest.zip", @"SkylineBatchTest\TestConfigurationFiles.zip"};

            RunFunctionalTest();*/

            throw new NotImplementedException();
        }

        protected override void DoTest()
        {
            TEST_FOLDER = TestFilesDirs[0].FullPath;
            CONFIG_FOLDER = TestFilesDirs[1].FullPath;

            var mainWindow = MainFormWindow();
            var mainForm = mainWindow as MainForm;
            WaitForShownForm(mainForm);
            Assert.IsNotNull(mainForm, "Main program window is not an instance of MainForm.");
            Assert.AreEqual(0, mainForm.ConfigCount());

            TestAddRemoteFileSource(mainForm);

            TestEditCurrentRemoteFileSource(mainForm);

            TestEditListRemoteFileSource(mainForm);

            TestReplaceRemoteFileSources(mainForm);
        }

        /*private bool ConfigRunning(MainForm mainForm, bool expectedAnswer)
        {
            bool worked = false;
            RunUI(() =>
            {
                worked = expectedAnswer == mainForm.ConfigRunning("EmptyTemplate");
            });
            return worked;
        }*/

        public void TestAddRemoteFileSource(MainForm mainForm)
        {
            throw new NotImplementedException();
            /*var dataDirectory = Path.Combine(CONFIG_FOLDER, "emptyData");
            var configFile = Path.Combine(TEST_FOLDER, "DownloadingConfigurationFTP.bcfg");
            
            RunUI(() =>
            {
                mainForm.DoImport(configFile);
                FunctionalTestUtil.CheckConfigs(1, 0, mainForm, "Config was not imported!", "Config was imported but invalid");
            });
            var longWaitDialog = ShowDialog<LongWaitDlg>(() => mainForm.ClickRun(1));
            WaitForClosedForm(longWaitDialog);
            var thirtySeconds = new TimeSpan(0,0,30);
            FunctionalTestUtil.WaitForCondition(ConfigRunning, mainForm, true, thirtySeconds, 200,
                "Config did not start");
            var oneMinute = new TimeSpan(0, 1, 0);
            FunctionalTestUtil.WaitForCondition(ConfigRunning, mainForm, false, oneMinute, 1000,
                "Config ran past timeout");
            Assert.AreEqual(true, File.Exists(Path.Combine(dataDirectory, "nselevse_L120412_003_SW.wiff")));
            Assert.AreEqual(12427264, new FileInfo(Path.Combine(dataDirectory, "nselevse_L120412_003_SW.wiff")).Length);
            */
        }

        public void TestEditCurrentRemoteFileSource(MainForm mainForm)
        {
            throw new NotImplementedException();

        }

        public void TestEditListRemoteFileSource(MainForm mainForm)
        {
            throw new NotImplementedException();

        }

        public void TestReplaceRemoteFileSources(MainForm mainForm)
        {
            throw new NotImplementedException();

        }
    }
}
