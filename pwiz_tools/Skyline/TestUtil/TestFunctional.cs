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
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Excel;
using JetBrains.Annotations;
// using Microsoft.Diagnostics.Runtime; only needed for stack dump logic, which is currently disabled
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Controls;
using pwiz.Common.Database;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.Fasta;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Model.Lib.BlibData;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
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

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MinidumpLeakThresholdAttribute : Attribute
    {
        public MinidumpLeakThresholdAttribute(int thresholdMB)
        {
            ThresholdMB = thresholdMB;
        }

        public int ThresholdMB { get; private set; }
    }

    /// <summary>
    /// All Skyline functional tests MUST derive from this base class.
    /// Perf tests (long running, huge-data-downloading) should be declared
    /// in the TestPerf namespace, where they receive special handling so as
    /// to not disturb the normal, frequent use of the main body of tests.
    /// </summary>
    public abstract class AbstractFunctionalTest : AbstractUnitTestEx
    {
        private const int SLEEP_INTERVAL = 100;
        public const int WAIT_TIME = 3 * 60 * 1000;    // 3 minutes (was 1 minute, but in code coverage testing that may be too impatient)

        static AbstractFunctionalTest()
        {
            IsCheckLiveReportsCompatibility = false;
        }

        private bool _testCompleted;
        private ScreenshotManager _shotManager;

        protected ScreenshotManager ScreenshotManager
        {
            get { return _shotManager; }
        }

        public static SkylineWindow SkylineWindow { get { return Program.MainWindow; } }

        private bool _forcMzml;

        protected bool ForceMzml
        {
            get { return _forcMzml; }
            set { _forcMzml = value && !IsPauseForScreenShots; }    // Don't force mzML during screenshots
        }

        protected static bool LaunchDebuggerOnWaitForConditionTimeout { get; set; } // Use with caution - this will prevent scheduled tests from completing, so we can investigate a problem

        protected virtual bool UseRawFiles
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

        protected string ExtWatersRaw
        {
            get { return UseRawFiles ? ExtensionTestContext.ExtWatersRaw : ExtensionTestContext.ExtMzml; }
        }

        protected void RunWithOldReports(Action test)
        {
            TestContext.Properties["LiveReports"] = false.ToString();
            test();
        }

        /// <summary>
        /// For use when <see cref="ShowStartPage"/> is true to initiate audit logging when
        /// Skyline is first shown.
        /// </summary>
        protected void ShowSkyline(Action act)
        {
            ShowDialog<SkylineWindow>(act);
            SkylineWindow.DocumentChangedEvent += OnDocumentChangedLogging;
        }

        protected static TDlg ShowDialog<TDlg>(Action act, int millis = -1) where TDlg : Form
        {
            var existingDialog = FindOpenForm<TDlg>();
            if (existingDialog != null)
            {
                var messageDlg = existingDialog as MessageDlg;
                if (messageDlg == null)
                    AssertEx.IsNull(existingDialog, typeof(TDlg) + " is already open");
                else
                    Assert.Fail(typeof(TDlg) + " is already open with the message: " + messageDlg.Message);
            }

            SkylineBeginInvoke(act);
            TDlg dlg;
            if (millis == -1)
                dlg = WaitForOpenForm<TDlg>();
            else
                dlg = WaitForOpenForm<TDlg>(millis);
            Assert.IsNotNull(dlg);

            // Making sure if the form has a visible icon it's Skyline release icon, not daily one.
            if (IsPauseForScreenShots && dlg.ShowIcon)
            {
                if (ReferenceEquals(dlg, SkylineWindow) || dlg.Icon.Handle != SkylineWindow.Icon.Handle)
                    RunUI(() => dlg.Icon = Resources.Skyline_Release1);
            }
            return dlg;
        }

        protected static void RunUI([InstantHandle] Action act)
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

        protected static void RunDlg<TDlg>(Action show, [InstantHandle] Action<TDlg> act = null, bool pause = false) where TDlg : Form
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

        protected void ChangePeakBounds(string chromName,
            double startDisplayTime,
            double endDisplayTime)
        {
            Assert.IsTrue(startDisplayTime < endDisplayTime,
                string.Format("Start time {0} must be less than end time {1}.", startDisplayTime, endDisplayTime));

            ActivateReplicate(chromName);

            WaitForGraphs();

            RunUIWithDocumentWait(() => // adjust integration
            {
                var graphChrom = SkylineWindow.GetGraphChrom(chromName);

                var nodeGroupTree = SkylineWindow.SequenceTree.GetNodeOfType<TransitionGroupTreeNode>();
                IdentityPath pathGroup;
                if (nodeGroupTree != null)
                    pathGroup = nodeGroupTree.Path;
                else
                {
                    var nodePepTree = SkylineWindow.SequenceTree.GetNodeOfType<PeptideTreeNode>();
                    pathGroup = new IdentityPath(nodePepTree.Path, nodePepTree.ChildDocNodes[0].Id);
                }
                var listChanges = new List<ChangedPeakBoundsEventArgs>
                {
                    new ChangedPeakBoundsEventArgs(pathGroup,
                        null,
                        graphChrom.NameSet,
                        graphChrom.ChromGroupInfos[0].FilePath,
                        graphChrom.GraphItems.First().GetValidPeakBoundaryTime(startDisplayTime),
                        graphChrom.GraphItems.First().GetValidPeakBoundaryTime(endDisplayTime),
                        PeakIdentification.ALIGNED,
                        PeakBoundsChangeType.both)
                };
                graphChrom.SimulateChangedPeakBounds(listChanges);
            });
            WaitForGraphs();
        }

        private void RunUIWithDocumentWait(Action act)
        {
            var doc = SkylineWindow.Document;
            RunUI(act);
            WaitForDocumentChange(doc); // make sure the action changes the document
        }

        protected void SetDocumentGridSampleTypesAndConcentrations(IDictionary<string, Tuple<SampleType, double?>> sampleTypes)
        {
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(() => documentGrid.DataboundGridControl.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates));
            WaitForCondition(() => documentGrid.IsComplete);
            RunUI(() =>
            {
                var colReplicate = documentGrid.FindColumn(PropertyPath.Root);
                var colSampleType = documentGrid.FindColumn(PropertyPath.Root.Property("SampleType"));
                var colConcentration = documentGrid.FindColumn(PropertyPath.Root.Property("AnalyteConcentration"));
                for (int iRow = 0; iRow < documentGrid.RowCount; iRow++)
                {
                    var row = documentGrid.DataGridView.Rows[iRow];
                    var replicateName = row.Cells[colReplicate.Index].Value.ToString();
                    Tuple<SampleType, double?> tuple;
                    if (sampleTypes.TryGetValue(replicateName, out tuple))
                    {
                        row.Cells[colSampleType.Index].Value = tuple.Item1;
                        row.Cells[colConcentration.Index].Value = tuple.Item2;
                    }
                }
            });
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
                Assert.Fail(ClipboardHelper.GetPasteErrorMessage());
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
            bool[] legacyFileValues = new[] {false};
            if (filePath.EndsWith(".xls"))
            {
                legacyFileValues = new[] {true, false};
            }

            foreach (bool legacyFile in legacyFileValues)
            {
                using (var stream = File.OpenRead(filePath))
                {
                    IExcelDataReader excelDataReader;
                    if (legacyFile)
                    {
                        excelDataReader = ExcelReaderFactory.CreateBinaryReader(stream);
                    }
                    else
                    {
                        excelDataReader = ExcelReaderFactory.CreateOpenXmlReader(stream);
                    }
                    if (excelDataReader == null)
                    {
                        continue;
                    }
                    return GetExcelReaderText(excelDataReader, page, columns, hasHeader);
                }
            }
            throw new InvalidDataException("Unable to read Excel file " + filePath);
        }

        private static string GetExcelReaderText(IExcelDataReader excelDataReader, string page, int columns, bool hasHeader)
        {
            var dataSet = excelDataReader.AsDataSet();
            foreach (DataTable dataTable in dataSet.Tables)
            {
                if (dataTable.TableName != page)
                {
                    continue;
                }
                var sb = new StringBuilder();
                for (int iRow = hasHeader ? 1 : 0; iRow < dataTable.Rows.Count; iRow++)
                {
                    DataRow row = dataTable.Rows[iRow];
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
            throw new ArgumentException("Could not find page " + page);
        }

        private static IEnumerable<Form> OpenForms
        {
            get
            {
                return FormUtil.OpenForms;
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

        public static Form FindOpenForm(Type formType) 
        {
            foreach (var form in OpenForms)
            {
                if (((formType.IsInstanceOfType(form) || formType.DeclaringType != null && formType.DeclaringType.IsInstanceOfType(form))) && form.Created)
                {
                    return form;
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

            // Wait a little longer for debug build. (This may also imply code coverage testing, slower yet)
            if (ExtensionTestContext.IsDebugMode)
            {
                waitCycles = waitCycles * 4;
            }

            return waitCycles;
        }

        public static TDlg TryWaitForOpenForm<TDlg>(int millis = WAIT_TIME, Func<bool> stopCondition = null) where TDlg : Form
        {
            int waitCycles = GetWaitCycles(millis);
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

                if (stopCondition != null && stopCondition())
                    break;

                Thread.Sleep(SLEEP_INTERVAL);
            }
            return null;
        }

        public static Form TryWaitForOpenForm(Type formType, int millis = WAIT_TIME, Func<bool> stopCondition = null) 
        {
            int waitCycles = GetWaitCycles(millis);
            for (int i = 0; i < waitCycles; i++)
            {
                Assert.IsFalse(Program.TestExceptions.Any(), "Exception while running test");

                Form tForm = FindOpenForm(formType);
                if (tForm != null)
                {
                    string formTypeName = tForm.GetType().Name;
                    var multipleViewProvider = tForm as IMultipleViewProvider;
                    if (multipleViewProvider != null)
                    {
                        formTypeName += "." + multipleViewProvider.ShowingFormView.GetType().Name;
                        var formName = "(" + formType.Name + ")";
                        RunUI(() =>
                        {
                            if (tForm.Text.EndsWith(formName))
                                tForm.Text = tForm.Text.Replace(formName, "(" + formTypeName + ")");
                        });
                    }

                    if (_formLookup == null)
                        _formLookup = new FormLookup();
                    Assert.IsNotNull(_formLookup.GetTest(formTypeName),
                        formType + " must be added to TestRunnerLib\\TestRunnerFormLookup.csv");

                    if (Program.PauseForms != null && Program.PauseForms.Remove(formTypeName))
                    {
                        var formSeen = new FormSeen();
                        formSeen.Saw(formType);
                        PauseAndContinueForm.Show(string.Format("Pausing for {0}", formType));
                    }

                    return tForm;
                }

                if (stopCondition != null && stopCondition())
                    break;

                Thread.Sleep(SLEEP_INTERVAL);
            }
            return null;
        }


        public static TDlg WaitForOpenForm<TDlg>(int millis = WAIT_TIME) where TDlg : Form
        {
            var result = TryWaitForOpenForm<TDlg>(millis);
            if (result == null)
            {
                int waitCycles = GetWaitCycles(millis);
                Assert.Fail(@"Timeout {0} seconds exceeded in WaitForOpenForm({1}). Open forms: {2}", waitCycles * SLEEP_INTERVAL / 1000, typeof(TDlg).Name, GetOpenFormsString());
            }
            return result;
        }

        public void PauseForForm(Type formType)
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

            Assert.Fail(@"Timeout {0} seconds exceeded in WaitForClosedForm. Open forms: {1}", waitCycles * SLEEP_INTERVAL / 1000, GetOpenFormsString());
        }

        public static void WaitForClosedAllChromatogramsGraph()
        {
            WaitForConditionUI(() =>
            {
                var acg = FindOpenForm<AllChromatogramsGraph>();
                if (acg == null)
                    return true;
                if (acg.HasErrors)
                    Assert.Fail(TextUtil.LineSeparate("Unexpected import errors:", TextUtil.LineSeparate(acg.GetErrorMessages())));
                return false;
            });
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
           
            FormEx formEx = form as FormEx;
            if (formEx != null)
            {
                String detailedMessage = formEx.DetailedMessage;
                if (detailedMessage != null)
                {
                    result = detailedMessage;
                }
            }
            return result;
        }

        private static string GetExceptionText(Control control)
        {
            string text = control.Text;
            int assembliesIndex = text.IndexOf("************** Loaded Assemblies **************", StringComparison.Ordinal);
            if (assembliesIndex != -1)
                text = TextUtil.LineSeparate(text.Substring(0, assembliesIndex).Trim(), "------------- End ThreadExceptionDialog Stack -------------");
            return text;
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
            // Without line numbers, this isn't terribly useful.  Disable for now.
            // result += GetAllThreadsStackTraces();
            return result;
        }

        /*
         * Without line numbers, this turns out to be not all that useful, so disable for now at least.  
         * See https://github.com/Microsoft/clrmd/blob/master/src/FileAndLineNumbers/Program.cs if you want to make that work.
         * I (bspratt) stopped short of that only because it looked like it *might* introduce config issues but did not investigatge to see if that was actually a problem.
         * 
        private static string GetAllThreadsStackTraces()
        {
            // Adapted from:
            // http://stackoverflow.com/questions/2057781/is-there-a-way-to-get-the-stacktraces-for-all-threads-in-c-like-java-lang-thre
            //
            // Requires NuGet package ClrMd from Microsoft (prerelease version 0.8.31-beta as of 5/25/2016)
            //
            // N.B. this doesn't show line numbers - that apparently can be done using the techniques at
            // https://github.com/Microsoft/clrmd/blob/master/src/FileAndLineNumbers/Program.cs

            var result = "\r\nCould not get stack traces of running threads.\r\n";
            try
            {
                var pid = System.Diagnostics.Process.GetCurrentProcess().Id;

                using (var dataTarget = DataTarget.AttachToProcess(pid, 5000, AttachFlag.Passive))
                {
                    var runtime = dataTarget.ClrVersions[0].CreateRuntime();
                    if (runtime != null)
                    {
                        result = string.Empty;
                        foreach (var t in runtime.Threads)
                        {
                            result += "Thread Id " + t.ManagedThreadId + "\r\n";
                            var exception = t.CurrentException;
                            if (exception != null)
                            {
                                result += string.Format("  CurrentException: {0:X} ({1}), HRESULT={2:X}\r\n", exception.Address, exception.Type.Name, exception.HResult);
                            }
                            if (t.StackTrace.Any())
                            {
                                result += "   Stacktrace:\r\n";
                                foreach (var frame in t.StackTrace)
                                {
                                    result += String.Format("    {0,12:x} {1,12:x} {2}\r\n", frame.InstructionPointer, frame.StackPointer, frame.DisplayString);
                                }
                            }
                        }
                    }
                    result += "End of managed threads list.\r\n";
                }
            }
            catch
            {
                // ignored
            }
            return "\r\nCurrent managed thread stack traces: \r\n" + result;
        }
        */

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
            WaitForConditionUI(millis, () =>
            {
                var alertDlg = FindOpenForm<AlertDlg>();
                if (alertDlg != null)
                    Assert.Fail("Unexpected alert found: {0}", TextUtil.LineSeparate(alertDlg.Message, alertDlg.DetailMessage));
                return SkylineWindow.DocumentUI.IsLoaded;
            });
            WaitForProteinMetadataBackgroundLoaderCompletedUI(millis);  // make sure document is stable
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

        public static bool WaitForCondition(int millis, Func<bool> func, string timeoutMessage = null, bool failOnTimeout = true, bool throwOnProgramException = true)
        {
            int waitCycles = GetWaitCycles(millis);
            for (int i = 0; i < waitCycles; i++)
            {
                if (throwOnProgramException)
                    Assert.IsFalse(Program.TestExceptions.Any(), "Exception while running test");

                if (func())
                    return true;
                Thread.Sleep(SLEEP_INTERVAL);
                // Assistance in chasing down intermittent timeout problems
                if (i == waitCycles - 1 && LaunchDebuggerOnWaitForConditionTimeout)
                {
                    System.Diagnostics.Debugger.Launch(); // Try again, under the debugger
                    System.Diagnostics.Debugger.Break();
                    i = 0; // For debugging ease - stay in loop
                }
            }
            if (failOnTimeout)
            {
                var msg = (timeoutMessage == null)
                    ? string.Empty
                    : " (" + timeoutMessage + ")";
                Assert.Fail(@"Timeout {0} seconds exceeded in WaitForCondition{1}. Open forms: {2}", waitCycles * SLEEP_INTERVAL / 1000, msg, GetOpenFormsString());
            }
            return false;
        }

        public static bool WaitForConditionUI([InstantHandle] Func<bool> func)
        {
            return WaitForConditionUI(WAIT_TIME, func);
        }

        public static bool WaitForConditionUI(Func<bool> func, string timeoutMessage)
        {
            return WaitForConditionUI(func, () => timeoutMessage);
        }

        public static bool WaitForConditionUI(Func<bool> func, Func<string> timeoutMessage)
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

        public static bool WaitForConditionUI(int millis, Func<bool> func, Func<string> timeoutMessage = null, bool failOnTimeout = true, bool throwOnProgramException = true)
        {
            int waitCycles = GetWaitCycles(millis);
            for (int i = 0; i < waitCycles; i++)
            {
                if (throwOnProgramException)
                    Assert.IsFalse(Program.TestExceptions.Any(), "Exception while running test");

                bool isCondition = false;
                Program.MainWindow.Invoke(new Action(() => isCondition = func()));
                if (isCondition)
                    return true;
                Thread.Sleep(SLEEP_INTERVAL);

                // Assistance in chasing down intermittent timeout problems
                if (i==waitCycles-1 && LaunchDebuggerOnWaitForConditionTimeout)
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

                Assert.Fail(@"Timeout {0} seconds exceeded in WaitForConditionUI{1}. Open forms: {2}", waitCycles * SLEEP_INTERVAL / 1000, msg, GetOpenFormsString());
            }
            return false;
        }

        public static void WaitForPaneCondition<TPane>(GraphSummary summary, Func<TPane, bool> condition) where TPane : class
        {
            WaitForConditionUI(() =>
            {
                TPane pane;
                summary.TryGetGraphPane(out pane);
                return condition(pane);
            });
            WaitForGraphs();
        }

        public static void WaitForGraphs(bool throwOnProgramException = true)
        {
            WaitForConditionUI(WAIT_TIME, () => !SkylineWindow.IsGraphUpdatePending, null, true, false);
        }

        private static void WaitForBackgroundLoaders()
        {
            if (!WaitForCondition(WAIT_TIME, () => !SkylineWindow.BackgroundLoaders.Any(bgl => bgl.AnyProcessing()), null, false))
            {
                var activeLoaders = new List<string>();
                foreach (var loader in SkylineWindow.BackgroundLoaders)
                {
                    if (loader.AnyProcessing())
                    {
                        activeLoaders.Add(loader.GetType().FullName);
                    }
                }
                if (activeLoaders.Any())
                {
                    activeLoaders.Add(@"Open forms: " + GetOpenFormsString());
                    Assert.Fail(@"One or more background loaders did not exit properly: " + TextUtil.LineSeparate(activeLoaders));
                }
            }
        }

        // Pause a test so we can play with the UI manually.
        public static void PauseTest(string description = null)
        {
            if (!Program.SkylineOffscreen)
                PauseAndContinueForm.Show(description);
        }

        /// <summary>
        /// If true, calls to PauseForScreenShot used in the tutorial tests will pause
        /// the tests and wait until the pause form is dismissed, allowing a screenshot
        /// to be taken.
        /// </summary>
        private static bool _isPauseForScreenShots;

        public static bool IsPauseForScreenShots
        {
            get { return _isPauseForScreenShots || Program.PauseSeconds != 0; }
            set
            {
                _isPauseForScreenShots = value;
                if (_isPauseForScreenShots)
                {
                    Program.PauseSeconds = -1;
                }
            }
        }

        public int PauseStartPage { get; set; }

        public static bool IsPauseForAuditLog { get; set; }

        private bool IsTutorial
        {
            get { return TestContext.TestName.Contains("Tutorial"); }
        }

        public virtual bool AuditLogCompareLogs
        {
            get { return IsTutorial && !IsFullData; }   // Logs were recorded with partial data and not in Pass0
        }

        public virtual bool AuditLogConvertPathsToFileNames
        {
            get { return IsTutorial; }
        }

        public bool IsRecordAuditLogForTutorials
        {
            get { return IsTutorial && RecordAuditLogs; }
        }

        public static bool IsShowMatchingTutorialPages { get; set; }

        public static bool IsDemoMode { get { return Program.DemoMode; } }

        public static bool IsPass0 { get { return Program.IsPassZero; } }

        public bool IsFullData { get { return IsPauseForScreenShots || IsDemoMode || IsPass0; } }

        public static bool IsCheckLiveReportsCompatibility { get; set; }

        public string LinkPdf { get; set; }

        private string LinkPage(int? pageNum)
        {
            return pageNum.HasValue ? LinkPdf + "#page=" + pageNum : null;
        }

        private static FormLookup _formLookup;

        public void PauseForScreenShot(string description = null, int? pageNum = null, int? timeout = null)
        {
            PauseForScreenShot(description, pageNum, null, null, timeout);
        }
        public void PauseForScreenShot(Form screenshotForm, string description = null, int? pageNum = null, int? timeout = null)
        {
            PauseForScreenShot(description, pageNum, null, screenshotForm, timeout);
        }

        public void PauseForScreenShot<TView>(string description, int? pageNum = null, int? timeout = null)
            where TView : IFormView
        {
            PauseForScreenShot(description, pageNum, typeof(TView), null, timeout);
        }

        private void PauseForScreenShot(string description, int? pageNum, Type formType, Form screenshotForm = null, int? timeout = null)
        {
            if (Program.SkylineOffscreen)
                return;

            if (IsCheckLiveReportsCompatibility)
                CheckReportCompatibility.CheckAll(SkylineWindow.Document);
            if (IsDemoMode)
                Thread.Sleep(3 * 1000);
            else if (Program.PauseSeconds > 0)
                Thread.Sleep(Program.PauseSeconds * 1000);
            else if (IsPauseForScreenShots && PauseStartPage <= (pageNum ?? int.MaxValue))
            {
                if (screenshotForm == null)
                {
                    if (formType != null)
                    {
                        screenshotForm = TryWaitForOpenForm(formType) ?? SkylineWindow;
                    }
                    else
                        screenshotForm = SkylineWindow;
                    RunUI(() => screenshotForm?.Update());
                }

//                Thread.Sleep(300);
//                _shotManager.TakeNextShot(screenshotForm);

                var formSeen = new FormSeen();
                formSeen.Saw(formType);
                bool showMatchingPages = IsShowMatchingTutorialPages || Program.ShowMatchingPages;

                PauseAndContinueForm.Show(description, LinkPage(pageNum), showMatchingPages, timeout);
            }
            else
            {
                PauseForForm(formType);
            }
        }

        public void PauseForAuditLog()
        {
            if (IsPauseForAuditLog)
            {
                RunUI(() => SkylineWindow.ShowAuditLog());
                PauseTest();
            } 
        }

        public void ExpandMenu([NotNull] ToolStrip menu, [NotNull] string path)
        {
            LinkedList<string> parsedPath = new LinkedList<string>(path.Split('>'));
            Application.DoEvents();
            ExpandMenuRecursive(menu, parsedPath.First);
        }

        private void ExpandMenuRecursive(ToolStrip menu, [NotNull] LinkedListNode<string> path)
        {
            var nextItem = menu.Items.OfType<ToolStripMenuItem>().FirstOrDefault((i) => { return (i.Text.Replace(@"&", "") == path.Value); });
            if (nextItem != null)
            {
                nextItem.Select();
                if (nextItem.HasDropDown && nextItem.HasDropDownItems)
                {
                    if (path.Next != null)
                    {
                        nextItem.ShowDropDown();
                        ExpandMenuRecursive(nextItem.DropDown, path.Next);
                    }
                }
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
        protected void RunFunctionalTest(string defaultUiMode = UiModes.PROTEOMIC)
        {
            if (IsPerfTest && !RunPerfTests)
            {
                return; // Don't want to run this lengthy test right now
            }

            bool firstTry = true;
            // Be prepared to re-run test in the event that a previously downloaded data file is damaged or stale
            for (;;)
            {
                try
                {
                    RunFunctionalTestOrThrow(defaultUiMode);
                }
                catch (Exception x)
                {
                    Program.AddTestException(x);
                }

                Settings.Default.SrmSettingsList[0] = SrmSettingsList.GetDefault(); // Release memory held in settings

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
                            Program.AddTestException(x);
                            FileStreamManager.Default.CloseAllStreams();
                        }
                    }
                }

                if (firstTry && Program.TestExceptions.Count > 0 && RetryDataDownloads)
                {
                    try
                    {
                        if (FreshenTestDataDownloads())
                        {
                            firstTry = false;
                            Program.TestExceptions.Clear();
                            continue;
                        }
                    }
                    catch (Exception xx)
                    {
                        Program.AddTestException(xx); // Some trouble with data download, make a note of it
                    }
                }


                if (Program.TestExceptions.Count > 0)
                {
                    //Log<AbstractFunctionalTest>.Exception(@"Functional test exception", Program.TestExceptions[0]);
                    const string errorSeparator = "------------------------------------------------------";
                    Assert.Fail("{0}{1}{2}{3}",
                        Environment.NewLine + Environment.NewLine,
                        errorSeparator + Environment.NewLine,
                        Program.TestExceptions[0],
                        Environment.NewLine + errorSeparator + Environment.NewLine);
                }
                break;
            }

            if (!_testCompleted)
            {
                //Log<AbstractFunctionalTest>.Fail(@"Functional test did not complete");
                Assert.Fail("Functional test did not complete");
            }
        }

        protected void RunFunctionalTestOrThrow(string defaultUiMode)
        {
            Program.FunctionalTest = true;
            Program.DefaultUiMode = defaultUiMode;
            Program.TestExceptions = new List<Exception>();
            LocalizationHelper.InitThread();

            // Unzip test files.
            if (TestFilesZipPaths != null)
            {
                TestFilesDirs = new TestFilesDir[TestFilesZipPaths.Length];
                for (int i = 0; i < TestFilesZipPaths.Length; i++)
                {
                    TestFilesDirs[i] = new TestFilesDir(TestContext, TestFilesZipPaths[i], TestDirectoryName,
                        TestFilesPersistent, IsExtractHere(i));
                }
            }

            _shotManager = new ScreenshotManager(TestContext, SkylineWindow);

            // Run test in new thread (Skyline on main thread).
            Program.Init();
            InitializeSkylineSettings();
            if (Program.PauseSeconds != 0)
            {
                ForceMzml = false;
            }

            var threadTest = new Thread(WaitForSkyline) { Name = @"Functional test thread" };
            LocalizationHelper.InitThread(threadTest);
            threadTest.Start();
            Program.Main();
            threadTest.Join();

            // Were all windows disposed?
            FormEx.CheckAllFormsDisposed();
            CommonFormEx.CheckAllFormsDisposed();
        }

        /// <summary>
        /// Reset the settings for the Skyline application before starting a test.
        /// Tests can override this method if they have have any settings that need to
        /// be set before the test's DoTest method gets called (i.e. before the SkylineWindow is created).
        /// </summary>
        protected virtual void InitializeSkylineSettings()
        {
            Settings.Default.Reset();
            Settings.Default.ImportResultsAutoCloseWindow = true;
            Settings.Default.ImportResultsSimultaneousFiles = (int)MultiFileLoader.ImportResultsSimultaneousFileOptions.many;    // use maximum threads for multiple file import
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
            Settings.Default.TutorialMode = true;
        }

        private void BeginAuditLogging()
        {
            CleanupAuditLogs(); // Clean-up before to avoid appending to an existing audit log
            if (SkylineWindow != null)
                SkylineWindow.DocumentChangedEvent += OnDocumentChangedLogging;
            AuditLogEntry.ConvertPathsToFileNames = AuditLogConvertPathsToFileNames;
        }

        private void EndAuditLogging()
        {
            AuditLogEntry.ConvertPathsToFileNames = false;
            if (SkylineWindow == null)
                return;
            SkylineWindow.DocumentChangedEvent -= OnDocumentChangedLogging;
            VerifyAuditLogCorrect();
            CleanupAuditLogs(); // Clean-up after to avoid appending to an existing autid log - if passed, then it matches expected
        }

        private string AuditLogDir
        {
            get { return Path.Combine(TestContext.TestRunResultsDirectory ?? TestContext.TestDir, "AuditLog"); }
        }

        private string AuditLogTutorialDir
        {
            get { return TestContext.GetProjectDirectory(@"TestTutorial\TutorialAuditLogs"); }
        }

        private readonly HashSet<AuditLogEntry> _setSeenEntries = new HashSet<AuditLogEntry>();
        private readonly Dictionary<int, AuditLogEntry> _lastLoggedEntries = new Dictionary<int, AuditLogEntry>();

        private void OnDocumentChangedLogging(object sender, DocumentChangedEventArgs e)
        {
            var log = SkylineWindow.Document.AuditLog;
            if (e.IsOpeningFile)
            {
                lock (_setSeenEntries)
                {
                    _setSeenEntries.Clear();
                    for (var entry = log.AuditLogEntries; !entry.IsRoot; entry = entry.Parent)
                        _setSeenEntries.Add(entry);
                }
                // Avoid logging newly deserialized entries
                return;
            }
            LogNewEntries(log.AuditLogEntries);
        }

        private void LogNewEntries(AuditLogEntry entry)
        {
            if (entry.IsRoot)
                return;

            AuditLogEntry lastLoggedEntry;
            lock (_setSeenEntries)
            {
                if (_setSeenEntries.Contains(entry))
                    return;
                lastLoggedEntry = GetLastLogged(entry);
                _setSeenEntries.Add(entry);
            }

            LogNewEntries(entry.Parent);
            if (lastLoggedEntry == null)
                WriteEntryToFile(AuditLogDir, entry);
            else
                WriteDiffEntryToFile(AuditLogDir, entry, lastLoggedEntry);
        }

        private AuditLogEntry GetLastLogged(AuditLogEntry entry)
        {
            if (_lastLoggedEntries.TryGetValue(entry.LogIndex, out var lastLoggedEntry))
            {
                _lastLoggedEntries[entry.LogIndex] = entry;
                return lastLoggedEntry;
            }
            _lastLoggedEntries.Add(entry.LogIndex, entry);
            return null;
        }

        private void VerifyAuditLogCorrect()
        {
            var recordedFile = GetLogFilePath(AuditLogDir);
            if (!AuditLogCompareLogs)
                return;

            // Ensure expected tutorial log file exists unless recording
            var projectFile = GetLogFilePath(AuditLogTutorialDir);
            bool existsInProject = File.Exists(projectFile);
            if (!IsRecordAuditLogForTutorials)
            {
                Assert.IsTrue(existsInProject,
                    "Log file for test \"{0}\" does not exist, set IsRecordAuditLogForTutorials=true to create it",
                    TestContext.TestName);
            }

            // Compare file contents
            var expected = existsInProject ? ReadTextWithNormalizedLineEndings(projectFile) : string.Empty;
            var actual = ReadTextWithNormalizedLineEndings(recordedFile);
            if (Equals(expected, actual))
                return;

            // They are not equal. So, report an intelligible error and potentially copy
            // a new expected file to the project if in record mode.
            if (!IsRecordAuditLogForTutorials)
                AssertEx.NoDiff(expected, actual);
            else
            {
                // Copy the just recorded file to the project for comparison or commit
                File.Copy(recordedFile, projectFile, true);
                if (!existsInProject)
                    Console.WriteLine(@"Successfully recorded tutorial audit log");
                else
                    Console.WriteLine(@"Successfully recorded changed tutorial audit log");
            }
        }

        private string ReadTextWithNormalizedLineEndings(string filePath)
        {
            // Mimic what AssertEx.NoDiff() does, which turns out to produce results
            // somewhat different from File.ReadAllLines()
            using (var reader = new StreamReader(filePath, Encoding.UTF8))
            {
                var sb = new StringBuilder();
                string line;
                while ((line = reader.ReadLine()) != null)
                    sb.AppendLine(line);
                return sb.ToString();
            }
        }

        private void CleanupAuditLogs()
        {
            var recordedFile = GetLogFilePath(AuditLogDir);
            if (File.Exists(recordedFile))
                Helpers.TryTwice(() => File.Delete(recordedFile));    // Avoid appending to the same file on multiple runs
        }

        private string GetLogFilePath(string folderPath)
        {
            var language = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            var path = Path.Combine(folderPath, language);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return Path.Combine(path, TestContext.TestName + ".log");
        }

        private void WriteDiffEntryToFile(string folderPath, AuditLogEntry entry, AuditLogEntry lastLoggedEntry)
        {
            var filePath = GetLogFilePath(folderPath);
            using (var fs = File.Open(filePath, FileMode.Append))
            {
                using (var sw = new StreamWriter(fs))
                {
                    sw.Write(AuditLogEntryDiffToString(entry, lastLoggedEntry));
                }
            }
        }

        private string AuditLogEntryDiffToString(AuditLogEntry entry, AuditLogEntry lastLoggedEntry)
        {
            Assert.AreEqual(lastLoggedEntry.LogIndex, entry.LogIndex);
            Assert.AreEqual(lastLoggedEntry.UndoRedo.ToString(), entry.UndoRedo.ToString());
            Assert.AreEqual(lastLoggedEntry.Summary.ToString(), entry.Summary.ToString());
            Assert.AreEqual(lastLoggedEntry.AllInfo.Count, entry.AllInfo.Count);
            var result = string.Empty;
            if (!Equals(entry.Reason, lastLoggedEntry.Reason))
                result += string.Format("Reason: '{0}' to '{1}'\r\n", lastLoggedEntry.Reason, entry.Reason);
            for (int i = 0; i < entry.AllInfo.Count; i++)
            {
                var lastReason = lastLoggedEntry.AllInfo[i].Reason;
                var newReason = entry.AllInfo[i].Reason;
                if (!Equals(lastReason, newReason))
                    result += string.Format("Detail Reason {0}: '{1}' to '{2}'\r\n", i, lastReason, newReason);
            }

            if (!string.IsNullOrEmpty(result))
                result = result.Insert(0, string.Format("Reason Changed: {0} \r\n", entry.UndoRedo)) + "\r\n";

            return result;
        }

        private void WriteEntryToFile(string folderPath, AuditLogEntry entry)
        {
            var filePath = GetLogFilePath(folderPath);
            using (var fs = File.Open(filePath, FileMode.Append))
            {
                using (var sw = new StreamWriter(fs))
                {
                    sw.WriteLine(AuditLogEntryToString(entry));
                }
            }
        }

        private string AuditLogEntryToString(AuditLogEntry entry)
        {
            var result = string.Format("Undo Redo : {0}\r\n", entry.UndoRedo);
            result += string.Format("Summary   : {0}\r\n", entry.Summary);
            result += "All Info  :\r\n";

            foreach (var allInfoItem in entry.AllInfo)
                result += allInfoItem + "\r\n";

            if (entry.ExtraInfo != null)
                result += string.Format("Extra Info: {0}\r\n", LogMessage.ParseLogString(entry.ExtraInfo, LogLevel.all_info, entry.DocumentType));

            return result;
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
                    if (ShowStartPage && null != FindOpenForm<StartPage>())
                        break;

                    Thread.Sleep(SLEEP_INTERVAL);
                }
                if (!ShowStartPage)
                {
                    Assert.IsTrue(Program.MainWindow != null && Program.MainWindow.IsHandleCreated,
                        @"Timeout {0} seconds exceeded in WaitForSkyline", waitCycles * SLEEP_INTERVAL / 1000);
                }
                BeginAuditLogging();
                RunTest();
                EndAuditLogging();
            }
            catch (Exception x)
            {
                // Save exception for reporting from main thread.
                Program.AddTestException(x);
            }

            EndTest();

            Settings.Default.Reset();
            MsDataFileImpl.PerfUtilFactory.Reset();
        }

        private void RunTest()
        {
            if (null != SkylineWindow)
            {
                // Clean-up before running the test
                RunUI(() =>
                {
                    SkylineWindow.UseKeysOverride = true;
                    SkylineWindow.AssumeNonNullModificationAuditLogging = true;
                    if (IsPauseForScreenShots)
                    {
                        // Screenshots should be taken with release icon and "Skyline" in the window title
                        SkylineWindow.Icon = Resources.Skyline_Release1;
                    }
                });
                 
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

            var doClipboardCheck = TestContext.Properties.Contains(@"ClipboardCheck");
            string clipboardCheckText = doClipboardCheck ? (string)TestContext.Properties[@"ClipboardCheck"] : String.Empty;
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
            {
                if (Program.StartWindow != null)
                {
                    CloseOpenForms(typeof(StartPage));
                    _testCompleted = true;
                    RunUI(Program.StartWindow.Close);
                }

                return;
            }

            try
            {
                // Release all resources by setting the document to something that
                // holds no file handles.
                var docNew = new SrmDocument(SrmSettingsList.GetDefault());
                // Try twice, because this operation can fail due to active background processing
                RunUI(() => Helpers.TryTwice(() => SkylineWindow.SwitchDocument(docNew, null)));

                WaitForCondition(1000, () => !FileStreamManager.Default.HasPooledStreams, string.Empty, false);
                if (FileStreamManager.Default.HasPooledStreams)
                {
                    // Just write to console to provide more information. This should cause a failure
                    // trying to remove the test directory, which will provide a path to the problem file
                    Console.WriteLine(TextUtil.LineSeparate("Streams left open:", string.Empty,
                        FileStreamManager.Default.ReportPooledStreams()));
                }

                WaitForGraphs(false);
                // Wait for any background loaders to notice the change and stop what they're doing
                WaitForBackgroundLoaders();
                // Restore minimal View to close dock windows.
                RestoreMinimalView();

                if (Program.TestExceptions.Count == 0)
                {
                    // Long wait for library build notifications
                    SkylineWindow.RemoveLibraryBuildNotification(); // Remove off UI thread to avoid deadlocking
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

            CloseOpenForms(typeof(SkylineWindow));

            _testCompleted = true;

            try
            {
                // Clear the clipboard to avoid the appearance of a memory leak.
                ClipboardEx.Release();
                // Occasionally this causes an InvalidOperationException during stress testing.
                RunUI(SkylineWindow.Close);
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

        private void CloseOpenForms(Type exceptType)
        {
            // Actually throwing an exception can cause an infinite loop in MSTest
            var openForms = OpenForms.Where(form => form.GetType() != exceptType).ToList();
            Program.TestExceptions.AddRange(
                from form in openForms
                select new AssertFailedException(
                    String.Format(@"Form of type {0} left open at end of test", form.GetType())));
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

            var messageDlg = formToClose as AlertDlg;
            // ReSharper disable LocalizableElement
            if (messageDlg == null)
                Console.WriteLine("\n\nClosing open form of type {0}\n", formToClose.GetType()); // Not L10N
            else
                Console.WriteLine("\n\nClosing open MessageDlg: {0}\n", TextUtil.LineSeparate(messageDlg.Message, messageDlg.DetailMessage)); // Not L10N
            // ReSharper restore LocalizableElement

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
            WaitForConditionUI(WAIT_TIME, () => true, null, true, false);
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

        protected void AdjustSequenceTreePanelWidth(bool colorLegend = false)
        {
            int newWidth = SkylineWindow.SequenceTree.WidthToEnsureAllItemsVisible();
            if (colorLegend)
                newWidth += 10;

            var seqPanel = SkylineWindow.DockPanel.Contents.OfType<SequenceTreeForm>().FirstOrDefault();
            var sequencePanel = seqPanel as DockableFormEx;
            if (sequencePanel != null)
                sequencePanel.DockPanel.DockLeftPortion = (double)newWidth / sequencePanel.Width * sequencePanel.DockPanel.DockLeftPortion;
        }

        public static void RemovePeptide(string peptideSequence, bool isDecoy = false)
        {
            RemovePeptide(new Target(peptideSequence), isDecoy);
        }

        public static void RemoveTargetByDisplayName(string targetName)
        {
            var docStart = SkylineWindow.Document;
            var nodePeptide = docStart.Molecules.FirstOrDefault(nodePep =>
                Equals(targetName, nodePep.Peptide.Target.DisplayName));

            Assert.IsNotNull(nodePeptide);
            RemovePeptide(nodePeptide.Target);
        }

        public static void RemovePeptide(Target peptideSequence, bool isDecoy = false)
        {
            var docStart = SkylineWindow.Document;
            var nodePeptide = docStart.Molecules.FirstOrDefault(nodePep =>
                Equals(peptideSequence, nodePep.Peptide.Target) &&
                isDecoy == nodePep.IsDecoy);

            Assert.IsNotNull(nodePeptide);

            RunDlg<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg, findPeptideDlg =>
            {
                findPeptideDlg.SearchString = peptideSequence.DisplayName;
                findPeptideDlg.FindNext();
                while (!SkylineWindow.SequenceTree.SelectedDocNodes.Contains(nodePeptide))
                    findPeptideDlg.FindNext();
                findPeptideDlg.Close();
            });

            RunUI(SkylineWindow.EditDelete);

            Assert.IsTrue(WaitForCondition(() => !SkylineWindow.Document.Peptides.Any(nodePep =>
                Equals(peptideSequence, nodePep.Peptide.Target) &&
                isDecoy == nodePep.IsDecoy)));
            if (nodePeptide == null)
                Assert.Fail(); // Resharper
            AssertEx.IsDocumentState(SkylineWindow.Document, null,
                                     docStart.MoleculeGroupCount,
                                     docStart.MoleculeCount - 1,
                                     docStart.MoleculeTransitionGroupCount - nodePeptide.TransitionGroupCount,
                                     docStart.MoleculeTransitionCount - nodePeptide.TransitionCount);
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
            var docBefore = SkylineWindow.Document;
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            RunDlg<OpenDataSourceDialog>(() => importResultsDlg.NamedPathSets = importResultsDlg.GetDataSourcePathsFile(null),
               openDataSourceDialog =>
               {
                   openDataSourceDialog.SelectFile(fileName);
                   openDataSourceDialog.Open();
               });
            WaitForConditionUI(() => importResultsDlg.NamedPathSets != null);
            if (lockMassParameters == null)
            {
                OkDialog(importResultsDlg, importResultsDlg.OkDialog);
            }
            else
            {
                RunDlg<ImportResultsLockMassDlg>(importResultsDlg.OkDialog, dlg =>
                {
                    dlg.LockmassPositive = lockMassParameters.LockmassPositive ?? 0;
                    dlg.LockmassNegative = lockMassParameters.LockmassNegative ?? 0;
                    dlg.LockmassTolerance = lockMassParameters.LockmassTolerance ?? 0;
                    dlg.OkDialog();
                });
            }
            if (expectedErrorMessage != null)
            {
                var dlg = WaitForOpenForm<MessageDlg>();
                Assert.IsTrue(dlg.DetailMessage.Contains(expectedErrorMessage));
                dlg.CancelDialog();
            }
            else
            {
                WaitForDocumentChangeLoaded(docBefore, waitForLoadSeconds*1000);
                WaitForClosedAllChromatogramsGraph();
            }
        }

        public void ImportResultsReplicatesCE(string replicatesDirName, int waitForLoadSeconds = 420)
        {

            RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg =>
            {
                importResultsDlg.RadioAddNewChecked = true;
                importResultsDlg.OptimizationName = ExportOptimize.CE;
                importResultsDlg.NamedPathSets = DataSourceUtil.GetDataSourcesInSubdirs(replicatesDirName).ToArray();
                string prefix = importResultsDlg.NamedPathSets.Select(ns => ns.Key).GetCommonPrefix();
                string suffix = importResultsDlg.NamedPathSets.Select(ns => ns.Key).GetCommonSuffix();
                // Rename all the replicates to remove the specified prefix and/or suffix, so those dialogs don't pop up.
                for (int i = 0; i < importResultsDlg.NamedPathSets.Length; i++)
                {
                    var namedSet = importResultsDlg.NamedPathSets[i];
                    importResultsDlg.NamedPathSets[i] = new KeyValuePair<string, MsDataFileUri[]>(
                        namedSet.Key.Substring(prefix.Length, namedSet.Key.Length - (prefix.Length+suffix.Length)), namedSet.Value);
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

            string prefix = fileNames.Select(f => f.GetFileName()).GetCommonPrefix();
            if (prefix.Length < ImportResultsDlg.MIN_COMMON_PREFIX_LENGTH)
            {
                OkDialog(importResultsDlg, importResultsDlg.OkDialog);
            }
            else
            {
                ImportResultsNameDlg importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importResultsDlg.OkDialog);
                OkDialog(importResultsNameDlg, importResultsNameDlg.OkDialog);
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
                PauseForScreenShot("Import Results");

                if (removePrefix.Value)
                    OkDialog(importResultsNameDlg, importResultsNameDlg.YesDialog);
                else
                    OkDialog(importResultsNameDlg, importResultsNameDlg.NoDialog);
            }
            WaitForDocumentChange(doc);
        }

        #endregion

        #region Spectral library test helpers

        public static IList<DbRefSpectra> GetRefSpectra(string filename)
        {
            using (var connection = new SQLiteConnection(string.Format("Data Source='{0}';Version=3", filename)))
            {
                connection.Open();
                return GetRefSpectra(connection);
            }
        }

        private static double? ParseNullable(SQLiteDataReader reader, int ordinal)
        {
            if (ordinal < 0)
                return null;
            var str = reader[ordinal].ToString();
            if (string.IsNullOrEmpty(str))
                return null;
            return double.Parse(str);
        }

        public static IList<DbRefSpectra> GetRefSpectra(SQLiteConnection connection)
        {
            var list = new List<DbRefSpectra>();
            var hasAnnotations = SqliteOperations.TableExists(connection, @"RefSpectraPeakAnnotations");
            using (var select = new SQLiteCommand(connection) { CommandText = "SELECT * FROM RefSpectra" })
            using (var reader = @select.ExecuteReader())
            {
                var iAdduct = reader.GetOrdinal("precursorAdduct");
                var iIonMobility = reader.GetOrdinal("ionMobility");
                var iCCS = reader.GetOrdinal("collisionalCrossSectionSqA");
                var noMoleculeDetails = reader.GetOrdinal("moleculeName") < 0; // Also a cue for presence of chemicalFormula, inchiKey, and otherKeys
                while (reader.Read())
                {
                    var refSpectrum = new DbRefSpectra
                    {
                        PeptideSeq = reader["peptideSeq"].ToString(),
                        PeptideModSeq = reader["peptideModSeq"].ToString(),
                        PrecursorCharge = int.Parse(reader["precursorCharge"].ToString()),
                        PrecursorAdduct = iAdduct < 0 ? string.Empty : reader[iAdduct].ToString(),
                        PrecursorMZ = double.Parse(reader["precursorMZ"].ToString()),
                        RetentionTime = double.Parse(reader["retentionTime"].ToString()),
                        IonMobility = ParseNullable(reader, iIonMobility),
                        CollisionalCrossSectionSqA = ParseNullable(reader, iCCS),
                        MoleculeName = noMoleculeDetails ? string.Empty : reader["moleculeName"].ToString(),
                        ChemicalFormula = noMoleculeDetails ? string.Empty : reader["chemicalFormula"].ToString(),
                        InChiKey = noMoleculeDetails ? string.Empty : reader["inchiKey"].ToString(),
                        OtherKeys = noMoleculeDetails ? string.Empty : reader["otherKeys"].ToString(),
                        NumPeaks = ushort.Parse(reader["numPeaks"].ToString())
                    };
                    if (hasAnnotations)
                    {
                        var id = int.Parse(reader["id"].ToString());
                        var annotations = GetRefSpectraPeakAnnotations(connection, refSpectrum, id);
                        refSpectrum.PeakAnnotations = annotations;
                    }
                    list.Add(refSpectrum);
                }

                return list;
            }
        }

        private static IList<DbRefSpectraPeakAnnotations> GetRefSpectraPeakAnnotations(SQLiteConnection connection, DbRefSpectra refSpectrum, int refSpectraId)
        {
            var list = new List<DbRefSpectraPeakAnnotations>();
            using (var select = new SQLiteCommand(connection) { CommandText = "SELECT * FROM RefSpectraPeakAnnotations WHERE RefSpectraId = " + refSpectraId })
            using (var reader = @select.ExecuteReader())
            {
                while (reader.Read())
                {
                    list.Add(new DbRefSpectraPeakAnnotations
                    {

                        RefSpectra = refSpectrum,
                        Id = int.Parse(reader["id"].ToString()),
                        PeakIndex = int.Parse(reader["peakIndex"].ToString()),
                        Charge = int.Parse(reader["charge"].ToString()),
                        Adduct = reader["adduct"].ToString(),
                        mzTheoretical = double.Parse(reader["mzTheoretical"].ToString()),
                        mzObserved = double.Parse(reader["mzObserved"].ToString()),
                        Name = reader["name"].ToString(),
                        Formula = reader["formula"].ToString(),
                        InchiKey = reader["inchiKey"].ToString(),
                        OtherKeys = reader["otherKeys"].ToString(),
                        Comment = reader["comment"].ToString(),
                    });
                }
                return list;
            }
        }

        public static void CheckRefSpectra(IList<DbRefSpectra> spectra, string peptideSeq, string peptideModSeq, int precursorCharge, double precursorMz, ushort numPeaks, double rT, IonMobilityAndCCS im = null)
        {
            for (var i = 0; i < spectra.Count; i++)
            {
                var spectrum = spectra[i];
                if (spectrum.PeptideSeq.Equals(peptideSeq) &&
                    spectrum.PeptideModSeq.Equals(peptideModSeq) &&
                    spectrum.PrecursorCharge.Equals(precursorCharge) &&
                    Math.Abs((spectrum.RetentionTime ?? 0) - rT) < 0.001 &&
                    Math.Abs(spectrum.PrecursorMZ - precursorMz) < 0.001 &&
                    spectrum.NumPeaks.Equals(numPeaks))
                {
                    spectra.RemoveAt(i);
                    return;
                }
            }
            Assert.Fail("{0} [{1}], precursor charge {2}, precursor m/z {3}, RT {4} with {5} peaks not found", peptideSeq, peptideModSeq, precursorCharge, precursorMz, rT, numPeaks);
        }

        #endregion
    }
}
