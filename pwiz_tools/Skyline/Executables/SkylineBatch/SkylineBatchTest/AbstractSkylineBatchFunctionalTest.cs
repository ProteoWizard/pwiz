using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SkylineBatch;
using SharedBatchTest;

namespace SkylineBatchTest
{
    /// <summary>
    /// All functional tests MUST derive from this base class.
    /// </summary>
    public abstract class AbstractSkylineBatchFunctionalTest : AbstractBaseFunctionalTest
    {
        protected override Form MainFormWindow()
        {
            return Program.MainWindow;
        }

        protected override void ResetSettings()
        {
            SkylineBatch.Properties.Settings.Default.Reset();
        }

        protected override void InitProgram()
        {
        }

        protected override void StartProgram()
        {
            Program.Main();
        }

        protected override void InitTestExceptions()
        {
            Program.TestExceptions = new List<Exception>();
        }

        protected override void AddTestException(Exception exception)
        {
            Program.AddTestException(exception);
        }

        protected override List<Exception> GetTestExceptions()
        {
            return Program.TestExceptions;
        }

        protected override void SetFunctionalTest()
        {
            Program.FunctionalTest = true;
        }
    }
}