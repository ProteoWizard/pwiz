using System;
using System.Collections.Generic;
using System.IO;
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




        public static void PopulateConfigForm(SkylineBatchConfigForm configForm, string configName, string directory, AbstractSkylineBatchFunctionalTest caller)
        {
            caller.WaitForShownForm(configForm);
            configForm.textConfigName.Text = configName;
            configForm.textTemplateFile.Text = Path.Combine(directory, "emptyTemplate.sky");
            configForm.textAnalysisPath.Text = Path.Combine(directory, "analysisFolder");
            configForm.textDataPath.Text = Path.Combine(directory, "emptyData");
        }

        public static void CheckConfigs(int total, int invalid, MainForm mainForm)
        {
            Assert.AreEqual(total, mainForm.ConfigCount());
            Assert.AreEqual(invalid, mainForm.InvalidConfigCount());
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