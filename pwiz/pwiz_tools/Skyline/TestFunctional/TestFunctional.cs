/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Ionic.Zip;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;

namespace pwiz.SkylineTestFunctional
{
    public abstract class AbstractFunctionalTest
    {
        private const int SLEEP_INTERVAL = 100;
        private const int WAIT_TIME = 30000;    // 30 seconds
        private const int WAIT_CYCLES = WAIT_TIME/SLEEP_INTERVAL;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }
        public string TestFilesZip { get; set; }
        public string TestDirectoryName { get; set; }
        public TestFilesDir TestFilesDir { get; set; }
        private readonly List<Exception> _testExceptions = new List<Exception>();
        private bool _testCompleted;

        public static SkylineWindow SkylineWindow { get { return Program.MainWindow; } }

        protected static T ShowDialog<T>(Action act) where T : Form
        {
            SkylineWindow.BeginInvoke(act);
            T dlg = WaitForOpenForm<T>();
            Assert.IsNotNull(dlg);
            return dlg;
        }

        protected static void RunUI(Action act)
        {
            SkylineWindow.Invoke(act);
        }

        protected static void RunDlg<T>(Action show, Action<T> act) where T : Form
        {
            T dlg = ShowDialog<T>(show);
            RunUI(() => act(dlg));
            WaitForClosedForm(dlg);
        }

        protected static void SelectNode(SrmDocument.Level level, int iNode)
        {
            var pathSelect = SkylineWindow.Document.GetPathTo((int)level, iNode);
            RunUI(() => SkylineWindow.SequenceTree.SelectedPath = pathSelect);
        }

        public static T FindOpenForm<T>() where T : Form
        {
            while (true) {
                try
                {
                    foreach (var form in Application.OpenForms)
                    {
                        var tForm = form as T;
                        if (tForm != null)
                        {
                            return tForm;
                        }
                    }
                    return null;
                }
                catch (InvalidOperationException)
                {
                    // Application.OpenForms might be modified during the iteration.
                    // If that happens, go through the list again.
                }
            }
        }

        public static T WaitForOpenForm<T>() where T : Form
        {
            for (int i = 0; i < WAIT_CYCLES; i ++ )
            {
                T tForm = FindOpenForm<T>();
                if (tForm != null)
                    return tForm;

                Thread.Sleep(SLEEP_INTERVAL);
            }
            return null;
        }

        public static bool IsFormOpen(Form form)
        {
            foreach (var formOpen in Application.OpenForms)
            {
                if (ReferenceEquals(form, formOpen))
                {
                    return true;
                }
            }
            return false;
        }

        public static void WaitForClosedForm(Form formClose)
        {
            for (int i = 0; i < WAIT_CYCLES; i++)
            {
                bool isOpen = true;
                Program.MainWindow.Invoke(new Action(() => isOpen = IsFormOpen(formClose)));
                if (!isOpen)
                    return;
                Thread.Sleep(SLEEP_INTERVAL);
            }
        }

        public static bool WaitForDocumentChange(SrmDocument docCurrent)
        {
            // Make sure the document changes on the UI thread, since tests are mostly
            // interested in interacting with the document on the UI thread.
            return WaitForConditionUI(() => !ReferenceEquals(docCurrent, SkylineWindow.DocumentUI));
        }

        public static bool WaitForCondition(Func<bool> func)
        {
            for (int i = 0; i < WAIT_CYCLES; i ++)
            {
                if (func())
                    return true;
                Thread.Sleep(SLEEP_INTERVAL);
            }
            return false;
        }

        public static bool WaitForConditionUI(Func<bool> func)
        {
            for (int i = 0; i < WAIT_CYCLES; i++)
            {
                bool isCondition = false;
                Program.MainWindow.Invoke(new Action(() => isCondition = func()));                
                if (isCondition)
                    return true;
                Thread.Sleep(SLEEP_INTERVAL);
            }
            return false;
        }

        public static void WaitForGraphs()
        {
            WaitForConditionUI(() => !SkylineWindow.IsGraphUpdatePending);
        }

        public static void OkDialog(Form form, Action okAction)
        {
            form.Invoke(okAction);
            WaitForClosedForm(form);
        }

        /// <summary>
        /// Starts up Skyline, and runs the <see cref="DoTest"/> test method.
        /// </summary>
        protected void RunFunctionalTest()
        {
            new Thread(WaitForSkyline).Start();
            Program.Main();
            if (_testExceptions.Count > 0)
            {
                Assert.Fail(_testExceptions[0].ToString());
            }
            Assert.IsTrue(_testCompleted);
        }

