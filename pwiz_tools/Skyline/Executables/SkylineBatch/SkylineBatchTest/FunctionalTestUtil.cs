using System;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;
using SkylineBatch;

namespace SkylineBatchTest
{
    /// <summary>
    /// All functional tests MUST derive from this base class.
    /// </summary>
    public class FunctionalTestUtil
    {


        public delegate bool ConditionDelegate(MainForm mainForm, bool expectedValue);

        public static void WaitForCondition(ConditionDelegate condition, MainForm mainForm, bool expectedValue, TimeSpan timeout, int timestep, string errorMessage)
        {
            var startTime = DateTime.Now;
            while (DateTime.Now - startTime < timeout)
            {
                // Condition satisfied
                if (condition(mainForm, expectedValue)) return;

                // Bail out early if a connection error dialog is open (network/panorama dependency unavailable)
                var connectionError = SharedBatchTest.AbstractBaseFunctionalTest.FindOpenForm<ConnectionErrorForm>();
                if (connectionError != null)
                {
                    Assert.Inconclusive("Skipping test due to ConnectionErrorForm (network or server unavailable).");
                    return;
                }

                Thread.Sleep(timestep);
            }
            throw new Exception(errorMessage);
        }

        public static void PopulateConfigForm(SkylineBatchConfigForm configForm, string configName, string directory, AbstractSkylineBatchFunctionalTest caller)
        {
            caller.WaitForShownForm(configForm);
            configForm.textConfigName.Text = configName;
            configForm.templateControl.SetPath(Path.Combine(directory, "emptyTemplate.sky"));
            configForm.textAnalysisPath.Text = Path.Combine(directory, "analysisFolder");
            configForm.dataControl.SetPath(Path.Combine(directory, "emptyData"));
        }

        public static void CheckConfigs(int total, int invalid, MainForm mainForm, string messsageOne = "", string messageTwo = "")
        {
            Assert.AreEqual(total, mainForm.ConfigCount(),
                messsageOne + Environment.NewLine + mainForm.ConfigValidationReport());
            // Append why each configuration is invalid. Without it this assert reports only two
            // integers, which is not enough to tell a real regression from a machine that is
            // missing something a configuration points at.
            Assert.AreEqual(invalid, mainForm.InvalidConfigCount(),
                messageTwo + Environment.NewLine + mainForm.ConfigValidationReport() +
                Environment.NewLine + SkylineInstallationReport());
        }

        /// <summary>
        /// Which Skyline installation, if any, the batch tool can see. A configuration that fails
        /// with "Could not find a Skyline installation on this computer" is reporting this state,
        /// not anything about the configuration itself, and the two are easy to confuse.
        /// </summary>
        public static string SkylineInstallationReport()
        {
            return string.Join(Environment.NewLine,
                @"  Skyline installations:",
                $@"    local cmd path:  {SharedBatch.Properties.Settings.Default.SkylineLocalCommandPath ?? @"(none)"}",
                $@"    admin cmd path:  {SharedBatch.Properties.Settings.Default.SkylineAdminCmdPath ?? @"(none)"}",
                $@"    test fallback:   {SkylineInstallations.TestAdminSkylineCmdPath ?? @"(none)"}",
                $@"    HasLocalSkylineCmd={SkylineInstallations.HasLocalSkylineCmd} HasSkyline={SkylineInstallations.HasSkyline} HasSkylineDaily={SkylineInstallations.HasSkylineDaily} HasCustom={SkylineInstallations.HasCustomSkylineCmd}");
        }

        public static void ClearConfigs(MainForm mainForm)
        {
            while (mainForm.ConfigCount() > 0)
            {
                    mainForm.ClickConfig(0);
                    mainForm.ClickDelete();
            }
            CheckConfigs(0, 0, mainForm);
        }

    }
}