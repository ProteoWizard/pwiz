using System;
using System.IO;
using AutoQC;
using AutoQC.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.GUI;

namespace AutoQCTest
{
    [TestClass]
    public class AutoQcFunctionalTest : AutoQcBaseFunctionalTest
    {
        
        [TestMethod]
        public void TestAutoQcInterface()
        {
            TestFilesZipPaths = new[] { @"Executables\AutoQC\AutoQCTest\AutoQCTest.zip" };
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            base.DoTest();
            // Test the following:
            // 1. Skyline document path entered in AutoQcConfigForm should be valid
            // 2. Configuration names should be unique
            // 3. Start configurations successfully
            // 4. Select a configuration, switch to the log lab and verify that the correct log is selected
            // 5. A running configuration cannot be edited
            BasicTest(MainForm);
        }

        public void BasicTest(MainForm mainForm)
        {
            var skylineDocPath1 = Path.Combine(TestFolder, "test.sky");
            var folderToWatch1 = CreateRawDataDir();

            // Add a configuration.
            var configOneName = "Config One";
            var newConfigForm = ShowDialog<AutoQcConfigForm>(mainForm.ClickAdd);
            RunUI(() =>
            {
                WaitForShownForm(newConfigForm);
                newConfigForm.SetConfigName(configOneName);
            });

            // Skyline file path is required for a configuration.
            TestInvalidMainSettings(newConfigForm, Resources.MainSettings_ValidateSkylineFile_Skyline_file_name_cannot_be_empty__Please_specify_path_to_a_Skyline_file_);

            // File name of the path entered in the Skyline document field must have a .sky extension.
            var doesNotExistFile = Path.Combine(TestFolder, "test-with-results.sky.view");
            RunUI(() =>
            {
                newConfigForm.SetSkylineDocPath(doesNotExistFile);
            });
            TestInvalidMainSettings(newConfigForm,
                string.Format(
                    Resources
                        .MainSettings_ValidateSkylineFile__0__is_not_a_valid_Skyline_file__Skyline_files_have_a__sky_extension_,
                    doesNotExistFile));

            // Enter valid values in all required fields.
            RunUI(() =>
            {
                newConfigForm.SetSkylineDocPath(skylineDocPath1);
                newConfigForm.SetFolderToWatch(folderToWatch1);
                newConfigForm.ClickSave();
            });
            WaitForClosedForm(newConfigForm);
            Assert.AreEqual(1, mainForm.ConfigCount()); // Configuration added successfully


            // Add a second configuration
            var configTwoName = "Config Two";
            var skylineDocPath2 = Path.Combine(TestFolder, "test-with-results.sky");
            newConfigForm = ShowDialog<AutoQcConfigForm>(mainForm.ClickAdd);
            RunUI(() =>
            {
                WaitForShownForm(newConfigForm);
                newConfigForm.SetConfigName(configOneName);
            });

            // Configurations must have unique names
            TestInvalidMainSettings(newConfigForm, string.Format(
                SharedBatch.Properties.Resources.ConfigManager_InsertConfiguration_Configuration___0___already_exists_ + Environment.NewLine +
                SharedBatch.Properties.Resources.ConfigManager_InsertConfiguration_Please_enter_a_unique_name_for_the_configuration_,
                configOneName));

            // Enter valid values in all required fields.
            RunUI(() =>
            {
                WaitForShownForm(newConfigForm);
                newConfigForm.SetConfigName(configTwoName);
                newConfigForm.SetSkylineDocPath(skylineDocPath2);
                newConfigForm.SetFolderToWatch(folderToWatch1);
                newConfigForm.ClickSave();
            });
            WaitForClosedForm(newConfigForm);
            Assert.AreEqual(2, mainForm.ConfigCount()); // Configuration added successfully

            // Start both configurations
            var configOneIndex = StartConfig(mainForm, configOneName);
            var configTwoIndex = StartConfig(mainForm, configTwoName);
            
            RunUI(() =>
            {
                // Select the first config; switch to the "Log" tab; confirm that the correct configuration's log is displayed.
                mainForm.ClickConfig(configOneIndex);
                mainForm.SelectLogTab();
                Assert.AreEqual(configOneName, mainForm.GetSelectedLogName());

                // Select the second config; switch to the "Log" tab; confirm that the correct configuration's log is displayed.
                mainForm.SelectConfigsTab();
                mainForm.ClickConfig(configTwoIndex);
                mainForm.SelectLogTab();
                Assert.AreEqual(configTwoName, mainForm.GetSelectedLogName());
                mainForm.SelectConfigsTab();
            });

            // Both configuration are running, they should not be editable. 
            var editConfigForm = ShowDialog<AutoQcConfigForm>(mainForm.ClickEdit);
            RunUI(() =>
            {
                WaitForShownForm(editConfigForm);
                Assert.IsFalse(editConfigForm.SaveButtonVisible(), "Save button was not hidden for a running configuration");
                Assert.IsTrue(editConfigForm.ConfigNotEditableLabelVisible(),
                    "Message indicating that the configuration is running was not displayed");
                editConfigForm.ClickOk();
            });
            WaitForClosedForm(editConfigForm);

            // Stop the configurations
            StopConfig(mainForm, configOneIndex, configOneName);
            StopConfig(mainForm, configTwoIndex, configTwoName);
        }

        private void TestInvalidMainSettings(AutoQcConfigForm newConfigForm, string expectedMessage)
        {
            RunDlg<CommonAlertDlg>(newConfigForm.ClickSave,
                dlg =>
                {
                    Assert.AreEqual(expectedMessage, dlg.Message);
                    dlg.ClickOk();
                });
        }
    }
}
