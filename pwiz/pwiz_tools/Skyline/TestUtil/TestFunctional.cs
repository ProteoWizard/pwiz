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
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Controls;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;

namespace pwiz.SkylineTestUtil
{
    public abstract class AbstractFunctionalTest : AbstractUnitTest
    {
        private const int SLEEP_INTERVAL = 100;
        private const int WAIT_TIME = 60*1000;    // 1 minute

        static AbstractFunctionalTest()
        {
            IsEnableLiveReports = true;
            IsCheckLiveReportsCompatibility = false;
        }


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

        protected static void RunDlg<TDlg>(Action show, Action<TDlg> act) where TDlg : Form
        {
            RunDlg(show, false, act);
        }

        protected static void RunDlg<TDlg>(Action show, bool waitForDocument, Action<TDlg> act) where TDlg : Form
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
                ClipboardEx.Clear();
                ClipboardEx.SetText(text);
            }
            catch (ExternalException)
            {
                Assert.Fail(ClipboardHelper.GetOpenClipboardMessage("Failed to set text to the clipboard.")); // Not L10N
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
                {
                    string formType = typeof(TDlg).Name;
                    if (Program.PauseForms != null && Program.PauseForms.Contains(formType))
                    {
// ReSharper disable LocalizableElement
                        RunUI(() => tForm.Text += "  (" + formType + ")");
// ReSharper restore LocalizableElement
                        Program.PauseForms.Remove(formType);
                        if (Program.PauseSeconds > 0)
                            Thread.Sleep(Program.PauseSeconds * 1000);
                        else if (Program.PauseSeconds < 0)
                            PauseAndContinueForm.Show(string.Format("Pausing for {0}", formType));
                    }

                    if (Program.ShownForms != null && !Program.ShownForms.Contains(formType))
                        Program.ShownForms.Add(formType);

                    return tForm;
                }

                Thread.Sleep(SLEEP_INTERVAL);
                Assert.IsTrue(i < waitCycles - 1, String.Format("Timeout {0} seconds exceeded in WaitForOpenForm", waitCycles * SLEEP_INTERVAL / 1000)); // Not L10N
            }
            return null;
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
            Assert.Fail("Timeout {0} seconds exceeded in WaitForClosedForm", waitCycles * SLEEP_INTERVAL / 1000); // Not L10N
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
            WaitForConditionUI(millis, () => SkylineWindow.DocumentUI.IsLoaded);
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
                Assert.IsTrue(i < waitCycles - 1, String.Format("Timeout {0} seconds exceeded in WaitForCondition", waitCycles * SLEEP_INTERVAL / 1000)); // Not L10N
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
                Assert.IsTrue(i < waitCycles - 1, String.Format("Timeout {0} seconds exceeded in WaitForConditionUI", waitCycles * SLEEP_INTERVAL / 1000)); // Not L10N
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

        /// <summary>
        /// If true, calls to PauseForScreenShot used in the tutorial tests will pause
        /// the tests and wait until the pause form is dismissed, allowing a screenshot
        /// to be taken.
        /// </summary>
        public static bool IsPauseForScreenShots { get; set; }

        public static bool IsDemoMode { get { return Program.DemoMode; } }

        public static bool IsEnableLiveReports { get; set; }

        public static bool IsCheckLiveReportsCompatibility { get; set; }

        public void PauseForScreenShot(string description = null)
        {
            if (IsCheckLiveReportsCompatibility)
                CheckReportCompatibility.CheckAll(SkylineWindow.Document);
            if (IsDemoMode)
                Thread.Sleep(3*1000);
            else if (Program.PauseSeconds > 0)
                Thread.Sleep(Program.PauseSeconds*1000);
            else if (IsPauseForScreenShots || Program.PauseSeconds < 0)
                PauseAndContinueForm.Show(description);
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
                Program.FunctionalTest = true;
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
                _testExceptions.Add(x);
            }

            // Delete unzipped test files.
            if (TestFilesDirs != null)
            {
                foreach (TestFilesDir dir in TestFilesDirs)
                {
                    try
                    {
                        if (dir != null)
                            dir.Dispose();
                    }
                    catch (Exception x)
                    {
                        _testExceptions.Add(x);
                    }
                }
            }

            if (_testExceptions.Count > 0)
            {
                Log<AbstractFunctionalTest>.Exception("Functional test exception", _testExceptions[0]); // Not L10N
                Assert.Fail(_testExceptions[0].ToString());
            }

