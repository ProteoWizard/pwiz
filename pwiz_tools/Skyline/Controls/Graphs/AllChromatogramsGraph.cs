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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// A window that progressively displays chromatogram data during file import.
    /// </summary>
    public partial class AllChromatogramsGraph : FormEx
    {
        private string _currentFilePath;
        private readonly int _adjustLayoutForMultifile;

        //private static readonly Log LOG = new Log<AllChromatogramsGraph>();

        public AllChromatogramsGraph()
        {
            InitializeComponent();

            // Keep VS designer from crashing.
            if (Program.MainWindow == null)
                return;

            Icon = Resources.Skyline;
            lblFileName.Text = string.Empty;
            lblFileCount.Text = string.Empty;
            
            // Restore window placement.
            Size size = Settings.Default.AllChromatogramsSize;
            if (!size.IsEmpty)
                Size = size;
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
                    var screen = Screen.FromPoint(location);
                    location.X = Math.Min(location.X, screen.WorkingArea.Right - Size.Width);
                    location.Y = Math.Min(location.Y, screen.WorkingArea.Bottom - Size.Height);
                    Location = location;
                }
            }
            if (Settings.Default.AllChromatogramsMaximized)
                WindowState = FormWindowState.Maximized;
            Move += WindowMove;
            FormClosing += AllChromatogramsGraph_FormClosing;

            // Assume just one file.
            panelMultifileProgress.Visible = false;
            _adjustLayoutForMultifile = panelMultifileProgress.Top - panelFileProgress.Top;
            panelFileProgress.Top = panelMultifileProgress.Top;
            panelGraph.Height += _adjustLayoutForMultifile;
            btnCancelFile.Visible = false;
        }

        // AllChromatogramsGraph still has work to do processing chromatogram data
        // even after the user closes the window, so we just hide the window instead
        // of actually closing it.
        private void AllChromatogramsGraph_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        /// <summary>
        /// Prepare to close this window for realy (instead of just hiding it).
        /// </summary>
        public void Finish()
        {
            asyncGraph.Finish();
            FormClosing -= AllChromatogramsGraph_FormClosing;
        }

        private void WindowMove(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Normal)
                Settings.Default.AllChromatogramsLocation = Location;
            Settings.Default.AllChromatogramsMaximized =
                (WindowState == FormWindowState.Maximized);
        }

        private void WindowResize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Normal)
                Settings.Default.AllChromatogramsSize = Size;
            Settings.Default.AllChromatogramsMaximized =
                (WindowState == FormWindowState.Maximized);
        }

        /// <summary>
        /// Display chromatogram data. 
        /// </summary>
        /// <param name="status"></param>
        public void UpdateStatus(ChromatogramLoadingStatus status)
        {
            // Update progress bars and progress labels.
            var fileCount = Math.Max(1, status.SegmentCount - 1);

            // Update file label.
            if (status.Segment == status.SegmentCount-1)
            {
                lblFileName.Visible = false;
                progressBarFile.Visible = false;
                lblFileCount.Text = Resources.AllChromatogramsGraph_UpdateStatus_Joining_chromatograms___;
            }
            else
            {
                progressBarFile.Value = status.ZoomedPercentComplete;

                // Clear graph when a new file starts loading.
                if (!string.IsNullOrEmpty(status.FilePath) && _currentFilePath != status.FilePath)
                {
                    _currentFilePath = status.FilePath;
//                    LOG.Info("Showing " + _currentFilePath);    // Not L10N
                    lblFileName.Text = SampleHelp.GetFileName(status.FilePath) + SampleHelp.GetPathSampleNamePart(status.FilePath);
                    asyncGraph.ClearGraph(status);
                }

                lblFileCount.Text = string.Format(Resources.AllChromatogramsGraph_UpdateStatus__0__of__1__files, status.Segment + 1, fileCount);
            }

            // Show multi-file progress bar if we have more than one file to import.
            if (fileCount != 1)
            {
                if (Visible && !panelMultifileProgress.Visible)
                {
                    panelMultifileProgress.Visible = true;
                    panelFileProgress.Top -= _adjustLayoutForMultifile;
                    panelGraph.Height -= _adjustLayoutForMultifile;
                    // TODO: uncomment this when single file cancellation works
                    //btnCancelFile.Visible = true;
                }
                progressBarAllFiles.Value = status.PercentComplete;
            }
        }

        // Close the window.
        private void btnClose_Click(object sender, EventArgs e)
        {
            Hide();
        }

        // Cancel all uncached files.
        private void btnCancel_Click(object sender, EventArgs e)
        {
            Hide();
            Program.MainWindow.ModifyDocument(Resources.AllChromatogramsGraph_btnCancel_Click_Cancel_import,
                                              doc => FilterFiles(doc, info => IsCachedFile(doc, info)));
        }

        private bool IsCachedFile(SrmDocument doc, ChromFileInfo info)
        {
            return doc.Settings.MeasuredResults.IsCachedFile(info.FilePath);
        }

        // Cancel one file.
        private void btnCancelFile_Click(object sender, EventArgs e)
        {
            Program.MainWindow.ModifyDocument(Resources.AllChromatogramsGraph_btnCancelFile_Click_Cancel_file,
                                              doc => FilterFiles(doc, info => !Equals(info.FilePath, _currentFilePath)));
        }

        /// <summary>
        /// Filters document chromatograms for all but a selected set of files.
        /// </summary>
        private SrmDocument FilterFiles(SrmDocument doc, Func<ChromFileInfo, bool> selectFilesToKeepFunc)
        {
            if (doc.Settings.MeasuredResults == null)
                return doc;

            var keepChromatograms = new List<ChromatogramSet>();
            foreach (var chromSet in doc.Settings.MeasuredResults.Chromatograms)
            {
                var keepFiles = chromSet.MSDataFileInfos.Where(selectFilesToKeepFunc).ToList();
                if (keepFiles.Count != 0)
                {
                    if (keepFiles.Count == chromSet.FileCount)
                        keepChromatograms.Add(chromSet);
                    else
                        keepChromatograms.Add(chromSet.ChangeMSDataFileInfos(keepFiles));
                }
            }

            // If nothing changed, don't create a new document instance
            if (ArrayUtil.ReferencesEqual(keepChromatograms, doc.Settings.MeasuredResults.Chromatograms))
                return doc;

            return doc.ChangeMeasuredResults(keepChromatograms.Count > 0
                                                 ? doc.Settings.MeasuredResults.ChangeChromatograms(keepChromatograms)
                                                 : null);
        }
    }
}
