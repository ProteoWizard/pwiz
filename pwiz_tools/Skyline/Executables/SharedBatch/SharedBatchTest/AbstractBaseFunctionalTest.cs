using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;

namespace SharedBatchTest
{
    public abstract class AbstractBaseFunctionalTest
    {
        protected const int SLEEP_INTERVAL = 100;
        public const int WAIT_TIME = 60 * 1000;    // 60 seconds

        private bool _testCompleted;

        protected abstract void DoTest();
        protected abstract Form MainFormWindow();
        protected abstract void ResetSettings();
        protected abstract void InitProgram();
        protected abstract void StartProgram();
        protected abstract void InitTestExceptions();
        protected abstract void AddTestException(Exception exception);
        protected abstract List<Exception> GetTestExceptions();
        protected abstract void SetFunctionalTest();

        private Form MainWindow => MainFormWindow();

        /// <summary>
        /// Starts up the program being tested, and runs the <see cref="DoTest"/> test method.
        /// </summary>
        protected void RunFunctionalTest()
        {
            // Be prepared to re-run test in the event that a previously downloaded data file is damaged or stale
            for (; ; )
            {
                try
                {
                    RunFunctionalTestOrThrow();
                }
                catch (Exception x)
                {
                    AddTestException(x);
                }

                // Delete unzipped test files.
                if (TestFilesDirs != null)
                {
                    foreach (TestFilesDir dir in TestFilesDirs)
                    {
                        try
                        {
                            dir?.Dispose();
                        }
                        catch (Exception x)
                        {
                            AddTestException(x);
                        }
                    }
                }

                if (GetTestExceptions().Count > 0)
                {
                    //Log<AbstractBaseFunctionalTest>.Exception(@"Functional test exception", Program.TestExceptions[0]);
                    const string errorSeparator = "------------------------------------------------------";
                    Assert.Fail("{0}{1}{2}{3}",
                        Environment.NewLine + Environment.NewLine,
                        errorSeparator + Environment.NewLine,
                        GetTestExceptions()[0],
                        Environment.NewLine + errorSeparator + Environment.NewLine);
                }
                break;
            }

            if (!_testCompleted)
            {
                //Log<AbstractBaseFunctionalTest>.Fail(@"Functional test did not complete");
                Assert.Fail("Functional test did not complete");
            }
        }

        protected void RunFunctionalTestOrThrow()
        {
            SetFunctionalTest();
            InitTestExceptions();
            LocalizationHelper.InitThread();

            // Unzip test files.
            if (TestFilesZipPaths != null)
            {
                TestFilesDirs = new TestFilesDir[TestFilesZipPaths.Length];
                for (int i = 0; i < TestFilesZipPaths.Length; i++)
                {
                    TestFilesDirs[i] = new TestFilesDir(TestContext, TestFilesZipPaths[i], TestDirectoryName,
                        null, IsExtractHere(i));
                }
            }

            InitProgram();
            InitializeSettings();

            var threadTest = new Thread(WaitForMainWindow) { Name = @"Functional test thread" };
            LocalizationHelper.InitThread(threadTest);
            threadTest.Start();
            StartProgram();
            threadTest.Join();

            // Were all windows disposed?
            // FormEx.CheckAllFormsDisposed();
            CommonFormEx.CheckAllFormsDisposed();
        }

        /// <summary>
        /// Reset the settings for the application before starting a test.
        /// Tests can override this method if they have have any settings that need to
        /// be set before the test's DoTest method gets called.
        /// </summary>
        protected void InitializeSettings()
        {
            ResetSettings();
        }

        protected static int GetWaitCycles(int millis = WAIT_TIME)
        {
            int waitCycles = millis / SLEEP_INTERVAL;

            if (System.Diagnostics.Debugger.IsAttached)
            {
                // When debugger is attached, some vendor readers are S-L-O-W!
                waitCycles *= 10;
            }

            // Wait a little longer for debug build. (This may also imply code coverage testing, slower yet)
            if (ExtensionTestContext.IsDebugMode)
            {
                waitCycles = waitCycles * 4;
            }

            return waitCycles;
        }

        protected TDlg ShowDialog<TDlg>([InstantHandle] Action act, int millis = -1) where TDlg : Form
        {
            var existingDialog = FindOpenForm<TDlg>();
            if (existingDialog != null)
            {
                AssertEx.IsNull(existingDialog, typeof(TDlg) + " is already open");
            }

            AppBeginInvoke(act);
            TDlg dlg;
            if (millis == -1)
                dlg = WaitForOpenForm<TDlg>();
            else
                dlg = WaitForOpenForm<TDlg>(millis);
            Assert.IsNotNull(dlg);

            return dlg;
        }

