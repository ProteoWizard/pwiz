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
using System.Data;
using System.Data.OleDb;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Controls;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.Fasta;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using TestRunnerLib;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// Test method attribute which hides the test from SkylineTester.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class NoLocalizationAttribute : Attribute
    {
    }

    /// <summary>
    /// All Skyline functional tests MUST derive from this base class.
    /// Perf tests (long running, huge-data-downloading) should be declared
    /// in the TestPerf namespace, where they receive special handling so as
    /// to not disturb the normal, frequent use of the main body of tests.
    /// </summary>
    public abstract class AbstractFunctionalTest : AbstractUnitTest
    {
        private const int SLEEP_INTERVAL = 100;
        private const int WAIT_TIME = 3 * 60 * 1000;    // 3 minutes (was 1 minute, but in code coverage testing that may be too impatient)

        static AbstractFunctionalTest()
        {
            IsCheckLiveReportsCompatibility = false;
        }

        private bool _testCompleted;

        public static SkylineWindow SkylineWindow { get { return Program.MainWindow; } }

        protected bool ForceMzml { get; set; }

        protected bool UseRawFiles
        {
            get
            {
                return !ForceMzml &&
                    ExtensionTestContext.CanImportThermoRaw &&
                    ExtensionTestContext.CanImportAgilentRaw &&
                    ExtensionTestContext.CanImportAbWiff &&
                    ExtensionTestContext.CanImportWatersRaw;
            }
        }

        protected string ExtThermoRaw
        {
            get { return UseRawFiles ? ExtensionTestContext.ExtThermoRaw : ExtensionTestContext.ExtMzml; }
        }

        protected string ExtAbWiff
        {
            get { return UseRawFiles ? ExtensionTestContext.ExtAbWiff : ExtensionTestContext.ExtMzml; }
        }

        protected string ExtAgilentRaw
        {
            get { return UseRawFiles ? ExtensionTestContext.ExtAgilentRaw : ExtensionTestContext.ExtMzml; }
        }

        protected void RunWithOldReports(Action test)
        {
            TestContext.Properties["LiveReports"] = false.ToString();
            test();
        }

        protected static TDlg ShowDialog<TDlg>(Action act) where TDlg : Form
        {
            var existingDialog = FindOpenForm<TDlg>();
            if (existingDialog != null)
                Assert.IsNull(existingDialog, typeof(TDlg) + " is already open");

            SkylineBeginInvoke(act);
            TDlg dlg = WaitForOpenForm<TDlg>();
            Assert.IsNotNull(dlg);
            return dlg;
        }

        protected static void RunUI(Action act)
        {
            SkylineInvoke(() =>
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

        protected virtual bool ShowStartPage {get { return false; }}
        protected virtual List<string> SetMru { get { return new List<string>(); } }

        private static void SkylineInvoke(Action act)
        {
            if (null != SkylineWindow)
            {
                SkylineWindow.Invoke(act);
            }
            else
            {
                FindOpenForm<StartPage>().Invoke(act);
            }
        }

        private static void SkylineBeginInvoke(Action act)
        {
            if (null != SkylineWindow)
            {
                SkylineWindow.BeginInvoke(act);
            }
            else
            {
                FindOpenForm<StartPage>().BeginInvoke(act);
            }
        }

        protected static void RunDlg<TDlg>(Action show, Action<TDlg> act = null, bool pause = false) where TDlg : Form
        {
            RunDlg(show, false, act, pause);
        }

        protected static void RunDlg<TDlg>(Action show, bool waitForDocument, Action<TDlg> act = null, bool pause = false) where TDlg : Form
        {
            var doc = SkylineWindow.Document;
            TDlg dlg = ShowDialog<TDlg>(show);
            if (pause)
                PauseTest();
            RunUI(() =>
            {
                if (act != null)
                    act(dlg);
                else
                    dlg.CancelButton.PerformClick();
            });
            WaitForClosedForm(dlg);
            if (waitForDocument)
                WaitForDocumentChange(doc);
        }

        protected static void SelectNode(SrmDocument.Level level, int iNode)
        {
            var pathSelect = SkylineWindow.Document.GetPathTo((int)level, iNode);
            RunUI(() => SkylineWindow.SequenceTree.SelectedPath = pathSelect);
        }

        protected static void ActivateReplicate(string name)
        {
            RunUI(() => SkylineWindow.ActivateReplicate(name));
        }

        /// <summary>
        /// Sets the clipboard text, failing with a useful message if the
        /// SetText() method throws an exception, invoking the UI thread first.
        /// </summary>
        protected static void SetClipboardTextUI(string text)
        {
            RunUI(() => SetClipboardText(text));
        }

        /// <summary>
        /// Sets the clipboard text, failing with a useful message if the
        /// SetText() method throws an exception.  This function must be called
        /// on the UI thread.  If the calling code is not in the UI thread,
        /// use <see cref="SetClipboardTextUI"/> instead.
        /// </summary>
        protected static void SetClipboardText(string text)
        {
            try
            {
                ClipboardEx.UseInternalClipboard();
                ClipboardEx.Clear();
                ClipboardEx.SetText(text);
            }
            catch (ExternalException)
            {
                Assert.Fail(ClipboardHelper.GetOpenClipboardMessage("Failed to set text to the clipboard.")); // Not L10N
            }
        }

        protected static void SetCsvFileClipboardText(string filePath, bool hasHeader = false)
        {
            SetClipboardText(GetCsvFileText(filePath, hasHeader));
        }

        protected static string GetCsvFileText(string filePath, bool hasHeader = false)
        {
            string resultStr;
            if (TextUtil.CsvSeparator == TextUtil.SEPARATOR_CSV)
            {
                resultStr = File.ReadAllText(filePath);
            }
            else
            {
                var sb = new StringBuilder();
                string decimalSep = CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator;
                string decimalIntl = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
                foreach (var line in File.ReadLines(filePath))
                {
                    string[] fields = line.ParseDsvFields(TextUtil.SEPARATOR_CSV);
                    for (int i = 0; i < fields.Length; i++)
                    {
                        double result;
                        if (double.TryParse(fields[i], NumberStyles.Number, CultureInfo.InvariantCulture, out result))
                            fields[i] = fields[i].Replace(decimalSep, decimalIntl);
                    }
                    sb.AppendLine(fields.ToCsvLine());
                }
                resultStr = sb.ToString();
            }
            if (hasHeader)
            {
                resultStr = resultStr.Substring(resultStr.IndexOf('\n') + 1);
            }
            return resultStr;
        }

        protected static void SetExcelFileClipboardText(string filePath, string page, int columns, bool hasHeader)
        {
            SetClipboardText(GetExcelFileText(filePath, page, columns, hasHeader));
        }

        protected static string GetExcelFileText(string filePath, string page, int columns, bool hasHeader)
        {
            bool legacyFile = filePath.EndsWith(".xls");
            try
            {
                string connectionString = GetExcelConnectionString(filePath, hasHeader, legacyFile);
                return GetExcelFileText(connectionString, page, columns);
            }
            catch (Exception)
            {
                if (!legacyFile)
                    throw;

                // In case the system running this does not have the legacy adapter
                string connectionString = GetExcelConnectionString(filePath, hasHeader, false);
                return GetExcelFileText(connectionString, page, columns);
            }
        }

        private static string GetExcelFileText(string connectionString, string page, int columns)
        {
            var adapter = new OleDbDataAdapter(String.Format("SELECT * FROM [{0}$]", page), connectionString); // Not L10N
            var ds = new DataSet();
            adapter.Fill(ds, "TransitionListTable");
            DataTable data = ds.Tables["TransitionListTable"];
            var sb = new StringBuilder();
            foreach (DataRow row in data.Rows)
            {
                for (int i = 0; i < columns; i++)
                {
                    if (i > 0)
                        sb.Append('\t');
                    sb.Append(row[i] ?? String.Empty);
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string GetExcelConnectionString(string filePath, bool hasHeader, bool legacyFile)
        {
            string connectionFormat = legacyFile // Not L10N
                ? "Provider=Microsoft.Jet.OLEDB.4.0; data source={0}; Extended Properties=\"Excel 8.0;HDR={1}\""
                : "Provider=Microsoft.ACE.OLEDB.12.0;Password=\"\";User ID=Admin;Data Source={0};Mode=Share Deny Write;Extended Properties=\"HDR={1};\";Jet OLEDB:Engine Type=37";
            return String.Format(connectionFormat, filePath, hasHeader ? "YES" : "NO");
        }

        private static IEnumerable<Form> OpenForms
        {
            get
            {
                while (true)
                {
                    try
                    {
                        return Application.OpenForms.Cast<Form>().ToArray();
                    }
                    catch (InvalidOperationException)
                    {
                        // Application.OpenForms might be modified during the iteration.
                        // If that happens, go through the list again.
                    }
                }
            }
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

        private static int GetWaitCycles(int millis = WAIT_TIME)
        {
            int waitCycles = millis / SLEEP_INTERVAL;

            // Wait a little longer for stress test.
            if (Program.StressTest)
            {
                waitCycles = waitCycles * 2;
            }

            if (System.Diagnostics.Debugger.IsAttached)
            {
                // When debugger is attached, some vendor readers are S-L-O-W!
                waitCycles *= 10;
            }

            // Wait longer if running multiple processes simultaneously.
            if (Program.UnitTestTimeoutMultiplier != 0)
            {
                waitCycles *= Program.UnitTestTimeoutMultiplier;
            }

            // Wait a little longer for debug build.
            if (ExtensionTestContext.IsDebugMode)
            {
                waitCycles = waitCycles * 150 / 100;
            }

            return waitCycles;
        }

        public static TDlg WaitForOpenForm<TDlg>() where TDlg : Form
        {
            int waitCycles = GetWaitCycles();
            for (int i = 0; i < waitCycles; i++)
            {
                Assert.IsFalse(Program.TestExceptions.Any(), "Exception while running test");

                TDlg tForm = FindOpenForm<TDlg>();
                if (tForm != null)
                {
                    string formType = typeof(TDlg).Name;
                    var multipleViewProvider = tForm as IMultipleViewProvider;
                    if (multipleViewProvider != null)
                    {
                        formType += "." + multipleViewProvider.ShowingFormView.GetType().Name;
                        var formName = "(" + typeof (TDlg).Name + ")";
                        RunUI(() =>
                        {
                            if (tForm.Text.EndsWith(formName))
                                tForm.Text = tForm.Text.Replace(formName, "(" + formType + ")");
                        });
                    }

                    if (_formLookup == null)
                        _formLookup = new FormLookup();
                    Assert.IsNotNull(_formLookup.GetTest(formType),
                        formType + " must be added to TestRunnerLib\\TestRunnerFormLookup.csv");

                    if (Program.PauseForms != null && Program.PauseForms.Remove(formType))
                    {
                        var formSeen = new FormSeen();
                        formSeen.Saw(formType);
                        PauseAndContinueForm.Show(string.Format("Pausing for {0}", formType));
                    }

                    return tForm;
                }

                Thread.Sleep(SLEEP_INTERVAL);
                if (i == waitCycles - 1)
                    Assert.Fail("Timeout {0} seconds exceeded in WaitForOpenForm({1}). Open forms: {2}", waitCycles * SLEEP_INTERVAL / 1000, typeof(TDlg).Name, GetOpenFormsString()); // Not L10N
            }
            return null;
        }

        private void PauseForForm(Type formType)
        {
            if (Program.PauseForms == null)
                return;
            var viewTypeName = FormSeen.GetViewType(formType);
            if (viewTypeName != null && Program.PauseForms.Remove(viewTypeName))
            {
                var formSeen = new FormSeen();
                formSeen.Saw(viewTypeName);
                PauseAndContinueForm.Show(string.Format("Pausing for {0}", viewTypeName));
            }
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

        public static void WaitForClosedForm<TDlg>() where TDlg : Form
        {
            var dlg = FindOpenForm<TDlg>();
            if (dlg != null)
                WaitForClosedForm(dlg);
        }

        public static void WaitForClosedForm(Form formClose)
        {
            int waitCycles = GetWaitCycles();
            for (int i = 0; i < waitCycles; i++)
            {
                Assert.IsFalse(Program.TestExceptions.Any(), "Exception while running test");

                bool isOpen = true;
                SkylineInvoke(() => isOpen = IsFormOpen(formClose));
                if (!isOpen)
                    return;
                Thread.Sleep(SLEEP_INTERVAL);
            }

            Assert.Fail("Timeout {0} seconds exceeded in WaitForClosedForm. Open forms: {1}", waitCycles * SLEEP_INTERVAL / 1000, GetOpenFormsString()); // Not L10N
        }

        private static string GetTextForForm(Control form)
        {
            var result = form.Text;
            var threadExceptionDialog = form as ThreadExceptionDialog;
            if (threadExceptionDialog != null)
            {
                // Locate the details text box, return the contents - much more informative than the dialog title
                result = threadExceptionDialog.Controls.Cast<Control>().Where(control => control.GetType() == typeof (TextBox)).Aggregate(result, (current, control) => current + (": " + control.Text));
            }
            return result;
        }

        private static string GetOpenFormsString()
        {
            var result =  string.Join(", ", OpenForms.Select(form => string.Format("{0} ({1})", form.GetType().Name, GetTextForForm(form))));
            if (SkylineWindow.Document != null)
            {
                var state = string.Join("\", \"", SkylineWindow.Document.NonLoadedStateDescriptions);
                if (!string.IsNullOrEmpty(state))
                   result += " Also, document is not fully loaded: \"" + state + "\"";
            }
            return result;
        }

        public static SrmDocument WaitForDocumentChange(SrmDocument docCurrent)
        {
            WaitForProteinMetadataBackgroundLoaderCompleted(); // make sure document is stable

            // Make sure the document changes on the UI thread, since tests are mostly
            // interested in interacting with the document on the UI thread.
            Assert.IsTrue(WaitForConditionUI(() => !ReferenceEquals(docCurrent, SkylineWindow.DocumentUI)));
            return SkylineWindow.Document;
        }

        public static SrmDocument WaitForDocumentLoaded(int millis = WAIT_TIME)
        {
            WaitForConditionUI(millis, () => SkylineWindow.DocumentUI.IsLoaded);
            WaitForProteinMetadataBackgroundLoaderCompleted(millis);  // make sure document is stable
            return SkylineWindow.Document;
        }

        public static SrmDocument WaitForDocumentChangeLoaded(SrmDocument docCurrent, int millis = WAIT_TIME)
        {
            WaitForDocumentChange(docCurrent);
            return WaitForDocumentLoaded(millis);
        }

        public static bool WaitForCondition(Func<bool> func)
        {
            return WaitForCondition(WAIT_TIME, func);
        }

        public static bool WaitForCondition(Func<bool> func, string timeoutMessage)
        {
            return WaitForCondition(WAIT_TIME, func, timeoutMessage);
        }

        public static bool TryWaitForCondition(Func<bool> func)
        {
            return TryWaitForCondition(WAIT_TIME, func);
        }

        public static bool TryWaitForCondition(int millis, Func<bool> func)
        {
            return WaitForCondition(millis, func, null, false);
        }

        public static bool WaitForCondition(int millis, Func<bool> func, string timeoutMessage = null, bool failOnTimeout = true)
        {
            int waitCycles = GetWaitCycles(millis);
            for (int i = 0; i < waitCycles; i++)
            {
                Assert.IsFalse(Program.TestExceptions.Any(), "Exception while running test");

                if (func())
                    return true;
                Thread.Sleep(SLEEP_INTERVAL);
                if (failOnTimeout && (i == waitCycles - 1))
                {
                    var msg = (timeoutMessage == null)
                        ? string.Empty
                        : " (" + timeoutMessage + ")";
                    Assert.Fail("Timeout {0} seconds exceeded in WaitForCondition{1}. Open forms: {2}", waitCycles * SLEEP_INTERVAL / 1000, msg, GetOpenFormsString()); // Not L10N
                }
            }
            return false;
        }

        public static bool WaitForConditionUI(Func<bool> func)
        {
            return WaitForConditionUI(WAIT_TIME, func);
        }

        public static bool WaitForConditionUI(Func<bool> func, string timeoutMessage)
        {
            return WaitForConditionUI(WAIT_TIME, func, timeoutMessage);
        }

        public static bool TryWaitForConditionUI(Func<bool> func)
        {
            return TryWaitForConditionUI(WAIT_TIME, func);
        }

        public static bool TryWaitForConditionUI(int millis, Func<bool> func)
        {
            return WaitForConditionUI(millis, func, null, false);
        }

        public static bool WaitForConditionUI(int millis, Func<bool> func, string timeoutMessage = null, bool failOnTimeout = true)
        {
            int waitCycles = GetWaitCycles(millis);
            for (int i = 0; i < waitCycles; i++)
            {
                Assert.IsFalse(Program.TestExceptions.Any(), "Exception while running test");

                bool isCondition = false;
                Program.MainWindow.Invoke(new Action(() => isCondition = func()));
                if (isCondition)
                    return true;
                Thread.Sleep(SLEEP_INTERVAL);
                if (failOnTimeout && (i == waitCycles - 1))
                {
                    var msg = (timeoutMessage == null) 
                        ? string.Empty
                        : " (" + timeoutMessage + ")";
                    Assert.Fail("Timeout {0} seconds exceeded in WaitForConditionUI{1}. Open forms: {2}", waitCycles * SLEEP_INTERVAL / 1000, msg, GetOpenFormsString()); // Not L10N
                }
            }
            return false;
        }

        public static void WaitForGraphs()
        {
            WaitForConditionUI(() => !SkylineWindow.IsGraphUpdatePending);
        }

        // Pause a test so we can play with the UI manually.
        public static void PauseTest()
        {
            PauseAndContinueForm.Show();
        }

        /// <summary>
        /// If true, calls to PauseForScreenShot used in the tutorial tests will pause
        /// the tests and wait until the pause form is dismissed, allowing a screenshot
        /// to be taken.
        /// </summary>
        private static bool _isPauseForScreenShots;

        public static bool IsPauseForScreenShots
        {
            get { return _isPauseForScreenShots || Program.PauseSeconds < 0; }
            set
            {
                _isPauseForScreenShots = value;
                if (_isPauseForScreenShots)
                {
                    Program.PauseSeconds = -1;
                    Settings.Default.TestSmallMolecules = false; // Extra test node will mess up the pretty pictures
                }
            }
        }

        public static bool IsShowMatchingTutorialPages { get; set; }

        public static bool IsDemoMode { get { return Program.DemoMode; } }

        public static bool IsCheckLiveReportsCompatibility { get; set; }

        public string LinkPdf { get; set; }

        private string LinkPage(int? pageNum)
        {
            return pageNum.HasValue ? LinkPdf + "#page=" + pageNum : null;
        }

        private static FormLookup _formLookup;

        public void PauseForScreenShot(string description = null, int? pageNum = null)
        {
            PauseForScreenShot(description, pageNum, null);
        }

        public void PauseForScreenShot<TView>(string description, int? pageNum = null)
            where TView : IFormView
        {
            PauseForScreenShot(description, pageNum, typeof(TView));
        }

        private void PauseForScreenShot(string description, int? pageNum, Type formType)
        {
            if (IsCheckLiveReportsCompatibility)
                CheckReportCompatibility.CheckAll(SkylineWindow.Document);
            if (IsDemoMode)
                Thread.Sleep(3 * 1000);
            else if (Program.PauseSeconds > 0)
                Thread.Sleep(Program.PauseSeconds * 1000);
            else if (IsPauseForScreenShots)
            {
                var formSeen = new FormSeen();
                formSeen.Saw(formType);
                if (pageNum.HasValue)
                    description = string.Format("page {0} - {1}", pageNum, description);
                bool showMathingPages = IsShowMatchingTutorialPages || Program.ShowMatchingPages;
                PauseAndContinueForm.Show(description, LinkPage(pageNum), showMathingPages);
            }
            else
            {
                PauseForForm(formType);
            }
        }

        public static void OkDialog(Form form, Action okAction)
        {
            RunUI(okAction);
            WaitForClosedForm(form);
        }

        /// <summary>
        /// Starts up Skyline, and runs the <see cref="DoTest"/> test method.
        /// </summary>
        protected void RunFunctionalTest()
        {
            try
            {

                if (IsPerfTest && !RunPerfTests)
                {
                    return;  // Don't want to run this lengthy test right now
                }
                Program.FunctionalTest = true;
                Program.TestExceptions = new List<Exception>();
                LocalizationHelper.InitThread();

                // Unzip test files.
                if (TestFilesZipPaths != null)
                {
                    TestFilesDirs = new TestFilesDir[TestFilesZipPaths.Length];
                    for (int i = 0; i < TestFilesZipPaths.Length; i++)
                    {
                        TestFilesDirs[i] = new TestFilesDir(TestContext, TestFilesZipPaths[i], TestDirectoryName, TestFilesPersistent);
                    }
                }

                // Run test in new thread (Skyline on main thread).
                Program.Init();
                Settings.Default.SrmSettingsList[0] = SrmSettingsList.GetDefault();
                // Reset defaults with names from resources for testing different languages
                Settings.Default.BackgroundProteomeList[0] = BackgroundProteomeList.GetDefault();
                Settings.Default.DeclusterPotentialList[0] = DeclusterPotentialList.GetDefault();
                Settings.Default.RetentionTimeList[0] = RetentionTimeList.GetDefault();
                Settings.Default.ShowStartupForm = ShowStartPage;
                Settings.Default.MruList = SetMru;
                // For automated demos, start with the main window maximized
                if (IsDemoMode)
                    Settings.Default.MainWindowMaximized = true;
                var threadTest = new Thread(WaitForSkyline) { Name = "Functional test thread" }; // Not L10N
                LocalizationHelper.InitThread(threadTest);
                threadTest.Start();
                Program.Main();
                threadTest.Join();

                // Were all windows disposed?
                FormEx.CheckAllFormsDisposed();
                CommonFormEx.CheckAllFormsDisposed();

                Settings.Default.SrmSettingsList[0] = SrmSettingsList.GetDefault(); // Release memory held in settings
            }
            catch (Exception x)
            {
                Program.AddTestException(x);
            }

            // Delete unzipped test files.
            if (TestFilesDirs != null)
            {
                foreach (TestFilesDir dir in TestFilesDirs)
                {
                    if (dir == null)
                        continue;
                    try
                    {
                        dir.Dispose();
                    }
                    catch (Exception x)
                    {
                        Program.AddTestException(x);
                    }
                }
            }

            if (Program.TestExceptions.Count > 0)
            {
                //Log<AbstractFunctionalTest>.Exception("Functional test exception", Program.TestExceptions[0]); // Not L10N
                const string errorSeparator = "------------------------------------------------------";
                Assert.Fail("{0}{1}{2}{3}",
                    Environment.NewLine + Environment.NewLine,
                    errorSeparator + Environment.NewLine,
                    Program.TestExceptions[0],
                    Environment.NewLine + errorSeparator + Environment.NewLine);
            }

            if (!_testCompleted)
            {
                //Log<AbstractFunctionalTest>.Fail("Functional test did not complete"); // Not L10N
                Assert.Fail("Functional test did not complete");
            }
        }

        private void WaitForSkyline()
        {
            try
            {
                int waitCycles = GetWaitCycles();
                for (int i = 0; i < waitCycles; i++)
                {
                    if (Program.MainWindow != null && Program.MainWindow.IsHandleCreated)
                        break;
                    if (ShowStartPage && null != FindOpenForm<StartPage>()) {
                        break;
                    }
                    Thread.Sleep(SLEEP_INTERVAL);
                }
                if (!ShowStartPage) 
                {
                    Assert.IsTrue(Program.MainWindow != null && Program.MainWindow.IsHandleCreated,
                    "Timeout {0} seconds exceeded in WaitForSkyline", waitCycles * SLEEP_INTERVAL / 1000); // Not L10N
                }
                Settings.Default.Reset();
                RunTest();
            }
            catch (Exception x)
            {
                // Save exception for reporting from main thread.
                Program.AddTestException(x);
            }

            EndTest();
        }

        private void RunTest()
        {
            if (null != SkylineWindow)
            {
                // Clean-up before running the test
                RunUI(() => SkylineWindow.UseKeysOverride = true);

                // Make sure the background proteome and sequence tree protein metadata loaders don't hit the web (unless they are meant to)
                bool allowInternetAccess = AllowInternetAccess; // Local copy for easy change in debugger when needed
                if (!allowInternetAccess)
                {
                    var protdbLoader = SkylineWindow.BackgroundProteomeManager;
                    protdbLoader.FastaImporter =
                        new WebEnabledFastaImporter(new WebEnabledFastaImporter.FakeWebSearchProvider());
                    var treeLoader = SkylineWindow.ProteinMetadataManager;
                    treeLoader.FastaImporter =
                        new WebEnabledFastaImporter(new WebEnabledFastaImporter.FakeWebSearchProvider());
                }
            }

            // Use internal clipboard for testing so that we don't collide with other processes
            // using the clipboard during a test run.
            ClipboardEx.UseInternalClipboard();
            ClipboardEx.Clear();

            var doClipboardCheck = TestContext.Properties.Contains("ClipboardCheck"); // Not L10N
            string clipboardCheckText = doClipboardCheck ? (string)TestContext.Properties["ClipboardCheck"] : String.Empty; // Not L10N
            if (doClipboardCheck)
            {
                RunUI(() => Clipboard.SetText(clipboardCheckText));
            }

            DoTest();
            if (null != SkylineWindow)
            {
                AssertEx.ValidatesAgainstSchema(SkylineWindow.Document);
            }

            if (doClipboardCheck)
            {
                RunUI(() => Assert.AreEqual(clipboardCheckText, Clipboard.GetText()));
            }
        }

        private void EndTest()
        {
            var skylineWindow = Program.MainWindow;
            if (skylineWindow == null || skylineWindow.IsDisposed || !IsFormOpen(skylineWindow))
                return;

            try
            {
                // Release all resources by setting the document to something that
                // holds no file handles.
                var docNew = new SrmDocument(SrmSettingsList.GetDefault());
                RunUI(() => SkylineWindow.SwitchDocument(docNew, null));
                WaitForGraphs();

                // Restore minimal View to close dock windows.
                RestoreMinimalView();

                if (Program.TestExceptions.Count == 0)
                {
                    // Long wait for library build notifications
                    RunUI(() => SkylineWindow.RemoveLibraryBuildNotification());
                    WaitForConditionUI(() => !OpenForms.Any(f => f is BuildLibraryNotification));
                    // Short wait for anything else
                    WaitForConditionUI(5000, () => OpenForms.Count() == 1);
                }
            }
            catch (Exception x)
            {
                // An exception occurred outside RunTest
                Program.AddTestException(x);
            }

            foreach (var messageDlg in OpenForms.OfType<MessageDlg>())
            {
// ReSharper disable LocalizableElement
                Console.WriteLine("\n\nOpen MessageDlg: {0}\n", messageDlg.Message); // Not L10N
// ReSharper restore LocalizableElement
            }

            // Actually throwing an exception can cause an infinite loop in MSTest
            var openForms = OpenForms.Where(form => !(form is SkylineWindow)).ToList();
            Program.TestExceptions.AddRange(
                from form in openForms
                select new AssertFailedException(
                    String.Format("Form of type {0} left open at end of test", form.GetType()))); // Not L10N
            RunUI(() =>
            {
                foreach (var form in openForms)
                {
                    try
                    {
                        form.Close();
                    }
// ReSharper disable EmptyGeneralCatchClause
                    catch
// ReSharper restore EmptyGeneralCatchClause
                    {
                    }
                }
            });

            _testCompleted = true;

            // Clear the clipboard to avoid the appearance of a memory leak.
            ClipboardEx.Release();

            try
            {
                // Occasionally this causes an InvalidOperationException during stress testing.
                RunUI(SkylineWindow.Close);
            }
// ReSharper disable EmptyGeneralCatchClause
            catch (InvalidOperationException)
// ReSharper restore EmptyGeneralCatchClause
            {
            }
        }

        
        /// <summary>
        /// Restore minimal view layout in order to close extra windows. 
        /// </summary>
        private void RestoreMinimalView()
        {
            var assembly = Assembly.GetAssembly(typeof(AbstractFunctionalTest));
            var layoutStream = assembly.GetManifestResourceStream(
                typeof(AbstractFunctionalTest).Namespace + ".minimal.sky.view"); // Not L10N
            Assert.IsNotNull(layoutStream);
            RunUI(() => SkylineWindow.LoadLayout(layoutStream));
            WaitForConditionUI(() => true);
        }

        public void RestoreViewOnScreen(int pageNum)
        {
            var viewsDir = TestFilesDirs.First(dir => dir.FullPath.EndsWith("Views"));
            RestoreViewOnScreen(viewsDir.GetTestPath(string.Format(@"p{0:0#}.view", pageNum)));
        }

        public void RestoreViewOnScreen(string viewFilePath)
        {
            if (!Program.SkylineOffscreen)
            {
                RunUI(() =>
                {
                    using (var fileStream = new FileStream(viewFilePath, FileMode.Open))
                    {
                        SkylineWindow.LoadLayout(fileStream);
                    }
                });
            }
        }

        protected abstract void DoTest();

        public void FindNode(string searchText)
        {
            RunDlg<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg, findPeptideDlg =>
            {
                findPeptideDlg.FindOptions = new FindOptions().ChangeText(searchText).ChangeForward(true);
                findPeptideDlg.FindNext();
                findPeptideDlg.Close();
            });
        }

        public static void RemovePeptide(string peptideSequence, bool isDecoy = false)
        {
            var docStart = SkylineWindow.Document;
            var nodePeptide = docStart.Peptides.FirstOrDefault(nodePep =>
                Equals(peptideSequence, nodePep.Peptide.Sequence) &&
                isDecoy == nodePep.IsDecoy);

            Assert.IsNotNull(nodePeptide);

            RunDlg<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg, findPeptideDlg =>
            {
                findPeptideDlg.SearchString = peptideSequence;
                findPeptideDlg.FindNext();
                while (!SkylineWindow.SequenceTree.SelectedDocNodes.Contains(nodePeptide))
                    findPeptideDlg.FindNext();
                findPeptideDlg.Close();
            });

            RunUI(SkylineWindow.EditDelete);

            Assert.IsTrue(WaitForCondition(() => !SkylineWindow.Document.Peptides.Any(nodePep =>
                Equals(peptideSequence, nodePep.Peptide.Sequence) &&
                isDecoy == nodePep.IsDecoy)));
            if (nodePeptide == null)
                Assert.Fail(); // Resharper
            AssertEx.IsDocumentState(SkylineWindow.Document, null,
                                     docStart.PeptideGroupCount,
                                     docStart.PeptideCount - 1,
                                     docStart.PeptideTransitionGroupCount - nodePeptide.TransitionGroupCount,
                                     docStart.PeptideTransitionCount - nodePeptide.TransitionCount);
        }

        public static SrmDocument WaitForProteinMetadataBackgroundLoaderCompletedUI(int millis = WAIT_TIME)
        {
            // In a functional test we expect the protein metadata search to at least pretend to have gone to the web
            WaitForCondition(millis, () => ProteinMetadataManager.IsLoadedDocument(SkylineWindow.Document)); // Make sure doc is stable
            WaitForConditionUI(millis, () => ProteinMetadataManager.IsLoadedDocument(SkylineWindow.DocumentUI)); // Then make sure UI ref is current
            return SkylineWindow.Document;
        }

        public static SrmDocument WaitForProteinMetadataBackgroundLoaderCompleted(int millis = WAIT_TIME)
        {
            // In a functional test we expect the protein metadata search to at least pretend to have gone to the web
            WaitForCondition(millis, () => ProteinMetadataManager.IsLoadedDocument(SkylineWindow.Document));
            return SkylineWindow.Document;
        }

        public static void WaitForBackgroundProteomeLoaderCompleted()
        {
            WaitForCondition(() => BackgroundProteomeManager.DocumentHasLoadedBackgroundProteomeOrNone(SkylineWindow.Document, true)); 
        }

        #region Modification helpers

        public static PeptideSettingsUI ShowPeptideSettings()
        {
            return ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
        }

        public static EditListDlg<SettingsListBase<StaticMod>, StaticMod> ShowEditStaticModsDlg(PeptideSettingsUI peptideSettingsUI)
        {
            return ShowDialog<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(peptideSettingsUI.EditStaticMods);
        }

        public static EditListDlg<SettingsListBase<StaticMod>, StaticMod> ShowEditHeavyModsDlg(PeptideSettingsUI peptideSettingsUI)
        {
            return ShowDialog<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(peptideSettingsUI.EditHeavyMods);
        }

        public static EditStaticModDlg ShowAddModDlg(EditListDlg<SettingsListBase<StaticMod>, StaticMod> editModsDlg)
        {
            return ShowDialog<EditStaticModDlg>(editModsDlg.AddItem);
        }

        public void AddStaticMod(StaticMod mod, PeptideSettingsUI peptideSettingsUI, string pauseText = null, int? pausePage = null)
        {
            var editStaticModsDlg = ShowEditStaticModsDlg(peptideSettingsUI);
            RunUI(editStaticModsDlg.SelectLastItem);
            AddMod(mod, editStaticModsDlg, pauseText, pausePage, typeof(EditStaticModDlg.StructuralModView));
        }

        public void AddHeavyMod(StaticMod mod, PeptideSettingsUI peptideSettingsUI, string pauseText = null, int? pausePage = null)
        {
            var editStaticModsDlg = ShowEditHeavyModsDlg(peptideSettingsUI);
            RunUI(editStaticModsDlg.SelectLastItem);
            AddMod(mod, editStaticModsDlg, pauseText, pausePage, typeof(EditStaticModDlg.IsotopeModView));
        }

        private void AddMod(StaticMod mod,
                            EditListDlg<SettingsListBase<StaticMod>, StaticMod> editModsDlg,
                            string pauseText,
                            int? pausePage,
                            Type viewType)
        {
            var addStaticModDlg = ShowAddModDlg(editModsDlg);
            RunUI(() => addStaticModDlg.Modification = mod);

            if (pauseText != null || pausePage.HasValue)
                PauseForScreenShot(pauseText, pausePage, viewType);

            OkDialog(addStaticModDlg, addStaticModDlg.OkDialog);
            OkDialog(editModsDlg, editModsDlg.OkDialog);
        }

        public static void AddStaticMod(string uniModName, bool isVariable, PeptideSettingsUI peptideSettingsUI)
        {
            var editStaticModsDlg = ShowEditStaticModsDlg(peptideSettingsUI);
            RunUI(editStaticModsDlg.SelectLastItem);
            AddMod(uniModName, isVariable, editStaticModsDlg);
        }

        public static void AddHeavyMod(string uniModName, PeptideSettingsUI peptideSettingsUI)
        {
            var editStaticModsDlg = ShowEditHeavyModsDlg(peptideSettingsUI);
            RunUI(editStaticModsDlg.SelectLastItem);
            AddMod(uniModName, false, editStaticModsDlg);
        }

        private static void AddMod(string uniModName, bool isVariable, EditListDlg<SettingsListBase<StaticMod>, StaticMod> editModsDlg)
        {
            var addStaticModDlg = ShowAddModDlg(editModsDlg);
            RunUI(() =>
            {
                addStaticModDlg.SetModification(uniModName, isVariable);
                addStaticModDlg.OkDialog();
            });
            WaitForClosedForm(addStaticModDlg);

            RunUI(editModsDlg.OkDialog);
            WaitForClosedForm(editModsDlg);
        }

        public static void SetStaticModifications(Func<IList<string>, IList<string>> changeMods)
        {
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, dlg =>
            {
                dlg.PickedStaticMods = changeMods(dlg.PickedStaticMods).ToArray();
                dlg.OkDialog();
            });
        }

        #endregion

        #region Results helpers

        public void ImportResultsFile(string fileName, int waitForLoadSeconds = 420, string expectedErrorMessage = null,
            LockMassParameters lockMassParameters = null)
        {
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            RunDlg<OpenDataSourceDialog>(() => importResultsDlg.NamedPathSets = importResultsDlg.GetDataSourcePathsFile(null),
               openDataSourceDialog =>
               {
                   openDataSourceDialog.SelectFile(fileName);
                   openDataSourceDialog.Open();
               });
            WaitForConditionUI(() => importResultsDlg.NamedPathSets != null);
            RunUI(importResultsDlg.OkDialog);
            if (lockMassParameters != null)
            {
                var dlg = WaitForOpenForm<ImportResultsLockMassDlg>();
                dlg.LockmassPositive = lockMassParameters.LockmassPositive ?? 0;
                dlg.LockmassNegative = lockMassParameters.LockmassNegative ?? 0;
                dlg.LockmassTolerance = lockMassParameters.LockmassTolerance ?? 0;
                dlg.OkDialog();
            } 
            if (expectedErrorMessage != null)
            {
                var dlg = WaitForOpenForm<MessageDlg>();
                Assert.IsTrue(dlg.DetailMessage.Contains(expectedErrorMessage));
                dlg.CancelDialog();
            }
            else
            {
                WaitForCondition(waitForLoadSeconds * 1000,
                    () => SkylineWindow.Document.Settings.HasResults && SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);
            }
        }

        public void ImportResultsReplicatesCE(string replicatesDirName, int waitForLoadSeconds = 420)
        {

            RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg =>
            {
                importResultsDlg.RadioAddNewChecked = true;
                importResultsDlg.OptimizationName = ExportOptimize.CE;
                importResultsDlg.NamedPathSets = DataSourceUtil.GetDataSourcesInSubdirs(replicatesDirName).ToArray();
                string prefix = ImportResultsDlg.GetCommonPrefix(Array.ConvertAll(importResultsDlg.NamedPathSets, ns => ns.Key));
                // Rename all the replicates to remove the specified prefix, so that dialog doesn't pop up.
                for (int i = 0; i < importResultsDlg.NamedPathSets.Length; i++)
                {
                    var namedSet = importResultsDlg.NamedPathSets[i];
                    importResultsDlg.NamedPathSets[i] = new KeyValuePair<string, MsDataFileUri[]>(
                        namedSet.Key.Substring(prefix.Length), namedSet.Value);
                }
                importResultsDlg.OkDialog();
            });

            WaitForCondition(waitForLoadSeconds * 1000,
                () => SkylineWindow.Document.Settings.HasResults && SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);
        }

        public void ImportResultsFiles(IEnumerable<MsDataFileUri> fileNames, int waitForLoadSeconds = 420)
        {
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            RunUI(() => importResultsDlg.NamedPathSets = importResultsDlg.GetDataSourcePathsFileReplicates(fileNames));

            string prefix = ImportResultsDlg.GetCommonPrefix(fileNames.Select(f => f.GetFileName()));
            if (prefix.Length < ImportResultsDlg.MIN_COMMON_PREFIX_LENGTH)
            {
                OkDialog(importResultsDlg, importResultsDlg.OkDialog);
            }
            else
            {
                ImportResultsNameDlg importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importResultsDlg.OkDialog);
                RunUI(importResultsNameDlg.YesDialog);
            }
            WaitForCondition(waitForLoadSeconds * 1000,
                () => SkylineWindow.Document.Settings.HasResults && SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);
        }

        /// <summary>
        /// Imports results in a directory with an extension and potentially a filter.
        /// </summary>
        /// <param name="dirPath">The directory path in which the data files are found</param>
        /// <param name="ext">The extension of the data files (e.g. raw, wiff, mzML, ...)</param>
        /// <param name="filter">A filter string the files must contain or null for no extra filtering</param>
        /// <param name="removePrefix">True to remove a shared prefix for the files</param>
        public void ImportResultsFiles(string dirPath, string ext, string filter, bool? removePrefix)
        {
            var doc = SkylineWindow.Document;
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            var openDataSourceDialog = ShowDialog<OpenDataSourceDialog>(() =>
                importResultsDlg.NamedPathSets = importResultsDlg.GetDataSourcePathsFile(null));
            RunUI(() =>
            {
                openDataSourceDialog.CurrentDirectory = new MsDataFilePath(dirPath);
                openDataSourceDialog.SelectAllFileType(ext, path => filter == null || path.Contains(filter));
                openDataSourceDialog.Open();
            });
            WaitForConditionUI(() => importResultsDlg.NamedPathSets != null);

            if (!removePrefix.HasValue)
                OkDialog(importResultsDlg, importResultsDlg.OkDialog);
            else
            {
                var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importResultsDlg.OkDialog);
                PauseForScreenShot();

                if (removePrefix.Value)
                    OkDialog(importResultsNameDlg, importResultsNameDlg.YesDialog);
                else
                    OkDialog(importResultsNameDlg, importResultsNameDlg.NoDialog);
            }
            WaitForDocumentChange(doc);
        }

        #endregion
    }
}
