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
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;

namespace pwiz.SkylineTestUtil
{
    public abstract class AbstractFunctionalTest
    {
        private const int SLEEP_INTERVAL = 100;
        private const int WAIT_TIME = 60*1000;    // 1 minute

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }
        private string[] _testFilesZips;
        public string TestFilesZip
        {
            get
            {
                Assert.AreEqual(1, _testFilesZips.Length, "Attempt to use TestFilesZip on test with multiple ZIP files.\nUse TestFilesZipPaths instead.");
                return _testFilesZips[0];
            }
            set { TestFilesZipPaths = new[] {value}; }
        }

        public string[] TestFilesZipPaths
        {
            get { return _testFilesZips; }
            set
            {
                string[] zipPaths = value;
                _testFilesZips = new string[zipPaths.Length];
                for (int i = 0; i < zipPaths.Length; i++)
                {
                    var zipPath = zipPaths[i];
                    // If the file is on the web, save it to the local disk in the developer's
                    // Downloads folder for future use
                    if (zipPath.Substring(0, 8).ToLower().Equals("https://") || zipPath.Substring(0, 7).ToLower().Equals("http://"))
                    {
                        string desktopFolder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                        string downloadsFolder = Path.Combine(Path.GetDirectoryName(desktopFolder) ?? "", "Downloads");
                        string tutorialsFolder = Path.Combine(downloadsFolder, "Tutorials");
                        string fileName = zipPath.Substring(zipPath.LastIndexOf('/') + 1);
                        string zipFilePath = Path.Combine(tutorialsFolder, fileName);
                        if (!File.Exists(zipFilePath))
                        {
                            if (!Directory.Exists(tutorialsFolder))
                                Directory.CreateDirectory(tutorialsFolder);

                            WebClient webClient = new WebClient();
                            webClient.DownloadFile(zipPath, zipFilePath);
                        }
                        zipPath = zipFilePath;
                    }
                    _testFilesZips[i] = zipPath;
                }
            }
        }

        public string TestDirectoryName { get; set; }
        public TestFilesDir TestFilesDir
        {
            get
            {
                Assert.AreEqual(1, TestFilesDirs.Length, "Attempt to use TestFilesDir on test with multiple directories.\nUse TestFilesDirs instead.");
                return TestFilesDirs[0];
            }
            set { TestFilesDirs = new[] {value}; }
        }
        public TestFilesDir[] TestFilesDirs { get; set; }

        private readonly List<Exception> _testExceptions = new List<Exception>();
        private bool _testCompleted;

        public static SkylineWindow SkylineWindow { get { return Program.MainWindow; } }