        public static TDlg FindOpenForm<TDlg>() where TDlg : Form
        {
            foreach (var form in OpenForms)
            {
                var tForm = form as TDlg;
                if (tForm != null && tForm.Created)
                {
                    return tForm;
                }
            }
            return null;
        }

        public TDlg WaitForOpenForm<TDlg>(int millis = WAIT_TIME) where TDlg : Form
        {
            var result = TryWaitForOpenForm<TDlg>(millis);
            if (result == null)
            {
                int waitCycles = GetWaitCycles(millis);
                Assert.Fail(@"Timeout {0} seconds exceeded in WaitForOpenForm({1}). Open forms: {2}", waitCycles * SLEEP_INTERVAL / 1000, typeof(TDlg).Name, GetOpenFormsString());
            }
            return result;
        }

        private static string GetOpenFormsString()
        {
            var result = string.Join(", ", OpenForms.Select(form => string.Format("{0} ({1})", form.GetType().Name, GetTextForForm(form))));
            // Without line numbers, this isn't terribly useful.  Disable for now.
            // result += GetAllThreadsStackTraces();
            return result;
        }

        private static string GetTextForForm(Control form)
        {
            var result = form.Text;
            var threadExceptionDialog = form as ThreadExceptionDialog;
            if (threadExceptionDialog != null)
            {
                // Locate the details text box, return the contents - much more informative than the dialog title
                result = threadExceptionDialog.Controls.Cast<Control>()
                    .Where(control => control is TextBox)
                    .Aggregate(result, (current, control) => current + ": " + GetExceptionText(control));
            }
            return result;
        }

        private static string GetExceptionText(Control control)
        {
            string text = control.Text;
            // int assembliesIndex = text.IndexOf("************** Loaded Assemblies **************", StringComparison.Ordinal);
            // if (assembliesIndex != -1)
            //     text = TextUtil.LineSeparate(text.Substring(0, assembliesIndex).Trim(), "------------- End ThreadExceptionDialog Stack -------------");
            return text;
        }

        public TDlg TryWaitForOpenForm<TDlg>(int millis = WAIT_TIME, Func<bool> stopCondition = null) where TDlg : Form
        {
            int waitCycles = GetWaitCycles(millis);
            for (int i = 0; i < waitCycles; i++)
            {
                Assert.IsFalse(GetTestExceptions().Any(), "Exception while running test");

                var tForm = FindOpenForm<TDlg>();
                if (tForm != null)
                {
                    return tForm;
                }

                if (stopCondition != null && stopCondition())
                    break;

                Thread.Sleep(SLEEP_INTERVAL);
            }
            return null;
        }

        public void WaitForClosedForm(Form formClose)
        {
            int waitCycles = GetWaitCycles();
            for (int i = 0; i < waitCycles; i++)
            {
                Assert.IsFalse(GetTestExceptions().Any(), "Exception while running test");

                bool isOpen = true;
                AppInvoke(() => isOpen = IsFormOpen(formClose));
                if (!isOpen)
                    return;
                Thread.Sleep(SLEEP_INTERVAL);
            }

            Assert.Fail(@"Timeout {0} seconds exceeded in WaitForClosedForm. Open forms: {1}", waitCycles * SLEEP_INTERVAL / 1000, GetOpenFormsString());
        }

        public void WaitForShownForm(Form formShow)
        {
            int waitCycles = GetWaitCycles();
            var j = 0;
            for (int i = 0; i < waitCycles; i++)
            {
                Assert.IsFalse(GetTestExceptions().Any(), "Exception while running test");

                bool isVisible = false;
                AppInvoke(() => isVisible = formShow.Visible);
                if (isVisible)
                    if (j < 3)
                        j++;
                    else
                        return;
                Thread.Sleep(SLEEP_INTERVAL);
            }

            Assert.Fail(@"Timeout {0} seconds exceeded in WaitForShownForm. Open forms: {1}", waitCycles * SLEEP_INTERVAL / 1000, GetOpenFormsString());
        }

        public static bool IsFormOpen(Form form)
        {
            foreach (var formOpen in OpenForms)
            {
                if (ReferenceEquals(form, formOpen))
                {
                    return true;
                }
            }
            return false;
        }

        protected void RunDlg<TDlg>(Action show, [InstantHandle] Action<TDlg> act = null, bool pause = false, int millis = -1) where TDlg : Form
        {
            RunDlg(show, false, act, pause, millis);
        }