            if (!_testCompleted)
            {
                Log<AbstractFunctionalTest>.Fail("Functional test did not complete"); // Not L10N
                Assert.IsTrue(_testCompleted);
            }
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
                Settings.Default.EnableLiveReports = IsEnableLiveReports;
                RunTest();
            }
            catch (Exception x)
            {
                // Save exception for reporting from main thread.
                _testExceptions.Add(x);
            }

            EndTest();
        }

        private void RunTest()
        {
            // Clean-up before running the test
            RunUI(() => SkylineWindow.UseKeysOverride = true);
            
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
                    RunUI(() => SkylineWindow.RemoveLibraryBuildNotification());
                    WaitForConditionUI(() => !OpenForms.Any(f => f is BuildLibraryNotification));
                    // Short wait for anything else
                    WaitForConditionUI(5000, () => OpenForms.Count() == 1);
                }
            }
            catch (Exception x)
            {
                // An exception occurred outside RunTest
                _testExceptions.Add(x);
            }

            foreach (var messageDlg in OpenForms.OfType<MessageDlg>())
            {
// ReSharper disable LocalizableElement
                Console.WriteLine("\n\nOpen MessageDlg: {0}\n", messageDlg.Message); // Not L10N
// ReSharper restore LocalizableElement
            }

            // Actually throwing an exception can cause an infinite loop in MSTest
            _testExceptions.AddRange(from form in OpenForms
                                        where !(form is SkylineWindow)
                                        select new AssertFailedException(
                                            String.Format("Form of type {0} left open at end of test", form.GetType()))); // Not L10N

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

        // Restore minimal view layout in order to close extra windows.
        private void RestoreMinimalView()
        {
            var assembly = Assembly.GetAssembly(typeof(AbstractFunctionalTest));
            var layoutStream = assembly.GetManifestResourceStream(
                typeof(AbstractFunctionalTest).Namespace + ".minimal.sky.view"); // Not L10N
            Assert.IsNotNull(layoutStream);
            RunUI(() => SkylineWindow.LoadLayout(layoutStream));
            WaitForConditionUI(() => true);
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
                                                                       findPeptideDlg.SearchString = searchText;
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
                while(!SkylineWindow.SequenceTree.SelectedDocNodes.Contains(nodePeptide))
                    findPeptideDlg.FindNext();
                findPeptideDlg.Close();
            });

            RunUI(SkylineWindow.EditDelete);

            Assert.IsTrue(WaitForCondition(() => !SkylineWindow.Document.Peptides.Any(nodePep =>
                Equals(peptideSequence, nodePep.Peptide.Sequence) &&
                isDecoy == nodePep.IsDecoy)));
            AssertEx.IsDocumentState(SkylineWindow.Document, null,
                                     docStart.PeptideGroupCount,
                                     docStart.PeptideCount - 1,
                                     docStart.TransitionGroupCount - nodePeptide.TransitionGroupCount,
                                     docStart.TransitionCount - nodePeptide.TransitionCount);
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

        public void AddStaticMod(StaticMod mod, PeptideSettingsUI peptideSettingsUI, bool pauseForScreenShot = false)
        {
            var editStaticModsDlg = ShowEditStaticModsDlg(peptideSettingsUI);
            RunUI(editStaticModsDlg.SelectLastItem);
            AddMod(mod, editStaticModsDlg, pauseForScreenShot);
        }

        public void AddHeavyMod(StaticMod mod, PeptideSettingsUI peptideSettingsUI, bool pauseForScreenShot = false)
        {
            var editStaticModsDlg = ShowEditHeavyModsDlg(peptideSettingsUI);
            RunUI(editStaticModsDlg.SelectLastItem);
            AddMod(mod, editStaticModsDlg, pauseForScreenShot);
        }

        private void AddMod(StaticMod mod,
                            EditListDlg<SettingsListBase<StaticMod>, StaticMod> editModsDlg,
                            bool pauseForScreenShot)
        {
            var addStaticModDlg = ShowAddModDlg(editModsDlg);
            RunUI(() => addStaticModDlg.Modification = mod);
            
            if (pauseForScreenShot)
                PauseForScreenShot();

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

        public void ImportResultsFile(string fileName, int waitForLoadSeconds = 420)
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
            WaitForCondition(waitForLoadSeconds * 1000,
                () => SkylineWindow.Document.Settings.HasResults && SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);            
        }

        public void ImportResultsFiles(IEnumerable<string> fileNames, int waitForLoadSeconds = 420)
        {
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            RunUI(() => importResultsDlg.NamedPathSets = importResultsDlg.GetDataSourcePathsFileReplicates(fileNames));

            ImportResultsNameDlg importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importResultsDlg.OkDialog);
            RunUI(importResultsNameDlg.YesDialog);
            WaitForCondition(waitForLoadSeconds * 1000,
                () => SkylineWindow.Document.Settings.HasResults && SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);
        }
        #endregion
    }
}
