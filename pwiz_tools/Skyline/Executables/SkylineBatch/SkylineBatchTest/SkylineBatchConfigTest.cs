using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkylineBatch;

namespace SkylineBatchTest
{

    [TestClass]
    public class SkylineBatchConfigTest
    {

        

        [TestMethod]
        public void TestValidateConfig()
        {
            var emptyConfig = new SkylineBatchConfig();
            TestValidateError(emptyConfig, "Configuration settings not initialized.");
            
            var noNameConfig = SkylineBatchConfig.GetDefault();
            TestValidateError(noNameConfig, "Please enter a name for the configuration.");

            var noSettingsConfig = SkylineBatchConfig.GetDefault();
            noSettingsConfig.Name = "name";
            TestValidateError(noSettingsConfig, "Please specify path to: Skyline file.");

            var invalidReportConfig = TestUtils.GetTestConfig();
            invalidReportConfig.ReportSettings.Reports.Add(new ReportInfo("bad", "not_real.skyr"));
            TestValidateError(invalidReportConfig, "Report path not_real.skyr is not a valid path.");

            var validConfig = TestUtils.GetTestConfig();
            try
            {
                validConfig.Validate();
            }
            catch (Exception e)
            {
                Assert.Fail("Failed to validate valid configuration");
            }

        }

        private void TestValidateError(SkylineBatchConfig config, string expectedError)
        {
            try
            {
                config.Validate();
                Assert.Fail("Should have failed to validate config");
            }
            catch (Exception e)
            {
                Assert.AreEqual(e.Message, expectedError);
            }
        }

        [TestMethod]
        public void TestMainSettingsEquals()
        {
            var defaultMainSettings = new MainSettings();
            Assert.IsTrue(Equals(defaultMainSettings, defaultMainSettings));
            Assert.IsTrue(Equals(defaultMainSettings, new MainSettings()));
            var changedMainSettings = new MainSettings();
            changedMainSettings.TemplateFilePath = "fakeSkyline.sky";
            Assert.IsFalse(Equals(defaultMainSettings, null));
            Assert.IsFalse(Equals(defaultMainSettings, changedMainSettings));
        }

        [TestMethod]
        public void TestReportSettingsEquals()
        {
            var testReportInfoNoScript = new ReportInfo("name", "path.skyr");
            var testReportInfoWithScript = TestUtils.GetTestReportSettings().Reports[0];
            Assert.IsTrue(Equals(testReportInfoNoScript, testReportInfoNoScript));
            Assert.IsTrue(Equals(testReportInfoNoScript, new ReportInfo("name", "path.skyr")));
            Assert.IsFalse(Equals(testReportInfoNoScript, testReportInfoWithScript));

            var emptyReportSettings = new ReportSettings();
            var reportSettingsWithScript = TestUtils.GetTestReportSettings();
            Assert.IsTrue(Equals(emptyReportSettings, emptyReportSettings));
            Assert.IsTrue(Equals(emptyReportSettings, new ReportSettings()));
            Assert.IsTrue(Equals(reportSettingsWithScript, reportSettingsWithScript));
            
            var changedReportSettings = new ReportSettings();
            changedReportSettings.Add(new ReportInfo("report", "fakeReport.skyr"));
            Assert.IsFalse(Equals(emptyReportSettings, changedReportSettings));
            Assert.IsFalse(Equals(emptyReportSettings, changedReportSettings));
            Assert.IsFalse(Equals(changedReportSettings, reportSettingsWithScript));
        }

        [TestMethod]
        public void TestConfigEquals()
        {
            var emptyConfig = new SkylineBatchConfig();
            Assert.IsTrue(Equals(emptyConfig, emptyConfig));
            Assert.IsTrue(Equals(emptyConfig, new SkylineBatchConfig()));

            var testConfig = TestUtils.GetTestConfig();
            Assert.IsTrue(Equals(testConfig, TestUtils.GetTestConfig()));
            Assert.IsFalse(Equals(testConfig, TestUtils.GetTestConfig("other")));
            var changedTestConfig = TestUtils.GetTestConfig();
            changedTestConfig.MainSettings.AnalysisFolderPath = TestUtils.GetTestFilePath("not_real");
            Assert.IsFalse(Equals(changedTestConfig, TestUtils.GetTestConfig()));
            changedTestConfig = TestUtils.GetTestConfig();
            changedTestConfig.ReportSettings.Add(new ReportInfo("newReport", "badPath.skyr"));
            Assert.IsFalse(Equals(changedTestConfig, TestUtils.GetTestConfig()));

        }

        [TestMethod]
        public void TestConfigCopy()
        {
            var initialConfig = TestUtils.GetTestConfig();
            var copiedConfig = initialConfig.Copy();
            Assert.IsTrue(Equals(initialConfig, copiedConfig));
            Assert.IsFalse(ReferenceEquals(initialConfig, copiedConfig));
            copiedConfig.MainSettings.DataFolderPath = "changedPath";
            Assert.IsFalse(Equals(initialConfig, copiedConfig));

            copiedConfig = initialConfig.Copy();
            copiedConfig.ReportSettings.Add(new ReportInfo("newReport", "badPath.skyr"));
            Assert.IsFalse(Equals(initialConfig, copiedConfig));

        }



    }
}