using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using SkylineBatch;
using SharedBatchTest;
using SkylineBatch.Properties;

namespace SkylineBatchTest
{
    /// <summary>
    /// All functional tests MUST derive from this base class.
    /// </summary>
    public abstract class AbstractSkylineBatchFunctionalTest : AbstractBaseFunctionalTest
    {
        public const string SKYLINE_BATCH_FOLDER = @"Executables\SkylineBatch\";


        public new string TestFilesZip
        {
            get => base.TestFilesZip;
            set => base.TestFilesZip = SKYLINE_BATCH_FOLDER + value;
        }

        public new string[] TestFilesZipPaths
        {
            get => base.TestFilesZipPaths;
            set
            {
                var testFilesZipPaths = value;
                for (int i = 0; i < testFilesZipPaths.Length; i++)
                    testFilesZipPaths[i] = SKYLINE_BATCH_FOLDER + testFilesZipPaths[i];
                base.TestFilesZipPaths = testFilesZipPaths;
            }
        }


        protected override Form MainFormWindow()
        {
            return Program.MainWindow;
        }

        protected override void ResetSettings()
        {
            Settings.Default.Reset();
        }

        protected override void InitProgram()
        {
        }

        protected override void StartProgram()
        {
            Program.TestDirectory = Path.GetDirectoryName(TestFilesDirs[0].FullPath);
            Program.Main(new string[0]);
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