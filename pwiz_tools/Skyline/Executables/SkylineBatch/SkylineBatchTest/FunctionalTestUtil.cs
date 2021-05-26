using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkylineBatch;
using SharedBatchTest;
using SkylineBatch.Properties;

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
            var ticksPerMillisecond = 10000;
            var startTime = DateTime.Now;
            while (DateTime.Now - startTime < timeout)
            {
                if (condition(mainForm, expectedValue)) return;
                Thread.Sleep(timestep);
            }
            throw new Exception(errorMessage);
        }

        public static void PopulateConfigForm(SkylineBatchConfigForm configForm, string configName, string directory, AbstractSkylineBatchFunctionalTest caller)
        {
            caller.WaitForShownForm(configForm);
            configForm.textConfigName.Text = configName;
            configForm.templateFileControl.Text = Path.Combine(directory, "emptyTemplate.sky");
            configForm.textAnalysisPath.Text = Path.Combine(directory, "analysisFolder");
            configForm.textDataPath.Text = Path.Combine(directory, "emptyData");
        }

        public static void CheckConfigs(int total, int invalid, MainForm mainForm, string messsageOne = "", string messageTwo = "")
        {
            Assert.AreEqual(total, mainForm.ConfigCount(), messsageOne);
            Assert.AreEqual(invalid, mainForm.InvalidConfigCount(), messageTwo);
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