        protected void RunDlg<TDlg>(Action show, bool waitForDocument, Action<TDlg> act = null, bool pause = false, int millis = -1) where TDlg : Form
        {
            TDlg dlg = ShowDialog<TDlg>(show, millis);
            // if (pause)
            //     PauseTest();
            RunUI(() =>
            {
                if (act != null)
                    act(dlg);
                else
                    dlg.CancelButton.PerformClick();
            });
            WaitForClosedForm(dlg);
        }

        private void WaitForMainWindow()
        {
            try
            {
                int waitCycles = GetWaitCycles();
                for (int i = 0; i < waitCycles; i++)
                {
                    if (MainWindow != null && MainWindow.IsHandleCreated)
                        break;

                    Thread.Sleep(SLEEP_INTERVAL);
                }

                Assert.IsTrue(MainWindow != null && MainWindow.IsHandleCreated,
                    @"Timeout {0} seconds exceeded in WaitForMainWindow", waitCycles * SLEEP_INTERVAL / 1000);

                RunTest();
            }
            catch (Exception x)
            {
                // Save exception for reporting from main thread.
                AddTestException(x);
            }

            EndTest();

            ResetSettings();
        }

        private void RunTest()
        {
            // Use internal clipboard for testing so that we don't collide with other processes
            // using the clipboard during a test run.
            // ClipboardEx.UseInternalClipboard();
            // ClipboardEx.Clear();

            var doClipboardCheck = TestContext.Properties.Contains(@"ClipboardCheck");
            string clipboardCheckText = doClipboardCheck ? (string)TestContext.Properties[@"ClipboardCheck"] : String.Empty;
            if (doClipboardCheck)
            {
                RunUI(() => Clipboard.SetText(clipboardCheckText));
            }

            DoTest();
            if (doClipboardCheck)
            {
                RunUI(() => Assert.AreEqual(clipboardCheckText, Clipboard.GetText()));
            }
        }

        private void EndTest()
        {
            var appWindow = MainWindow;
            if (appWindow == null || appWindow.IsDisposed || !IsFormOpen(appWindow))
            {
                return;
            }

            try
            {
                if (GetTestExceptions().Count == 0)
                {
                    WaitForConditionUI(5000, () => OpenForms.Count() == 1);
                }
            }
            catch (Exception x)
            {
                // An exception occurred outside RunTest
                AddTestException(x);
            }

            CloseOpenForms(MainWindow.GetType());

            _testCompleted = true;

            try
            {
                // Clear the clipboard to avoid the appearance of a memory leak.
                // ClipboardEx.Release();
                // Occasionally this causes an InvalidOperationException during stress testing.
                RunUI(MainWindow.Close);
            }
            // ReSharper disable EmptyGeneralCatchClause
            catch (System.ComponentModel.InvalidAsynchronousStateException)
            {
                // This gets thrown a lot during nightly tests under Windows 10
            }
            catch (InvalidOperationException)
            // ReSharper restore EmptyGeneralCatchClause
            {
            }
        }

        public bool WaitForConditionUI(int millis, Func<bool> func, Func<string> timeoutMessage = null, bool failOnTimeout = true, bool throwOnProgramException = true)
        {
            int waitCycles = GetWaitCycles(millis);
            for (int i = 0; i < waitCycles; i++)
            {
                if (throwOnProgramException)
                    Assert.IsFalse(GetTestExceptions().Any(), "Exception while running test");

                bool isCondition = false;
                MainWindow.Invoke(new Action(() => isCondition = func()));
                if (isCondition)
                    return true;
                Thread.Sleep(SLEEP_INTERVAL);

                // Assistance in chasing down intermittent timeout problems
                if (i == waitCycles - 1)
                {
                    System.Diagnostics.Debugger.Launch(); // Try again, under the debugger
                    System.Diagnostics.Debugger.Break();
                    i = 0; // For debugging ease - stay in loop
                }
            }
            if (failOnTimeout)
            {
                var msg = string.Empty;
                if (timeoutMessage != null)
                    RunUI(() => msg = " (" + timeoutMessage() + ")");

                AssertEx.Fail(@"Timeout {0} seconds exceeded in WaitForConditionUI{1}. Open forms: {2}", waitCycles * SLEEP_INTERVAL / 1000, msg, GetOpenFormsString());
            }
            return false;
        }

        private static IEnumerable<Form> OpenForms
        {
            get
            {
                return FormUtil.OpenForms;
            }
        }