        protected static TDlg ShowDialog<TDlg>(Action act) where TDlg : Form
        {
            SkylineWindow.BeginInvoke(act);
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
                                  catch(Exception e)
                                  {
                                      Assert.Fail(e.ToString());
                                  }
                              });
        }
        
        private static void SkylineInvoke(Action act)
        {
            SkylineWindow.Invoke(act);
        }

        protected static void RunDlg<TDlg>(Action show, Action<TDlg> act) where TDlg : FormEx
        {
            RunDlg(show, false, act);
        }

        protected static void RunDlg<TDlg>(Action show, bool waitForDocument, Action<TDlg> act) where TDlg : FormEx
        {
            var doc = SkylineWindow.Document;
            TDlg dlg = ShowDialog<TDlg>(show);
            RunUI(() => act(dlg));
            WaitForClosedForm(dlg);
            if (waitForDocument)
                WaitForDocumentChange(doc);
        }
        
        protected static void SelectNode(SrmDocument.Level level, int iNode)
        {
            var pathSelect = SkylineWindow.Document.GetPathTo((int)level, iNode);
            RunUI(() => SkylineWindow.SequenceTree.SelectedPath = pathSelect);
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
                ClipboardEx.SetText(text);
            }
            catch (ExternalException)
            {
                Assert.Fail(ClipboardHelper.GetOpenClipboardMessage("Failed to set text to the clipboard."));
            }
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
            var adapter = new OleDbDataAdapter(string.Format("SELECT * FROM [{0}$]", page), connectionString);
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
                    sb.Append(row[i] ?? string.Empty);
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string GetExcelConnectionString(string filePath, bool hasHeader, bool legacyFile)
        {
            string connectionFormat = legacyFile
                ? "Provider=Microsoft.Jet.OLEDB.4.0; data source={0}; Extended Properties=\"Excel 8.0;HDR={1}\""
                : "Provider=Microsoft.ACE.OLEDB.12.0;Password=\"\";User ID=Admin;Data Source={0};Mode=Share Deny Write;Extended Properties=\"HDR={1};\";Jet OLEDB:Engine Type=37";
            return string.Format(connectionFormat, filePath, hasHeader ? "YES" : "NO");
        }

        public static TDlg FindOpenForm<TDlg>() where TDlg : Form
        {
            while (true) {
                try
                {
                    foreach (var form in Application.OpenForms)
                    {
                        var tForm = form as TDlg;
                        if (tForm != null && tForm.Created)
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

        private static int GetWaitCycles(int millis = WAIT_TIME)
        {
            int waitCycles = millis/SLEEP_INTERVAL;
            
            // Wait a little longer for stress test.
            if (Program.StressTest)
            {
                waitCycles = waitCycles*150/100;
            }

            // Wait longer if running multiple processes simultaneously.
            if (Program.UnitTestTimeoutMultiplier != 0)
            {
                waitCycles *= Program.UnitTestTimeoutMultiplier;
            }

            // Wait a little longer for debug build.
            if (ExtensionTestContext.IsDebugMode)
            {
                waitCycles = waitCycles*150/100;

            }

            return waitCycles;
        }

        public static TDlg WaitForOpenForm<TDlg>() where TDlg : Form
        {
            int waitCycles = GetWaitCycles();
            for (int i = 0; i < waitCycles; i++)
            {
                TDlg tForm = FindOpenForm<TDlg>();
                if (tForm != null)
                    return tForm;

                Thread.Sleep(SLEEP_INTERVAL);
                Assert.IsTrue(i < waitCycles - 1, string.Format("Timeout {0} seconds exceeded in WaitForOpenForm", waitCycles*SLEEP_INTERVAL/1000));
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
            int waitCycles = GetWaitCycles();
            for (int i = 0; i < waitCycles; i++)
            {
                bool isOpen = true;
                Program.MainWindow.Invoke(new Action(() => isOpen = IsFormOpen(formClose)));
                if (!isOpen)
                    return;
                Thread.Sleep(SLEEP_INTERVAL);
            }
            Assert.Fail(string.Format("Timeout {0} seconds exceeded in WaitForClosedForm", waitCycles*SLEEP_INTERVAL/1000));
        }

        public static SrmDocument WaitForDocumentChange(SrmDocument docCurrent)
        {
            // Make sure the document changes on the UI thread, since tests are mostly
            // interested in interacting with the document on the UI thread.
            Assert.IsTrue(WaitForConditionUI(() => !ReferenceEquals(docCurrent, SkylineWindow.DocumentUI)));
            return SkylineWindow.Document;
        }

        public static SrmDocument WaitForDocumentLoaded(int millis = WAIT_TIME)
        {
            WaitForConditionUI(millis, () => SkylineWindow.DocumentUI.Settings.IsLoaded);
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

        public static bool WaitForCondition(int millis, Func<bool> func)
        {
            int waitCycles = GetWaitCycles(millis);
            for (int i = 0; i < waitCycles; i ++)
            {
                if (func())
                    return true;
                Thread.Sleep(SLEEP_INTERVAL);
                Assert.IsTrue(i < waitCycles - 1, string.Format("Timeout {0} seconds exceeded in WaitForCondition", waitCycles*SLEEP_INTERVAL/1000));
            }
            return false;
        }

        public static bool WaitForConditionUI(Func<bool> func)
        {
            return WaitForConditionUI(WAIT_TIME, func);
        }

        public static bool WaitForConditionUI(int millis, Func<bool> func)
        {
            int waitCycles = GetWaitCycles(millis);
            for (int i = 0; i < waitCycles; i++)
            {
                bool isCondition = false;
                Program.MainWindow.Invoke(new Action(() => isCondition = func()));                
                if (isCondition)
                    return true;
                Thread.Sleep(SLEEP_INTERVAL);
                Assert.IsTrue(i < waitCycles - 1, string.Format("Timeout {0} seconds exceeded in WaitForConditionUI", waitCycles*SLEEP_INTERVAL/1000));
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
            ClipboardEx.UseInternalClipboard(false);
            Thread.Sleep(-1);
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
                Program.Init();
                Settings.Default.SrmSettingsList[0] = SrmSettingsList.GetDefault();
                var threadTest = new Thread(WaitForSkyline) { Name = "Functional test thread" };
                threadTest.Start();
                Program.Main();
                threadTest.Join();
                Settings.Default.SrmSettingsList[0] = SrmSettingsList.GetDefault(); // Release memory held in settings
            }
            catch (Exception x)
            {
                _testExceptions.Add(x);
            }

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

                if (TestFilesZipPaths == null)
                {
                    RunTest();
                }
                else
                {
                    TestFilesDirs = new TestFilesDir[TestFilesZipPaths.Length];
                    for (int i = 0; i < TestFilesZipPaths.Length; i++)
                    {
                        TestFilesDirs[i] = new TestFilesDir(TestContext, TestFilesZipPaths[i], TestDirectoryName);
                    }
                    RunTest();
                }
            }
            catch (Exception x)
            {
                // An exception occurred outside RunTest
                _testExceptions.Add(x);
            }

            EndTest();

            if (TestFilesDirs != null)
            {
                foreach(TestFilesDir dir in TestFilesDirs)
                {
                    try
                    {
                        dir.Dispose();
                    }
                    catch (Exception x)
                    {
                        _testExceptions.Add(x);
                    }
                }
            }
        }

        private void RunTest()
        {
            // Clean-up before running the test
            RunUI(() => SkylineWindow.UseKeysOverride = true);
            
            // Use internal clipboard for testing so that we don't collide with other processes
            // using the clipboard during a test run.
            ClipboardEx.UseInternalClipboard();

            var doClipboardCheck = TestContext.Properties.Contains("ClipboardCheck");
            string clipboardCheckText = doClipboardCheck ? (string)TestContext.Properties["ClipboardCheck"] : "";
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

                if (_testExceptions.Count == 0)
                {
                    // Long wait for library build notifications
                    WaitForConditionUI(
                        () => !Application.OpenForms.Cast<Form>().Any(form => form is BuildLibraryNotification));
                    // Short wait for anything else
                    WaitForConditionUI(5000, () => Application.OpenForms.Count == 1);
                }
            }
            catch (Exception x)
            {
                // An exception occurred outside RunTest
                _testExceptions.Add(x);
            }

            foreach (var messageDlg in Application.OpenForms.OfType<MessageDlg>())
            {
                Console.WriteLine("\n\nOpen MessageDlg: {0}\n", messageDlg.Message);
            }

            // Actually throwing an exception can cause an infinite loop in MSTest
            _testExceptions.AddRange(from form in Application.OpenForms.Cast<Form>()
                                        where !(form is SkylineWindow)
                                        select new AssertFailedException(
                                            string.Format("Form of type {0} left open at end of test", form.GetType())));

            _testCompleted = true;

            // Clear the clipboard to avoid the appearance of a memory leak.
            ClipboardEx.Clear();

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

        // Restore minimal view layout in order to close extra windows.
        private void RestoreMinimalView()
        {
            var assembly = Assembly.GetAssembly(typeof(AbstractFunctionalTest));
            var layoutStream = assembly.GetManifestResourceStream(
                typeof(AbstractFunctionalTest).Namespace + ".minimal.sky.view");
            Assert.IsNotNull(layoutStream);
            RunUI(() => SkylineWindow.LoadLayout(layoutStream));
            WaitForConditionUI(() => true);
        }

        protected abstract void DoTest();

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

        public static void AddStaticMod(StaticMod mod, PeptideSettingsUI peptideSettingsUI)
        {
            var editStaticModsDlg = ShowEditStaticModsDlg(peptideSettingsUI);
            AddMod(mod, editStaticModsDlg);
        }

        public static void AddHeavyMod(StaticMod mod, PeptideSettingsUI peptideSettingsUI)
        {
            var editStaticModsDlg = ShowEditHeavyModsDlg(peptideSettingsUI);
            AddMod(mod, editStaticModsDlg);
        }

        private static void AddMod(StaticMod mod, EditListDlg<SettingsListBase<StaticMod>, StaticMod> editModsDlg)
        {
            var addStaticModDlg = ShowAddModDlg(editModsDlg);
            RunUI(() =>
            {
                addStaticModDlg.Modification = mod;
                addStaticModDlg.OkDialog();
            });
            WaitForClosedForm(addStaticModDlg);

            RunUI(editModsDlg.OkDialog);
            WaitForClosedForm(editModsDlg);
        }

        public static void AddStaticMod(string uniModName, bool isVariable, PeptideSettingsUI peptideSettingsUI)
        {
            var editStaticModsDlg = ShowEditStaticModsDlg(peptideSettingsUI);
            AddMod(uniModName, isVariable, editStaticModsDlg);
        }

        public static void AddHeavyMod(string uniModName, PeptideSettingsUI peptideSettingsUI)
        {
            var editStaticModsDlg = ShowEditHeavyModsDlg(peptideSettingsUI);
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

        public void ImportResultsFile(string fileName)
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
            WaitForCondition(420 * 1000,
                () => SkylineWindow.Document.Settings.HasResults && SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);            
        }

        #endregion
    }
}