        private void WaitForSkyline()
        {
            try
            {
                while (Program.MainWindow == null || !Program.MainWindow.IsHandleCreated)
                {
                    Thread.Sleep(SLEEP_INTERVAL);
                }
                Settings.Default.Reset();

                if (TestFilesZip == null)
                {
                    RunTest();
                }
                else
                {
                    TestFilesDir = new TestFilesDir(TestContext, TestFilesZip, TestDirectoryName);
                    RunTest();
                    TestFilesDir.Dispose();
                }
            }
            catch (Exception x)
            {
                // An exception occurred outside RunTest
                _testExceptions.Add(x);
                EndTest();
            }
        }

        private void RunTest()
        {
            DoTest();
            EndTest();
        }

        private void EndTest()
        {
            var skylineWindow = Program.MainWindow;
            if (skylineWindow == null)
                return;

            // Release all resources by setting the document to something that
            // holds no file handles.
            var docNew = new SrmDocument(SrmSettingsList.GetDefault());
            RunUI(() => SkylineWindow.SwitchDocument(docNew, null));
            // Close the Skyline window
            _testCompleted = true;
            skylineWindow.Invoke(new Action(skylineWindow.Close));                        
        }

        protected abstract void DoTest();
    }

    public static class ExtensionTestContext
    {
        public static string GetTestPath(this TestContext testContext, string relativePath)
        {
            return Path.Combine(testContext.TestDir, relativePath);
        }

        public static String GetProjectDirectory(this TestContext testContext, string relativePath)
        {
            for (String directory = testContext.TestDir; directory.Length > 10; directory = Path.GetDirectoryName(directory))
            {
                if (Equals(Path.GetFileName(directory), "TestResults"))
                {
                    return Path.Combine(Path.GetDirectoryName(directory), relativePath);
                }
            }
            return null;
        }

        public static void ExtractTestFiles(this TestContext testContext, string relativePathZip)
        {
            testContext.ExtractTestFiles(relativePathZip, testContext.TestDir);
        }

        public static void ExtractTestFiles(this TestContext testContext, string relativePathZip, string destDir)
        {
            string zipPath = testContext.GetProjectDirectory(relativePathZip);
            using (ZipFile zipFile = ZipFile.Read(zipPath))
            {
                foreach (ZipEntry zipEntry in zipFile)
                    zipEntry.Extract(destDir, ExtractExistingFileAction.OverwriteSilently);
            }
        }
    }

    /// <summary>
    /// Creates and cleans up a directory containing the contents of a
    /// test ZIP file.
    /// </summary>
    public sealed class TestFilesDir : IDisposable
    {
        private TestContext TestContext { get; set; }

        /// <summary>
        /// Creates a sub-directory of the Test Results directory with the same
        /// basename as a ZIP file in the test project tree.
        /// </summary>
        /// <param name="testContext">The test context for the test creating the directory</param>
        /// <param name="relativePathZip">A root project relative path to the ZIP file</param>
        /// <param name="directoryName">Name of directory to create in the test results</param>
        public TestFilesDir(TestContext testContext, string relativePathZip, string directoryName)
        {
            TestContext = testContext;
            if (directoryName == null)
                directoryName = Path.GetFileNameWithoutExtension(relativePathZip);
            FullPath = TestContext.GetTestPath(directoryName);
            TestContext.ExtractTestFiles(relativePathZip, FullPath);
        }

        public string FullPath { get; private set; }

        /// <summary>
        /// Returns a full path to a file in the unzipped directory.
        /// </summary>
        /// <param name="relativePath">Relative path, as stored in the ZIP file, to the file</param>
        /// <returns>Absolute path to the file for use in tests</returns>
        public string GetTestPath(string relativePath)
        {
            return Path.Combine(FullPath, relativePath);
        }

        /// <summary>
        /// Attempts to move the directory to make sure no file handles are open.
        /// Used to delete the directory, but it can be useful to look at test
        /// artifacts, after the tests complete.
        /// </summary>
        public void Dispose()
        {
            try
            {
                string guidName = Guid.NewGuid().ToString();
                Directory.Move(FullPath, guidName);
                Directory.Move(guidName, FullPath);
            }
            catch (IOException)
            {
                // Useful for debugging. Exception names file that is locked.
                Directory.Delete(FullPath, true);
            }
        }
    }

    public static class AssertEx
    {
        public static void ThrowsException<T>(Action throwEx)
            where T : Exception
        {
            ThrowsException<T>(() => { throwEx(); return null; });
        }

        public static void ThrowsException<T>(Func<object> throwEx)
            where T : Exception
        {
            try
            {
                throwEx();
                Assert.Fail("Exception expected");
            }
            catch (T)
            {
            }
        }

        public static void Contains(string value, params string[] parts)
        {
            Assert.IsNotNull(value, "No message found");
            foreach (string part in parts)
            {
                Assert.IsTrue(value.Contains(part),
                    string.Format("The text '{0}' does not contain '{1}'", value, part));
            }
        }
    }
}