        private void CloseOpenForms(Type exceptType)
        {
            // Actually throwing an exception can cause an infinite loop in MSTest
            var openForms = OpenForms.Where(form => form.GetType() != exceptType).ToList();
            GetTestExceptions().AddRange(
                from form in openForms
                select new AssertFailedException(
                    string.Format(@"Form of type {0} left open at end of test", form.GetType())));
            while (openForms.Count > 0)
                CloseOpenForm(openForms.First(), openForms);
        }

        private void CloseOpenForm(Form formToClose, List<Form> openForms)
        {
            openForms.Remove(formToClose);
            // Close any owned forms, since they may be pushing message loops that would keep this form
            // from closing.
            foreach (var ownedForm in formToClose.OwnedForms)
            {
                CloseOpenForm(ownedForm, openForms);
            }

            RunUI(() =>
            {
                try
                {
                    formToClose.Close();
                }
                catch
                {
                    // Ignore exceptions
                }
            });
        }

        protected void RunUI([InstantHandle] Action act)
        {
            AppInvoke(() =>
            {
                try
                {
                    act();
                }
                catch (Exception e)
                {
                    Assert.Fail(e.ToString());
                }
            });
        }

        private void AppInvoke(Action act)
        {
            MainWindow?.Invoke(act);
        }

        private void AppBeginInvoke(Action act)
        {
            MainWindow?.BeginInvoke(act);
        }


        // -------------------------------------------------------------------------------------------
        // Copied from AbstractUnitTest.cs 
        // -------------------------------------------------------------------------------------------

        /// <summary>
        /// Tracks which zip files were downloaded this run, and which might possibly be stale
        /// </summary>
        public Dictionary<string, bool> DictZipFileIsKnownCurrent { get; private set; }
        public string TestFilesZip
        {
            get
            {
                // ReSharper disable LocalizableElement
                Assert.AreEqual(1, _testFilesZips.Length, "Attempt to use TestFilesZip on test with multiple ZIP files.\nUse TestFilesZipPaths instead.");
                // ReSharper restore LocalizableElement
                return _testFilesZips[0];
            }
            set { TestFilesZipPaths = new[] { value }; }
        }

        private string[] _testFilesZips;
        public TestFilesDir[] TestFilesDirs { get; set; }
        public string TestDirectoryName { get; set; }

        // ReSharper disable UnusedAutoPropertyAccessor.Global
        // ReSharper disable MemberCanBeProtected.Global
        public TestContext TestContext { get; set; }
        // ReSharper restore MemberCanBeProtected.Global
        // ReSharper restore UnusedAutoPropertyAccessor.Global

        public bool IsExtractHere(int zipPathIndex)
        {
            return TestFilesZipExtractHere != null && TestFilesZipExtractHere[zipPathIndex];
        }

        /// <summary>
        /// One bool per TestFilesZipPaths indicating whether to unzip in the root directory (true) or a sub-directory (false or null)
        /// </summary>
        public bool[] TestFilesZipExtractHere { get; set; }

        public string[] TestFilesZipPaths
        {
            get { return _testFilesZips; }
            set
            {
                string[] zipPaths = value;
                _testFilesZips = new string[zipPaths.Length];
                DictZipFileIsKnownCurrent = new Dictionary<string, bool>();
                for (int i = 0; i < zipPaths.Length; i++)
                {
                    var zipPath = zipPaths[i];
                    // If the file is on the web, save it to the local disk in the developer's
                    // Downloads folder for future use
                    if (zipPath.Substring(0, 8).ToLower().Equals(@"https://") || zipPath.Substring(0, 7).ToLower().Equals(@"http://"))
                    {
                        GetTargetZipFilePath(zipPath, out var zipFilePath);
                        DictZipFileIsKnownCurrent.Add(zipPath, false); // May wish to retry test with a fresh download if it fails

                        zipPath = zipFilePath;
                    }
                    _testFilesZips[i] = zipPath;
                }
            }
        }

        private static void GetTargetZipFilePath(string zipPath, out string zipFilePath)
        {
            var downloadsFolder = PathEx.GetDownloadsPath();
            var urlFolder = zipPath.Split('/')[zipPath.Split('/').Length - 2]; // usually "tutorial" or "PerfTest"
            var targetFolder =
                Path.Combine(downloadsFolder, char.ToUpper(urlFolder[0]) + urlFolder.Substring(1)); // "tutorial"->"Tutorial"
            var fileName = zipPath.Substring(zipPath.LastIndexOf('/') + 1);
            zipFilePath = Path.Combine(targetFolder, fileName);
        }

        // -------------------------------------------------------------------------------------------
        // Copied from AbstractUnitTest.cs (End)
        // -------------------------------------------------------------------------------------------
    }
}