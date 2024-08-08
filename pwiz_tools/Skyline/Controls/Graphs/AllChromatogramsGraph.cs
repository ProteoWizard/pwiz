/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// A window that progressively displays chromatogram data during file import.
    /// </summary>
    public partial class AllChromatogramsGraph : FormEx
    {
        private readonly Stopwatch _stopwatch;
        private int _selected = -1;
        private bool _selectionIsSticky;
        private readonly int _multiFileWindowWidth;
        private readonly List<MsDataFileUri> _partialProgressList = new List<MsDataFileUri>();
        private DateTime _retryTime;
        private int _nextRetry;
        private ImportResultsRetryCountdownDlg _retryDlg;

        private Dictionary<MsDataFileUri, FileProgressControl> _fileProgressControls =
            new Dictionary<MsDataFileUri, FileProgressControl>();

        private const int RETRY_INTERVAL = 10;
        private const int RETRY_COUNTDOWN = 30;
        //private static readonly Log LOG = new Log<AllChromatogramsGraph>();

        public AllChromatogramsGraph()
        {
            InitializeComponent();
            toolStrip1.Renderer = new CustomToolStripProfessionalRenderer();
            _stopwatch = new Stopwatch();
            _multiFileWindowWidth = Size.Width;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (DesignMode) return;

            Icon = Resources.Skyline;

            // Restore window placement.
            if (Program.DemoMode)
            {
                var rectScreen = Screen.PrimaryScreen.WorkingArea;
                StartPosition = FormStartPosition.Manual;
                Location = new Point(rectScreen.Right - Size.Width, rectScreen.Bottom - Size.Height);
            }
            else
            {
                Point location = Settings.Default.AllChromatogramsLocation;
                if (!location.IsEmpty)
                {
                    StartPosition = FormStartPosition.Manual;

                    // Make sure the window is entirely on screen
                    Location = location;
                    ForceOnScreen();
                }
            }
            Move += WindowMove;

            btnAutoCloseWindow.Image = imageListPushPin.Images[Settings.Default.ImportResultsAutoCloseWindow ? 1 : 0];
            btnAutoScaleGraphs.Image = imageListLock.Images[Settings.Default.ImportResultsAutoScaleGraph ? 1 : 0];

            _stopwatch.Start();
            _retryTime = DateTime.MaxValue;
            elapsedTimer.Tick += ElapsedTimer_Tick;
        }

        protected override void OnClosed(EventArgs e)
        {
            graphChromatograms.Finish();
        }

        private bool _inCreateHandle;
        /// <summary>
        /// Override CreateHandle in order to try to track down intermittent test failures.
        /// TODO(nicksh): Remove this override once the intermittent failure is figured out
        /// </summary>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        protected override void CreateHandle()
        {
            Assume.IsFalse(_inCreateHandle);
            if (Program.FunctionalTest && Program.MainWindow != null && Program.MainWindow.InvokeRequired)
            {
                throw new ApplicationException(@"AllChromatogramsGraph.CreateHandle called on wrong thread");
            }

            try
            {
                _inCreateHandle = true;
                base.CreateHandle();
            }
            catch (Exception e)
            {
                Console.Out.WriteLine(@"Exception in AllChromatogramsGraph CreateHandle {0}", e);
                throw new Exception(@"Exception in AllChromatogramsGraph", e);
            }
            finally
            {
                _inCreateHandle = false;
            }
        }

        /// <summary>
        /// Clean up any resources being used.
        /// Also, check "_inCreateHandle" in the hopes of tracking down intermittent test failures.
        /// TODO(nicksh): Move this function back to AllChromatogramsGraph.Designer.cs once the
        /// test failures are figured out.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        protected override void Dispose(bool disposing)
        {
            if (_inCreateHandle)
            {
                Console.Out.WriteLine(@"AllChromatogramsGraph _inCreateHandle is {0}", _inCreateHandle);
            }

            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }


        private void ElapsedTimer_Tick(object sender, EventArgs e)
        {
            // Update timer and overall progress bar.
            // ReSharper disable LocalizableElement
            lblDuration.Text = _stopwatch.Elapsed.ToString(@"hh\:mm\:ss");
            // ReSharper restore LocalizableElement

            // Determine if we should automatically retry any failed file.
            if (_retryTime <= DateTime.Now)
            {
                _retryTime = DateTime.MaxValue;
                _retryDlg = new ImportResultsRetryCountdownDlg(RETRY_COUNTDOWN, 
                    () =>
                    {
                        for (int i = 0; i < flowFileStatus.Controls.Count; i++)
                        {
                            var control = (FileProgressControl) flowFileStatus.Controls[_nextRetry];
                            if (++_nextRetry == flowFileStatus.Controls.Count)
                                _nextRetry = 0;
                            if (control.Error != null)
                            {
                                ChromatogramManager.RemoveFile(control.FilePath);
                                Retry(control.Status);
                                break;
                            }
                        }
                        _retryDlg.Dispose();
                    },
                    () =>
                    {
                        _stopwatch.Stop();
                        elapsedTimer.Stop();
                        _retryDlg.Dispose();
                    });
                _retryDlg.ShowDialog(this);
            }
        }

        public ChromatogramManager ChromatogramManager { get; set; }

        public bool IsUserCanceled { get; private set; }

        public string Error { get { return textBoxError.Text; } }

        public FileProgressControl SelectedControl 
        { 
            get
            {
                return _selected >= 0 && _selected < flowFileStatus.Controls.Count
                    ? (FileProgressControl) flowFileStatus.Controls[_selected]
                    : null;
            }
        }

        public int Selected
        {
            get { return _selected; }
            set
            {
                if (_selected != value)
                {
                    SetSelectedControl(false);
                    _selected = value;
                    SetSelectedControl(true);
                    RefreshSelectedControl();
                }
            }
        }

        private void SetSelectedControl(bool selected)
        {
            if (SelectedControl != null)
                SelectedControl.Selected = selected;
        }

        private void RefreshSelectedControl()
        {
            if (SelectedControl == null)
                return;
            flowFileStatus.AutoScroll = true;
            flowFileStatus.ScrollControlIntoView(SelectedControl);
            SelectedControl.Invalidate();
            graphChromatograms.IsCanceled = SelectedControl.IsCanceled;
            if (SelectedControl.Error != null || SelectedControl.Warning != null)
            {
                textBoxError.Text = SelectedControl.GetErrorLog(cbMoreInfo.Checked);
                ShowControl(panelError);
            }
            else if (SelectedControl.Progress == 0)
            {
                labelFileName.Text = SelectedControl.FilePath.GetFileNameWithoutExtension();
                ShowControl(labelFileName);
            }
            else
            {
                graphChromatograms.Key = SelectedControl.FilePath.GetFilePath();
                ShowControl(graphChromatograms);
            }
        }

        public IEnumerable<FileStatus> Files
        {
            get
            {
                foreach (FileProgressControl control in flowFileStatus.Controls)
                {
                    yield return new FileStatus
                    {
                        FilePath = control.FilePath,
                        Progress = control.Progress,
                        Error = control.Error
                    };
                }
            }
        }

        public class FileStatus
        {
            public MsDataFileUri FilePath { get; set; }
            public int Progress { get; set; }
            public string Error { get; set; }
        }

        public void RetryImport(int index)
        {
            Retry(((FileProgressControl) flowFileStatus.Controls[index]).Status);
        }

        public bool IsItemComplete(int index)
        {
            return ((FileProgressControl) flowFileStatus.Controls[index]).Status.IsComplete;
        }

        public bool IsItemCanceled(int index)
        {
            return ((FileProgressControl)flowFileStatus.Controls[index]).Status.IsCanceled;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Up:
                    if (Selected > 0)
                        Selected--;
                    break;
                    
                case Keys.Down:
                    if (Selected < flowFileStatus.Controls.Count - 1)
                        Selected++;
                    break;

                default:
                    return base.ProcessCmdKey(ref msg, keyData);
            }

            return true;
        }

        /// <summary>
        /// Show final results of import before closing window.
        /// </summary>
        public void Finish()
        {
            // During retry interval, don't change anything.
            if (_retryTime < DateTime.MaxValue)
                return;

            _partialProgressList.Clear();

            // Finish all files and remove from status.
            bool hadErrors = false;
            if (Settings.Default.ImportResultsDoAutoRetry)
            {
                foreach (FileProgressControl control in flowFileStatus.Controls)
                {
                    control.Finish();
                    if (control.Error != null)
                        hadErrors = true;
                }
            }

            if (hadErrors)
            {
                _retryTime = DateTime.Now + TimeSpan.FromSeconds(RETRY_INTERVAL);
            }
            else
            {
                _stopwatch.Stop();
                elapsedTimer.Stop();
            }

            progressBarTotal.Visible = false;
            btnCancel.Visible = false;
            btnHide.Text = GraphsResources.AllChromatogramsGraph_Finish_Close;
        }

        public bool HasErrors
        {
            get
            {
                foreach (FileProgressControl control in flowFileStatus.Controls)
                {
                    if (control.Error != null || control.Warning != null)
                        return true;
                }
                return false;
            }
        }

        private void WindowMove(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Normal)
                Settings.Default.AllChromatogramsLocation = Location;
        }

        /// <summary>
        /// Display chromatogram data. 
        /// </summary>
        /// <param name="status"></param>
        public void UpdateStatus(MultiProgressStatus status)
        {
            // Update overall progress bar.
            if (_partialProgressList.Count == 0)
            {
                if (status.PercentComplete >= 0) // -1 value means "unknown" (possible if we are mid-completion). Just leave things alone in that case.
                {
                    progressBarTotal.Value = status.PercentComplete;
                }
            }
            else
            {
                int percentComplete = 0;
                foreach (var path in _partialProgressList)
                {
                    var matchingStatus = FindStatus(status, path);
                    if (matchingStatus != null)
                    {
                        percentComplete += matchingStatus.IsFinal ? 100 : matchingStatus.PercentComplete;
                    }
                }
                progressBarTotal.Value = percentComplete/_partialProgressList.Count;
            }

            // Add any new files.
            AddProgressControls(status);

            // Cancel missing files.
            CancelMissingFiles(status);

            if (Selected < 0)
                Selected = 0;

            // Update progress control for each file.
            for (int i = 0; i < status.ProgressList.Count; i++)
            {
                var loadingStatus = status.ProgressList[i];
                graphChromatograms.UpdateStatus(loadingStatus);
                var progressControl = FindProgressControl(loadingStatus.FilePath);
                bool wasError = progressControl.Error != null;
                progressControl.SetStatus(loadingStatus);
                if (loadingStatus.IsError && !wasError)
                {
                    if (!_selectionIsSticky)
                    {
                        _selectionIsSticky = true;
                        Selected = i;
                        flowFileStatus.ScrollControlIntoView(progressControl);
                    }
                    RemoveFailedFile(loadingStatus);
                }
            }

            RefreshSelectedControl();

            // Update status for a single file.
            if (flowFileStatus.Controls.Count == 1)
            {
                Width = _multiFileWindowWidth - panelFileList.Width;
                panelFileList.Visible = false;
                btnAutoScaleGraphs.Visible = false;
                return;
            }

            Width = _multiFileWindowWidth;
            panelFileList.Visible = true;
            btnAutoScaleGraphs.Visible = true;
            graphChromatograms.ScaleIsLocked = !Settings.Default.ImportResultsAutoScaleGraph;

            // If a file is successfully completed, automatically select another loading file.
            if (!_selectionIsSticky && (SelectedControl == null || SelectedControl.Progress == 100))
            {
                for (int i = Selected + 1; i < flowFileStatus.Controls.Count; i++)
                {
                    var control = (FileProgressControl) flowFileStatus.Controls[i];
                    if (!control.IsCanceled && control.Progress > 0 && control.Progress < 100)
                    {
                        Selected = i;
                        flowFileStatus.ScrollControlIntoView(control);
                    }
                }
            }

            if (!Finished)
            {
                btnCancel.Visible = true;
                btnHide.Text = GraphsResources.AllChromatogramsGraph_UpdateStatus_Hide;
                progressBarTotal.Visible = true;
                _stopwatch.Start();
                elapsedTimer.Start();
            }
        }

        private void ShowControl(Control control)
        {
            panelError.Visible = ReferenceEquals(control, panelError);
            labelFileName.Visible = ReferenceEquals(control, labelFileName);
            graphChromatograms.Visible = ReferenceEquals(control, graphChromatograms);
        }

        private ChromatogramLoadingStatus FindStatus(MultiProgressStatus status, MsDataFileUri filePath)
        {
            foreach (ChromatogramLoadingStatus loadingStatus in status.ProgressList)
            {
                if (loadingStatus.FilePath.Equals(filePath))
                {
                    return loadingStatus;
                }
            }
            return null;
        }

        private void AddProgressControls(MultiProgressStatus status)
        {
            // Nothing to do if everything is already covered
            if (status.ProgressList.All(s => FindProgressControl(s.FilePath) != null))
                return;

            // Match each file status with a progress control.
            bool first = true;
            var width = flowFileStatus.Width - 2 - // Avoid clipping the cancel/retry button when we need a vertical scrollbar
                        (flowFileStatus.VerticalScroll.Visible || 
                         status.ProgressList.Count > (panelFileList.Height / (new FileProgressControl()).Height)  // If scrollbar isn't visible already, it's about to be
                            ? SystemInformation.VerticalScrollBarWidth
                            : 0);
            List<FileProgressControl> controlsToAdd = new List<FileProgressControl>();
            foreach (var loadingStatus in status.ProgressList)
            {
                var filePath = loadingStatus.FilePath;
                var progressControl = FindProgressControl(filePath);
                if (progressControl != null)
                    continue;

                // Create a progress control for new file.
                progressControl = new FileProgressControl
                {
                    Number = flowFileStatus.Controls.Count + controlsToAdd.Count + 1,
                    Width = width,
                    Selected = first,
                    BackColor = SystemColors.Control,
                    FilePath = filePath
                };
                progressControl.SetToolTip(toolTip1, filePath.GetFilePath());
                int index = progressControl.Number - 1;
                progressControl.ControlMouseDown += (sender, args) => { Selected = index; };
                var thisLoadingStatus = loadingStatus;
                progressControl.Retry += (sender, args) => Retry(thisLoadingStatus);
                progressControl.Cancel += (sender, args) => Cancel(thisLoadingStatus);
                progressControl.ShowGraph += (sender, args) => ShowGraph();
                progressControl.ShowLog += (sender, args) => ShowLog();
                controlsToAdd.Add(progressControl);
                _fileProgressControls.Add(filePath.GetLocation(), progressControl);
                first = false;
            }

            flowFileStatus.Controls.AddRange(controlsToAdd.ToArray());
        }

        private void CancelMissingFiles(MultiProgressStatus status)
        {
            HashSet<MsDataFileUri> filesWithStatus = null;
            foreach (FileProgressControl progressControl in flowFileStatus.Controls)
            {
                if (!progressControl.IsComplete && !progressControl.IsCanceled)
                {
                    if (filesWithStatus == null)
                    {
                        filesWithStatus = new HashSet<MsDataFileUri>(status.ProgressList
                            .Select(loadingStatus => loadingStatus.FilePath));
                    }
                    if (!filesWithStatus.Contains(progressControl.FilePath))
                        progressControl.IsCanceled = true;
                }
            }
        }

        private FileProgressControl FindProgressControl(MsDataFileUri filePath)
        {
            FileProgressControl fileProgressControl;
            _fileProgressControls.TryGetValue(filePath.GetLocation(), out fileProgressControl);
            return fileProgressControl;
        }

        private void Retry(ChromatogramLoadingStatus status)
        {
            ChromatogramManager.RemoveFile(status.FilePath);
            if (!_partialProgressList.Contains(status.FilePath))
                _partialProgressList.Add(status.FilePath);
            graphChromatograms.ClearGraph(status.FilePath);
            for (int i = 0; i < flowFileStatus.Controls.Count; i++)
            {
                var control = (FileProgressControl) flowFileStatus.Controls[i];
                if (control.FilePath.Equals(status.FilePath))
                {
                    control.Reset();
                    Selected = i;
                    break;
                }
            }

            // Add this file back into the chromatogram set for each of its replicates.
            ModifyDocument(GraphsResources.AllChromatogramsGraph_Retry_Retry_import_results, monitor =>
            {
                Program.MainWindow.ModifyDocumentNoUndo(doc =>
                    {
                        var oldResults = doc.Settings.MeasuredResults ??
                                         new MeasuredResults(new ChromatogramSet[0]);
                        var newResults = oldResults.AddDataFile(status.FilePath, status.ReplicateNames);
                        return doc.ChangeMeasuredResults(newResults, monitor);
                    });
            });
        }

        private void ModifyDocument(string message, Action<SrmSettingsChangeMonitor> modifyAction)
        {
            try
            {
                using var longWaitDlg = new LongWaitDlg(Program.MainWindow);
                longWaitDlg.Text = Text; // Same as dialog box
                longWaitDlg.Message = message;
                longWaitDlg.ProgressValue = 0;
                longWaitDlg.PerformWork(this, 800, progressMonitor =>
                {
                    using (var settingsChangeMonitor =
                           new SrmSettingsChangeMonitor(progressMonitor, message, Program.MainWindow))
                    {
                        modifyAction(settingsChangeMonitor);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // SrmSettingsChangeMonitor can throw OperationCancelledException without LongWaitDlg knowing about it.
            }
            catch (Exception exception)
            {
                ExceptionUtil.DisplayOrReportException(Program.MainWindow, exception);
            }
        }

        private void Cancel(ChromatogramLoadingStatus status)
        {
            // Remove this file from document.
            var canceledPath = status.FilePath;
            ModifyDocument(GraphsResources.AllChromatogramsGraph_Cancel_Cancel_file_import,
                monitor => Program.MainWindow.ModifyDocumentNoUndo(
                    doc => FilterFiles(doc, info => !info.FilePath.Equals(canceledPath))));
        }

        private void RemoveFailedFile(ChromatogramLoadingStatus status)
        {
            // Remove this file from document.
            var canceledPath = status.FilePath;
            ModifyDocument(GraphsResources.AllChromatogramsGraph_RemoveFailedFile_Remove_failed_file,
                monitor => Program.MainWindow.ModifyDocumentNoUndo(
                    doc => FilterFiles(doc, info => !info.FilePath.Equals(canceledPath))));
        }

        private void ShowGraph()
        {
            ShowControl(graphChromatograms);
        }

        private void ShowLog()
        {
            ShowControl(panelError);
        }


        // Close the window.
        private void btnClose_Click(object sender, EventArgs e)
        {
            ClickClose();
        }

        public void ClickClose()
        {
            if (Finished)
                Program.MainWindow.DestroyAllChromatogramsGraph();
            else
                Hide();
        }

        public bool Finished
        {
            get
            {
                foreach (FileProgressControl control in flowFileStatus.Controls)
                {
                    if (!control.IsCanceled && control.Error == null && control.Progress < 100)
                        return false;
                }
                return true;
            }
        }

        // Cancel all uncached files.
        private void btnCancel_Click(object sender, EventArgs e)
        {
            ClickCancel();
        }

        public void ClickCancel()
        {
            graphChromatograms.IsCanceled = IsUserCanceled = true;
            Program.MainWindow.ModifyDocument(GraphsResources.AllChromatogramsGraph_btnCancel_Click_Cancel_import,
                doc => FilterFiles(doc, info => IsCachedFile(doc, info)),
                docPair => AuditLogEntry.CreateSimpleEntry(MessageType.canceled_import, docPair.OldDocumentType));
        }

        private bool IsCachedFile(SrmDocument doc, ChromFileInfo info)
        {
            return doc.Settings.MeasuredResults.IsCachedFile(info.FilePath);
        }

        /// <summary>
        /// Filters document chromatograms for all but a selected set of files.
        /// </summary>
        private SrmDocument FilterFiles(SrmDocument doc, Func<ChromFileInfo, bool> selectFilesToKeepFunc)
        {
            if (doc.Settings.MeasuredResults == null)
                return doc;

            var measuredResultsNew = doc.Settings.MeasuredResults.FilterFiles(selectFilesToKeepFunc);

            // If nothing changed, don't create a new document instance
            if (measuredResultsNew != null &&
                ArrayUtil.ReferencesEqual(measuredResultsNew.Chromatograms, doc.Settings.MeasuredResults.Chromatograms))
            {
                return doc;
            }

            return doc.ChangeMeasuredResults(measuredResultsNew);
        }

        private void btnAutoCloseWindow_Click(object sender, EventArgs e)
        {
            ClickAutoCloseWindow();
        }

        public void ClickAutoCloseWindow()
        {
            Settings.Default.ImportResultsAutoCloseWindow = !Settings.Default.ImportResultsAutoCloseWindow;
            btnAutoCloseWindow.Image = imageListPushPin.Images[Settings.Default.ImportResultsAutoCloseWindow ? 1 : 0];
        }
         
        private void btnAutoScaleGraphs_Click(object sender, EventArgs e)
        {
            ClickAutoScaleGraphs();
        }

        public void ClickAutoScaleGraphs()
        {
            Settings.Default.ImportResultsAutoScaleGraph = !Settings.Default.ImportResultsAutoScaleGraph;
            btnAutoScaleGraphs.Image = imageListLock.Images[Settings.Default.ImportResultsAutoScaleGraph ? 1 : 0];
            graphChromatograms.ScaleIsLocked = !Settings.Default.ImportResultsAutoScaleGraph;
        }

        private void cbShowErrorDetails_CheckedChanged(object sender, EventArgs e)
        {
            textBoxError.Text = GetSelectedControlErrorLog();
        }

        private string GetSelectedControlErrorLog()
        {
            return SelectedControl == null ? string.Empty : SelectedControl.GetErrorLog(cbMoreInfo.Checked);
        }

        private void btnCopyText_Click(object sender, EventArgs e)
        {
            ClipboardHelper.SetClipboardText(this, GetSelectedControlErrorLog());
        }

        #region Testing Support

        public int ProgressTotalPercent
        {
            get
            {
                return (100*(progressBarTotal.Value - -progressBarTotal.Minimum))/(progressBarTotal.Maximum - progressBarTotal.Minimum);
            }
        }

        // Click the button for this named file - first click is cancel, which toggles to retry
        public void FileButtonClick(string name)
        {
            foreach (FileProgressControl control in flowFileStatus.Controls)
            {
                if (control.FilePath.GetFileName().Contains(name))
                {
                    control.ButtonClick();
                    break;
                }
            }
        }

        public IEnumerable<string> GetErrorMessages()
        {
            foreach (FileProgressControl control in flowFileStatus.Controls)
            {
                if (control.Error != null)
                    yield return control.GetErrorLog(true);
            }
        }

        public override string DetailedMessage
        {
            get
            {
                var sb = new StringBuilder();
                foreach (FileProgressControl control in flowFileStatus.Controls)
                {
                    if (ReferenceEquals(SelectedControl, control))
                        sb.Append(@"-> ");
                    if (control.Error != null)
                        sb.AppendLine(string.Format(@"{0}: Error - {1}", control.FilePath, control.Error));
                    else if (control.IsCanceled)
                        sb.AppendLine(string.Format(@"{0}: Canceled", control.FilePath));
                    else
                        sb.AppendLine(string.Format(@"{0}: {1}%", control.FilePath, control.Progress));
                }
                return TextUtil.LineSeparate(sb.ToString(), string.Format(@"Total complete: {0}%", ProgressTotalPercent));
            }
        }

        #endregion

    }

    public class DisabledRichTextBox : RichTextBox
    {
        private const int WM_SETFOCUS = 0x07;
        private const int WM_ENABLE = 0x0A;
        private const int WM_SETCURSOR = 0x20;

        protected override void WndProc(ref Message m)
        {
            if (!(m.Msg == WM_SETFOCUS || m.Msg == WM_ENABLE || m.Msg == WM_SETCURSOR))
                base.WndProc(ref m);
        }
    }

    class CustomToolStripProfessionalRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            // Don't draw a border
        }
    }
}
