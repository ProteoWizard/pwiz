//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the Bumberdash project.
//
// The Initial Developer of the Original Code is Jay Holman.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari, Matt Chambers
//
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using BumberDash.lib;
using BumberDash.Model;
using CustomDataSourceDialog;
using CustomProgressCell;
using pwiz.CLI.msdata;

namespace BumberDash.Forms
{
    public partial class QueueForm : Form
    {
        #region Globals

        public delegate void JobDoneDelegate(string name, bool successful);

        private Rectangle _dragBoxFromMouseDown; //Created on valid mousedown
        internal ProgramHandler JobProcess; //eventual implememtation of program running
        private LogForm _jobLog;
        private int _rowIndexFromMouseDown; //holds clicked row
        private int _rowIndexOfItemUnderMouseToDrop = -1; //Changes as row is dragged around
        internal int LastCompleted = -1; //keeps track of where the scanner is in the list
        private bool _programmaticallyPaused; //indicates if a job can run
        private bool _manuallyPaused; //indicates if a job can run (set by user)
        private readonly NHibernate.ISession _session;

        #endregion
        
        /// <summary>
        /// Main form that the Bumbershoot GUI runs from
        /// </summary>
        public QueueForm()
        {
            try
            {
                InitializeComponent();
                var sessionFactory = SessionManager.CreateSessionFactory();
                _session = sessionFactory.OpenSession();
            }
            catch (Exception error)
            {
                MessageBox.Show("BumberDash could not initilize:" + error.Message + Environment.NewLine + error.StackTrace);
                throw;
            }
        }


        /// <summary>
        /// Initialize the DataGridView with previous jobs and add row at bottom for easy job queuing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void QueueForm_Load(object sender, EventArgs e)
        {
            try
            {
                _jobLog = new LogForm();

                #region Initialize program handler
                JobProcess = new ProgramHandler
                {
                    JobFinished = (x, y) =>
                    {
                        if (InvokeRequired)
                        {
                            ProgramHandler.ExitDelegate jobdelegate = IndicateJobDone;
                            Invoke(jobdelegate, x, y);
                        }
                        else
                            IndicateJobDone(x, y);
                    },
                    StatusUpdate = (x, y) =>
                    {
                        if (InvokeRequired)
                        {
                            ProgramHandler.StatusDelegate statusdelegate =
                                UpdateStatusText;
                            Invoke(statusdelegate, x, y);
                        }
                        else
                            UpdateStatusText(x, y);
                    },
                    LogUpdate = x =>
                    {
                        if (InvokeRequired)
                        {
                            ProgramHandler.LogDelegate logdelegate = AddLogLine;
                            Invoke(logdelegate, x);
                        }
                        else
                            AddLogLine(x);
                    },
                    ErrorForward = x =>
                    {
                        if (InvokeRequired)
                        {
                            ProgramHandler.LogDelegate logdelegate = ShowError;
                            Invoke(logdelegate, x);
                        }
                        else
                            ShowError(x);
                    },
                    PercentageUpdate = x =>
                    {
                        if (InvokeRequired)
                        {
                            ProgramHandler.PercentageDelegate percentdelegate =
                                SetPercentage;
                            Invoke(percentdelegate, x);
                        }
                        else
                            SetPercentage(x);
                    }
                };
                #endregion

                //Load all jobs from database
                var historyItemList = _session.QueryOver<HistoryItem>().OrderBy(x => x.RowNumber).Asc.List();
                foreach (var hi in historyItemList)
                    InsertRowFromHistoryItem(hi, JobQueueDGV.Rows.Count);

                //Add line at end for quick job creation
                var values = new object[6];
                values[0] = "Click to add new job";
                values[1] = string.Empty;
                values[2] = string.Empty;
                values[3] = string.Empty;
                values[4] = string.Empty;
                values[5] = 0;
                JobQueueDGV.Rows.Add(values);
                JobQueueDGV.Rows[JobQueueDGV.Rows.Count - 1].DefaultCellStyle.BackColor = Color.LightGray;

                for (int x = JobQueueDGV.Rows.Count - 2; x >= 0; x--)
                {
                    var progressCell = (DataGridViewProgressCell)JobQueueDGV[5, x];
                    if ((string)JobQueueDGV.Rows[x].Tag == "Finished" || (string)JobQueueDGV.Rows[x].Tag == "Unsuccessful")
                    {
                        progressCell.Message = (string)JobQueueDGV.Rows[x].Tag == "Finished" ? "Finished" : "Unsuccessful";
                        if (LastCompleted == -1)
                            LastCompleted = x;
                    }
                    else if ((string)JobQueueDGV.Rows[x].Tag == "Locked")
                        progressCell.Message = "Locked";
                }

                //Configure IDPicker Location
                if (Properties.Settings.Default.IDPickerLocation == string.Empty
                    || !File.Exists(Properties.Settings.Default.IDPickerLocation))
                {
                    if (!DetectLatestIDPicker())
                        iDPickerToolStripMenuItem.Visible = false;
                }
            }
            catch (Exception error)
            {
                MessageBox.Show("BumberDash could not load: " + error.Message + Environment.NewLine + error.StackTrace);
                throw;
            }

        }

        #region Events

        #region DataGridView Events

        /// <summary>
        /// Starts dragging DataGridView row if the mouse button is held down
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void JobQueueDGV_MouseMove(object sender, MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                // If the mouse moves outside the rectangle, start the drag.
                if (_dragBoxFromMouseDown != Rectangle.Empty &&
                    !_dragBoxFromMouseDown.Contains(e.X, e.Y))
                {
                    // Proceed with the drag and drop, passing in the list item.                    
                    JobQueueDGV.DoDragDrop(
                        JobQueueDGV.Rows[_rowIndexFromMouseDown],
                        DragDropEffects.Move);
                }
            }
        }

        /// <summary>
        /// Bring up context menu if the right mouse button is pressed or prepare DataGridView row for dragging if left is pressed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void JobQueueDGV_MouseDown(object sender, MouseEventArgs e)
        {
            // Get the index of the item the mouse is below.
            _rowIndexFromMouseDown = JobQueueDGV.HitTest(e.X, e.Y).RowIndex;

            if (_rowIndexFromMouseDown != -1)
            {
                _programmaticallyPaused = true;

                if (_rowIndexFromMouseDown != JobQueueDGV.Rows.Count - 1 && e.Button == MouseButtons.Right)
                {
                    //guarantee right clicked cell is selected
                    JobQueueDGV.CurrentCell = JobQueueDGV.CurrentCell ==
                                              JobQueueDGV.Rows[_rowIndexFromMouseDown].Cells[0]
                                                  ? JobQueueDGV.Rows[_rowIndexFromMouseDown].Cells[1]
                                                  : JobQueueDGV.Rows[_rowIndexFromMouseDown].Cells[0];

                    //check for lock status
                    lockToolStripMenuItem.Text = (string) JobQueueDGV.Rows[_rowIndexFromMouseDown].Tag == "Locked"
                                                     ? "Unlock"
                                                     : "Lock";

                    //show context menu
                    JQRowMenu.Show(JobQueueDGV, new Point(e.X, e.Y));
                }
                else if (_rowIndexFromMouseDown != JobQueueDGV.Rows.Count - 1 &&
                         _rowIndexFromMouseDown > LastCompleted + 1)
                {
                    // Remember the point where the mouse down occurred. 
                    // The DragSize indicates the size that the mouse can move 
                    // before a drag event should be started.                
                    Size dragSize = SystemInformation.DragSize;
                    // Create a rectangle using the DragSize, with the mouse position being
                    // at the center of the rectangle.
                    _dragBoxFromMouseDown = new Rectangle(new Point(e.X - (dragSize.Width/2),
                                                                    e.Y - (dragSize.Height/2)),
                                                          dragSize);
                }
                else
                    // Reset the rectangle if the mouse is not over an item in the ListBox.
                    _dragBoxFromMouseDown = Rectangle.Empty;
            }
            else
                // Reset the rectangle if the mouse is not over an item in the ListBox.
                _dragBoxFromMouseDown = Rectangle.Empty;
        }

        /// <summary>
        /// Get row under pointer and force DataGridView to redraw itself (triggering the CellPainting event)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void JobQueueDGV_DragOver(object sender, DragEventArgs e)
        {
            Point clientPoint = JobQueueDGV.PointToClient(new Point(e.X, e.Y));

            int overIndex = JobQueueDGV.HitTest(clientPoint.X, clientPoint.Y).RowIndex;
            e.Effect = DragDropEffects.Move;


            if (_rowIndexOfItemUnderMouseToDrop != overIndex)
            {
                _rowIndexOfItemUnderMouseToDrop = overIndex;
                JobQueueDGV.Invalidate();
            }

        }

        /// <summary>
        /// Move dragged row to the position indicated by the black line
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void JobQueueDGV_DragDrop(object sender, DragEventArgs e)
        {
            if (_rowIndexOfItemUnderMouseToDrop != -1 &&
                (_rowIndexOfItemUnderMouseToDrop > LastCompleted + 1 ||
                 (_rowIndexOfItemUnderMouseToDrop == LastCompleted + 1 && !JobProcess.JobIsRunning())))
            {
                // The mouse locations are relative to the screen, so they must be 
                // converted to client coordinates.
                Point clientPoint = JobQueueDGV.PointToClient(new Point(e.X, e.Y));
                // Get the row index of the item the mouse is below. 
                _rowIndexOfItemUnderMouseToDrop =
                    JobQueueDGV.HitTest(clientPoint.X, clientPoint.Y).RowIndex;

                // If the drag operation was a move then remove and insert the row.
                if (
                    e.Effect == DragDropEffects.Move &&
                    _rowIndexOfItemUnderMouseToDrop != _rowIndexFromMouseDown &&
                    _rowIndexOfItemUnderMouseToDrop != _rowIndexFromMouseDown + 1
                    )
                {
                    var rowToMove = e.Data.GetData(
                        typeof (DataGridViewRow)) as DataGridViewRow;

                    JobQueueDGV.Rows.RemoveAt(_rowIndexFromMouseDown);

                    if (_rowIndexOfItemUnderMouseToDrop < _rowIndexFromMouseDown)
                    {
                        JobQueueDGV.Rows.Insert(_rowIndexOfItemUnderMouseToDrop, rowToMove);
                        JobQueueDGV.CurrentCell = JobQueueDGV.Rows[_rowIndexOfItemUnderMouseToDrop].Cells[0];
                    }
                    else
                    {
                        JobQueueDGV.Rows.Insert(_rowIndexOfItemUnderMouseToDrop - 1, rowToMove);
                        JobQueueDGV.CurrentCell = JobQueueDGV.Rows[_rowIndexOfItemUnderMouseToDrop - 1].Cells[0];
                    }
                }
            }
            if (_programmaticallyPaused)
                _programmaticallyPaused = false;
            _rowIndexOfItemUnderMouseToDrop = -1;
            JobQueueDGV.Invalidate();
            SaveRowNumbers();
            CheckForRunableJob();
        }

        /// <summary>
        /// Places black line between cells if a row is being dragged
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void JobQueueDGV_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex != -1)
            {
                if (e.RowIndex != -1 && e.RowIndex == _rowIndexOfItemUnderMouseToDrop &&
                    (_rowIndexOfItemUnderMouseToDrop > LastCompleted + 1 ||
                     (_rowIndexOfItemUnderMouseToDrop == LastCompleted + 1 && !JobProcess.JobIsRunning())))
                {
                    var p = new Pen(Color.Black, 2);
                    e.Graphics.DrawLine(p, e.CellBounds.Left, e.CellBounds.Top - 1, e.CellBounds.Right,
                                        e.CellBounds.Top - 1);
                }
            }
        }

        /// <summary>
        /// Allows Edit box to be displayed if user hits F2 on a row
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void JobQueueDGV_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            ShowEditBox(e.RowIndex);
            e.Cancel = true;
        }

        /// <summary>
        /// Enable checking for new jobs and immediately check for new job after context menu has closed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void JobQueueDGV_MouseUp(object sender, MouseEventArgs e)
        {
            if (_programmaticallyPaused && JQRowMenu.Visible == false)
            {
                _programmaticallyPaused = false;
                CheckForRunableJob();
            }
        }

        /// <summary>
        /// Handle kill button being clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void JobQueueDGV_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 6 && e.RowIndex != JobQueueDGV.Rows.Count - 1)
                KillCheck(e.RowIndex);
        }

        #endregion


        #region JobQueue Context Menu

        /// <summary>
        /// Tries to open the output folder and checks if there are any new jobs
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void openOutputFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DataGridViewCell cell = JobQueueDGV.Rows[JobQueueDGV.SelectedRows[0].Index].Cells[1];

            try
            {
                System.Diagnostics.Process.Start(cell.ToolTipText.TrimEnd('+').TrimEnd('*'));
            }
            catch
            {
                MessageBox.Show("Cannot open folder");
            }

            CheckForRunableJob();
        }

        /// <summary>
        /// Brings up a new AddJobForm with all rows filled out with the cloned job's parameters
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cloneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowCloneBox((HistoryItem) JobQueueDGV.SelectedRows[0].Cells[0].Tag);
        }

        /// <summary>
        /// Prevents row from being selected to run, or unlocks job to be run
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lockToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (lockToolStripMenuItem.Text == "Lock")
            {
                if ((string) JobQueueDGV.SelectedRows[0].Tag == string.Empty)
                {
                    JobQueueDGV.SelectedRows[0].Tag = "Locked";
                    ((HistoryItem) JobQueueDGV.SelectedRows[0].Cells[0].Tag).CurrentStatus = "Locked";
                    _session.SaveOrUpdate(JobQueueDGV.SelectedRows[0].Cells[0].Tag);
                    for (var x = 0; x < 6; x++)
                        JobQueueDGV.SelectedRows[0].Cells[x].Style.BackColor = Color.Wheat;
                    var progressCell = (DataGridViewProgressCell)JobQueueDGV.SelectedRows[0].Cells[5];
                    progressCell.Message = "Locked";
                    progressCell.ProgressBarStyle = ProgressBarStyle.Continuous;
                }
            }
            else
            {
                JobQueueDGV.SelectedRows[0].Tag = string.Empty;
                ((HistoryItem) JobQueueDGV.SelectedRows[0].Cells[0].Tag).CurrentStatus = string.Empty;
                _session.SaveOrUpdate(JobQueueDGV.SelectedRows[0].Cells[0].Tag);
                for (var x = 0; x < 6; x++)
                    JobQueueDGV.SelectedRows[0].Cells[x].Style.BackColor = Color.White;
                var progressCell = (DataGridViewProgressCell)JobQueueDGV.SelectedRows[0].Cells[5];
                progressCell.Message = string.Empty;
                progressCell.ProgressBarStyle = ProgressBarStyle.Continuous;
                IsValidJob(JobQueueDGV.SelectedRows[0].Index);
            }
            _session.Flush();
            CheckForRunableJob();
        }

        /// <summary>
        /// Marks job for deletion and calls function to determine how to execute delete
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            KillCheck(JobQueueDGV.SelectedRows[0].Index);

            _programmaticallyPaused = false;
            CheckForRunableJob();
        }

        /// <summary>
        /// Calls the ShowEditBox function to edit job
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void editToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowEditBox(JobQueueDGV.SelectedRows[0].Index);
        }

        /// <summary>
        /// Releases internal queue pause when right click menu is closed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void JQRowMenu_Closed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            _programmaticallyPaused = false;
        }

        #endregion


        #region Top Menu

        /// <summary>
        /// Exit the program
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (JobProcess.JobIsRunning())
            {
                if (MessageBox.Show("Abort current job and exit?",
                    "Exit program", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    Environment.Exit(0);
            }
            else
                Environment.Exit(0);
        }

        /// <summary>
        /// Toolstrip handling of adding new job
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void newJobToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowAddBox();
        }

        /// <summary>
        /// Start IDPicker from toolstrip
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void iDPickerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(Properties.Settings.Default.IDPickerLocation);
            }
            catch
            {
                MessageBox.Show("IDPicker location invalid, please re-define correct location");
                Properties.Settings.Default.IDPickerLocation = string.Empty;
                Properties.Settings.Default.Save();
            }
        }

        /// <summary>
        /// Start full version of Config Editor from toolstrip
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void confiToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var editor = new ConfigForm(_session);
            editor.ShowDialog();
        }

        /// <summary>
        /// Allows the user to tell Bumberdash where their installed copy of IDPicker can be accessed from
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void setIDPickerLocationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var startPath =
                Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                                              "Bumbershoot"))
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Bumbershoot")
                    : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);


            var openFile = new OpenFileDialog
                               {
                                   Filter = "Executable (.exe)|*.exe",
                                   Title = "ID Picker Location",
                                   InitialDirectory = startPath,
                                   RestoreDirectory = true,
                                   CheckFileExists = true,
                                   CheckPathExists = true,
                                   Multiselect = false
                               };

            if (openFile.ShowDialog() == DialogResult.OK)
            {
                Properties.Settings.Default.IDPickerLocation = openFile.FileName;
                Properties.Settings.Default.Save();
                iDPickerToolStripMenuItem.Visible = true;
            }
        }

        /// <summary>
        /// Shows current version information
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var af = new AboutForm();
            af.ShowDialog();
            af.Close();
            af.Dispose();
        }

        /// <summary>
        /// Links to tutorial on how to use BumberDash
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void documentationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var helpFile = String.Format("\"{0}/lib/BumberDash Help 2.htm\"", Application.StartupPath.Replace("\\", "/"));
            System.Diagnostics.Process.Start(helpFile);
        }

        #endregion


        /// <summary>
        /// Minimize to system tray
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void QueueForm_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                TrayIcon.Visible = true;
                TrayIcon.ShowBalloonTip(5000);
            }
        }

        /// <summary>
        /// Restore from system tray by double-clicking
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TrayIcon_DoubleClick(object sender, EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            TrayIcon.Visible = false;
        }

        /// <summary>
        /// Restore from system tray with menu option
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void showToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            TrayIcon.Visible = false;
        }

        /// <summary>
        /// Displays a log of actions taken by the ProgramHandler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LogButton_Click(object sender, EventArgs e)
        {
            if (_jobLog.CanFocus)
                _jobLog.Hide();
            else
            {
                _jobLog.Show();
                _jobLog.ScrollToBottom();
            }
        }

        /// <summary>
        /// Sets or releases manual pause on job queuing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void pauseButton_Click(object sender, EventArgs e)
        {
            if (pauseButton.Text == "Start")
            {
                _manuallyPaused = false;
                pauseButton.Text = "Pause";
                CheckForRunableJob();
            }
            else
            {
                _manuallyPaused = true;
                pauseButton.Text = "Start";
            }
        }

        /// <summary>
        /// Stops procerssing job on close or gives user option to abort close
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void QueueForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (JobProcess != null && JobProcess.JobIsRunning() &&
                MessageBox.Show("Are you sure you want to exit? Progress on the current job will be lost.",
                                "Exit Program", MessageBoxButtons.YesNo) == DialogResult.Yes)
                JobProcess.ForceKill();
            else if (JobProcess != null && JobProcess.JobIsRunning())
                e.Cancel = true;

            if (!e.Cancel)
            {
                _jobLog.FullyClose();

                //clean up database
                RemoveUnusedConfigs();
            }
        }

        #endregion


        #region QueueForm <-> AddJobForm Functions

        /// <summary>
        /// Show empty AddJobForm
        /// </summary>
        private void ShowAddBox()
        {
            var oldconfigs = _session.QueryOver<HistoryItem>().OrderBy(x => x.RowNumber).Desc.List();
            var templateList = _session.QueryOver<ConfigFile>().Where(x => x.FilePath == "Template").List();
            var addJob = new AddJobForm(oldconfigs, templateList);
            if (addJob.ShowDialog() == DialogResult.OK)
            {
                _programmaticallyPaused = true;
                InsertRowFromHistoryItem(addJob.GetHistoryItem(), JobQueueDGV.Rows.Count - 1);
                _programmaticallyPaused = false;
            }

            CheckForRunableJob();
        }

        /// <summary>
        /// Show AddJobForm with given properties already filled out
        /// </summary>
        /// <param name="oldHi"></param>
        private void ShowCloneBox(HistoryItem oldHi)
        {
            var oldconfigs = _session.QueryOver<HistoryItem>().OrderBy(x => x.RowNumber).Desc.List();
            var templateList = _session.QueryOver<ConfigFile>().Where(x => x.FilePath == "Template").List();
            var addJob = new AddJobForm(oldconfigs, oldHi, false, templateList);
            if (addJob.ShowDialog() == DialogResult.OK)
            {
                _programmaticallyPaused = true;
                InsertRowFromHistoryItem(addJob.GetHistoryItem(), JobQueueDGV.Rows.Count - 1);
                _programmaticallyPaused = false;
            }

            CheckForRunableJob();
        }

        /// <summary>
        /// Mark row for editing and show an AddJobForm specified for editing
        /// </summary>
        /// <param name="row"></param>
        private void ShowEditBox(int row)
        {
            var locked = (string)JobQueueDGV.Rows[row].Tag == "Locked";
            JobQueueDGV.Rows[row].Tag = "Editing";
            var oldconfigs = _session.QueryOver<HistoryItem>().OrderBy(x => x.RowNumber).Desc.List();
            var oldHi = (HistoryItem) JobQueueDGV[0, row].Tag;
            var templateList = _session.QueryOver<ConfigFile>().Where(x => x.FilePath == "Template").List();
            var addJob = new AddJobForm(oldconfigs, oldHi, true, templateList);
            if (addJob.ShowDialog() == DialogResult.OK)
            {
                _programmaticallyPaused = true;
                _session.Delete(oldHi);
                JobQueueDGV.Rows.RemoveAt(row);
                InsertRowFromHistoryItem(addJob.GetHistoryItem(), row);
                _programmaticallyPaused = false;
            }
            JobQueueDGV.Rows[row].Tag = locked ? "Locked" : string.Empty;

            CheckForRunableJob();
        }

        #endregion


        #region Validation


        /// <summary>
        /// Checks row for validity. Mainly used when queuing job to make sure no files have been deleted.
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private bool IsValidJob(int row)
        {
            var allValid = true;
            var hi = (HistoryItem) JobQueueDGV.Rows[row].Cells[0].Tag;

            //only need to check if it is unlocked (Check fires automatically on unlock)
            //Prevents validation from removing visual indication of lock
            if ((string) JobQueueDGV.Rows[row].Tag == "Locked")
                return true;

            // Get all input files and validate that they exist
            foreach (var file in hi.FileList)
            {
                //Input Box
                var path = file.FilePath.Trim("\"".ToCharArray()); //temporary formatting string
                if (!File.Exists(path))
                {
                    allValid = false;
                    JobQueueDGV.Rows[row].Cells[0].Style.BackColor = Color.LightPink;
                    break;
                }
            }

            if (allValid)
                JobQueueDGV.Rows[row].Cells[0].Style.BackColor = Color.White;

            // Validate Output Directory
            if (!hi.OutputDirectory.EndsWith("+") || !Directory.Exists(hi.OutputDirectory.TrimEnd('*')))
                Directory.CreateDirectory(hi.OutputDirectory.TrimEnd('+').TrimEnd('*'));

            // Validate Database Location
            if (File.Exists(hi.ProteinDatabase))
                JobQueueDGV.Rows[row].Cells[2].Style.BackColor = Color.White;
            else
            {
                allValid = false;
                JobQueueDGV.Rows[row].Cells[2].Style.BackColor = Color.LightPink;
            }

            // Validate Spectral Library if needed
            if (hi.JobType != JobType.Library || File.Exists(hi.SpectralLibrary))
                JobQueueDGV.Rows[row].Cells[2].Style.BackColor = Color.White;
            else
            {
                allValid = false;
                JobQueueDGV.Rows[row].Cells[2].Style.BackColor = Color.LightPink;
            }

            if (!allValid)
            {
                var progressCell = (DataGridViewProgressCell) JobQueueDGV[5, LastCompleted + 1];
                JobQueueDGV.Rows[row].Tag = "Invalid";
                progressCell.Message = "Invalid";
            }

            return allValid;
        }

        #endregion


        #region Database Manipulation

        /// <summary>
        /// Returns properties from the given config file (newline seperated, in format "Property = Value")
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        private static string CreatePropertyStringFromConfig(ConfigFile config)
        {
            var tempString = new StringBuilder();
            foreach (var param in config.PropertyList)
                tempString.AppendLine(string.Format("{0} = {1}", param.Name, param.Value));
            return tempString.ToString().Trim();
        }

        /// <summary>
        /// Called from ProgramHandler to indicate to form 
        /// and database that a job has been completed
        /// </summary>
        private void IndicateJobDone(bool runNext, bool jobError)
        {
            if (runNext)
            {
                JobProcess.StartNewJob(LastCompleted + 1, (HistoryItem)JobQueueDGV[0, LastCompleted + 1].Tag);
                return;
            }

            var progressCell = (DataGridViewProgressCell) JobQueueDGV[5, LastCompleted + 1];

            if (jobError)
            {
                UpdateStatusText("Unsuccessful", false);
                progressCell.Value = 0;
                JobQueueDGV.Rows[LastCompleted + 1].Tag = "Unsuccessful";
            }
            else
            {
                UpdateStatusText("Finished", false);
                progressCell.Value = 100;
                JobQueueDGV.Rows[LastCompleted + 1].Tag = "Finished";
            }

            ((HistoryItem) JobQueueDGV[0, LastCompleted + 1].Tag).CurrentStatus =
                jobError ? "Unsuccessful" : "Finished";
            ((HistoryItem) JobQueueDGV[0, LastCompleted + 1].Tag).EndTime = DateTime.Now;
            _session.SaveOrUpdate(JobQueueDGV[0, LastCompleted + 1].Tag);
            _session.Flush();

            LastCompleted++;
            CheckForRunableJob();
        }

        /// <summary>
        /// Called on program close to remove any config files that are not 
        /// used by recorded jobs (if a job has been deleted, edited, etc)
        /// </summary>
        public void RemoveUnusedConfigs()
        {
            //Declaring new session updates mappings
            var sessionFactory = SessionManager.CreateSessionFactory();
            var session = sessionFactory.OpenSession();

            var configFiles = session.QueryOver<ConfigFile>().List();

            foreach (var cf in configFiles)
            {
                if ((cf.UsedByList == null || cf.UsedByList.Count == 0) &&
                    (cf.UsedByList2 == null || cf.UsedByList2.Count == 0) &&
                    cf.FilePath != "Template")
                {
                    session.Delete(cf);
                }
            }
            session.Flush();
        }

        /// <summary>
        /// Saves row numbers to database so datagridview will look the same on reload
        /// </summary>
        public void SaveRowNumbers()
        {
            for (var x = 0; x < JobQueueDGV.Rows.Count - 1; x++)
            {
                ((HistoryItem) JobQueueDGV[0, x].Tag).RowNumber = x;
                _session.SaveOrUpdate(JobQueueDGV[0, x].Tag);
            }
            _session.Flush();
        }

        #endregion


        #region Deletion

        /// <summary>
        /// Checks if job can be deleted
        /// </summary>
        /// <param name="row"></param>
        private void KillCheck(int row)
        {
            _programmaticallyPaused = true;
            if (row != JobQueueDGV.Rows.Count - 1)
            {
                switch (JobQueueDGV.Rows[row].Tag.ToString())
                {
                    case "":
                    case "Locked":
                        DeleteQueued(row);
                        break;
                    case "Running":
                        DeleteRunning(row);
                        break;
                    case "Finished":
                        DeleteFinished(row);
                        break;
                    case "Unsuccessful":
                        DeleteFinished(row);
                        break;
                }
            }


            _programmaticallyPaused = false;
            SaveRowNumbers();
            CheckForRunableJob();
        }

        /// <summary>
        /// Delete a job that has been queued, but has not yet been processed
        /// </summary>
        /// <param name="row"></param>
        private void DeleteQueued(int row)
        {
            _programmaticallyPaused = true;
            if (MessageBox.Show("Are you sure you want to remove this job?",
                                "Delete Job", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                _session.Delete(JobQueueDGV[0, row].Tag);
                _session.Flush();
                JobQueueDGV.Rows.RemoveAt(row);
            }
        }

        /// <summary>
        /// Delete a job that is currently being processed, ending the processing early. 
        ///  Job can then either be requeued or it can be removed from history.
        /// </summary>
        /// <param name="row"></param>
        private void DeleteRunning(int row)
        {
            var hi = (HistoryItem) JobQueueDGV[0, row].Tag;
        	var firstConfigList = hi.InitialConfigFile.PropertyList.Where(x => x.Name == "OutputSuffix").ToList();
            var firstConfig = firstConfigList.Any() ? firstConfigList[0] : null;

            //Remove associated pepXMLs? (yes/no)-> Save job for later? (no)-> [[Remove associated files as well?]]

            switch (MessageBox.Show("Delete associated result files as well?",
                                    "Abort job",
                                    MessageBoxButtons.YesNoCancel))
            {
                case DialogResult.Yes:
                    JobProcess.ForceKill();
                    var trimmedOutput = hi.OutputDirectory.TrimEnd('*');
                    if (hi.TagConfigFile == null)
                    {
                        foreach (var file in hi.FileList)
                        {
                            var fileOnly = Path.GetFileNameWithoutExtension(file.FilePath.Trim('"'));
                            DeleteFile(String.Format(@"{0}\{1}{2}.pepXML", trimmedOutput, fileOnly,
                                                     firstConfig == null ? string.Empty : firstConfig.Value));
                            DeleteFile(String.Format(@"{0}\{1}{2}.mzid", trimmedOutput, fileOnly,
                                                     firstConfig == null ? string.Empty : firstConfig.Value));
                        }
                    }
                    else
                    {
						var secondConfigList = hi.InitialConfigFile.PropertyList.Where(x => x.Name == "OutputSuffix").ToList();
						var secondConfig = secondConfigList.Any() ? secondConfigList[0] : null;
                        foreach (var file in hi.FileList)
                        {
                            var fileOnly = Path.GetFileNameWithoutExtension(file.FilePath.Trim('"'));
                            DeleteFile(String.Format(@"{0}\{1}{2}.tags", trimmedOutput, fileOnly,
                                                     firstConfig == null ? string.Empty : firstConfig.Value));
                            DeleteFile(String.Format(@"{0}\{1}{2}{3}.pepXML", trimmedOutput, fileOnly,
                                                     firstConfig == null ? string.Empty : firstConfig.Value,
                                                     secondConfig == null ? string.Empty : secondConfig.Value));
                            DeleteFile(String.Format(@"{0}\{1}{2}{3}.mzid", trimmedOutput, fileOnly,
                                                     firstConfig == null ? string.Empty : firstConfig.Value,
                                                     secondConfig == null ? string.Empty : secondConfig.Value));
                        }
                    }
                    if (hi.OutputDirectory.EndsWith("*"))
                    {
                        if (Directory.Exists(trimmedOutput))
                        {
                            try
                            {
                                var filesLeft = Directory.GetFileSystemEntries(trimmedOutput);
                                if (filesLeft.Length == 0
                                    || (filesLeft.Length == 1
                                        && filesLeft[0] == "directag_intensity_ranksum_bins.cache"))
                                    Directory.Delete(trimmedOutput);
                            }
                            catch
                            {
                                MessageBox.Show("Directory still in use on delete attempt. The folder " + trimmedOutput + " was not deleted");
                            }
                        }
                    }
                    if (
                        MessageBox.Show("Save job for later?", "Save job information?", MessageBoxButtons.YesNo).
                            Equals(DialogResult.Yes))
                    {
                        if (JobProcess != null && JobQueueDGV.Rows[row].Tag.ToString() == "Finished")
                        {
                            JobProcess.DeletedAbove();
                            LastCompleted--;
                        }
                        JobQueueDGV.Rows[row].Cells[5].Value = 0;
                        JobQueueDGV.Rows[row].Tag = "Locked";
                        ((HistoryItem) JobQueueDGV[0, row].Tag).CurrentStatus = "Locked";
                        var progressCell = (DataGridViewProgressCell)JobQueueDGV[5, row];
                        progressCell.Message = "Locked";
                        progressCell.ProgressBarStyle = ProgressBarStyle.Continuous;
                        _session.SaveOrUpdate(JobQueueDGV[0, row].Tag);
                        _session.Flush();
                        for (var x = 0; x < 6; x++)
                            JobQueueDGV.Rows[row].Cells[x].Style.BackColor = Color.Wheat;
                    }
                    else
                    {
                        if (JobProcess != null && JobQueueDGV.Rows[row].Tag.ToString() == "Finished")
                        {
                            JobProcess.DeletedAbove();
                            LastCompleted--;
                        }
                        _session.Delete(JobQueueDGV[0, row].Tag);
                        _session.Flush();
                        JobQueueDGV.Rows.RemoveAt(row);
                    }
                    break;

                case DialogResult.No:
                    JobProcess.ForceKill();
                    if (
                        MessageBox.Show("Save job for later?", "Save job information?", MessageBoxButtons.YesNo).
                            Equals(DialogResult.Yes))
                    {
                        if (JobProcess != null && JobQueueDGV.Rows[row].Tag.ToString() == "Finished")
                        {
                            JobProcess.DeletedAbove();
                            LastCompleted--;
                        }
                        JobQueueDGV.Rows[row].Cells[5].Value = 0;
                        JobQueueDGV.Rows[row].Tag = "Locked";
                        ((HistoryItem) JobQueueDGV[0, row].Tag).CurrentStatus = "Locked";
                        var progressCell = (DataGridViewProgressCell)JobQueueDGV[5, row];
                        progressCell.Message = "Locked";
                        progressCell.ProgressBarStyle = ProgressBarStyle.Continuous;
                        _session.SaveOrUpdate(JobQueueDGV[0, row].Tag);
                        _session.Flush();
                        for (var x = 0; x < 6; x++)
                            JobQueueDGV.Rows[row].Cells[x].Style.BackColor = Color.Wheat;
                    }
                    else
                    {
                        if (JobProcess != null && JobQueueDGV.Rows[row].Tag.ToString() == "Finished")
                        {
                            JobProcess.DeletedAbove();
                            LastCompleted--;
                        }
                        _session.Delete(JobQueueDGV[0, row].Tag);
                        _session.Flush();
                        JobQueueDGV.Rows.RemoveAt(row);
                    }
                    break;
                case DialogResult.Cancel:
                    break;
            }
        }

        /// <summary>
        /// Tries to delete specified file, informs user if this cannot be done.
        /// </summary>
        /// <param name="fileToDelete"></param>
        private static void DeleteFile(string fileToDelete)
        {
            try
            {
                File.Delete(fileToDelete);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not delete file due to exception: " + ex.Message);
            }
        }

        /// <summary>
        /// Deletes a job that has already been processed and warns ProgramHandler 
        /// that the row index of the job it is working on has changed.
        /// </summary>
        /// <param name="row"></param>
        private void DeleteFinished(int row)
        {
            var hi = (HistoryItem) JobQueueDGV[0, row].Tag;
			var firstConfigList = hi.InitialConfigFile.PropertyList.Where(x => x.Name == "OutputSuffix").ToList();
			var firstConfig = firstConfigList.Any() ? firstConfigList[0] : null;

            //Remove associated pepXMLs? (yes/no)->  [[Remove associated files as well?]]

            switch (MessageBox.Show("Delete associated result files as well?",
                                    "Abort job",
                                    MessageBoxButtons.YesNoCancel))
            {
                case DialogResult.Yes:
                    var trimmedOutput = hi.OutputDirectory.TrimEnd('*');
                    if (hi.TagConfigFile == null)
                    {
                        foreach (var file in hi.FileList)
                        {
                            if (!Directory.Exists(Path.Combine(new FileInfo(file.FilePath.Trim('"')).DirectoryName, trimmedOutput))) continue;
                            var fileOnly = Path.GetFileNameWithoutExtension(file.FilePath.Trim('"'));
                            DeleteFile(String.Format(@"{0}\{1}{2}.pepXML", trimmedOutput, fileOnly,
                                                     firstConfig == null ? string.Empty : firstConfig.Value));
                            DeleteFile(String.Format(@"{0}\{1}{2}.mzid", trimmedOutput, fileOnly,
                                                     firstConfig == null ? string.Empty : firstConfig.Value));
                        }
                    }
                    else
                    {
						var secondConfigList = hi.InitialConfigFile.PropertyList.Where(x => x.Name == "OutputSuffix").ToList();
						var secondConfig = secondConfigList.Any() ? secondConfigList[0] : null;
                        foreach (var file in hi.FileList)
                        {
                            if (!Directory.Exists(Path.Combine(new FileInfo(file.FilePath.Trim('"')).DirectoryName, trimmedOutput))) continue;
                            var fileOnly = Path.GetFileNameWithoutExtension(file.FilePath.Trim('"'));
                            DeleteFile(String.Format(@"{0}\{1}{2}.tags", trimmedOutput, fileOnly,
                                                     firstConfig == null ? string.Empty : firstConfig.Value));
                            DeleteFile(String.Format(@"{0}\{1}{2}{3}.pepXML", trimmedOutput, fileOnly,
                                                     firstConfig == null ? string.Empty : firstConfig.Value,
                                                     secondConfig == null ? string.Empty : secondConfig.Value));
                            DeleteFile(String.Format(@"{0}\{1}{2}{3}.mzid", trimmedOutput, fileOnly,
                                                     firstConfig == null ? string.Empty : firstConfig.Value,
                                                     secondConfig == null ? string.Empty : secondConfig.Value));
                        }
                    }

                    if (hi.OutputDirectory.EndsWith("*"))
                    {
                        if (Directory.Exists(trimmedOutput))
                        {
                            var filesLeft = Directory.GetFileSystemEntries(trimmedOutput);
                            if (filesLeft.Length == 0)
                                Directory.Delete(trimmedOutput);
                            if (filesLeft.Length == 1 
                                && filesLeft[0].EndsWith("directag_intensity_ranksum_bins.cache"))
                            {
                                File.Delete(filesLeft[0]);
                                Directory.Delete(trimmedOutput);
                            }
                                
                        }
                    }

                    //if (JobProcess != null && JobQueueDGV.Rows[row].Tag.ToString() == "Finished")
                    {
                        JobProcess.DeletedAbove();
                        LastCompleted--;
                    }
                    _session.Delete(JobQueueDGV[0, row].Tag);
                    _session.Flush();
                    JobQueueDGV.Rows.RemoveAt(row);
                    break;

                case DialogResult.No:
                    //if (JobProcess != null && JobQueueDGV.Rows[row].Tag.ToString() == "Finished")
                    {
                        JobProcess.DeletedAbove();
                        LastCompleted--;
                    }
                    _session.Delete(JobQueueDGV[0, row].Tag);
                    _session.Flush();
                    JobQueueDGV.Rows.RemoveAt(row);
                    break;
                case DialogResult.Cancel:
                    break;
            }
        }

        #endregion


        #region Utility

        /// <summary>
        /// Adds job based on given HistoryItem
        /// </summary>
        /// <param name="hi"></param>
        /// <param name="row"></param>
        private void InsertRowFromHistoryItem(HistoryItem hi, int row)
        {
            //Fill in job type if needed
            if (hi.JobType == null)
            {
                hi.JobType = hi.TagConfigFile == null ? JobType.Database : JobType.Tag;
                lock (_session)
                {
                    _session.Save(hi);
                    _session.Flush();
                }
            }

            #region Check for duplicate config files
            //Check initial config file
            var oldConfigList = _session.QueryOver<ConfigFile>().
                Where(x => x.FilePath == hi.InitialConfigFile.FilePath && x.DestinationProgram == hi.InitialConfigFile.DestinationProgram).OrderBy(y => y.FirstUsedDate).Asc.List();

            //Hack: initialize used by list to keep it from being deleted
            //on program close (if job has not finished)
            hi.InitialConfigFile.UsedByList = new List<HistoryItem> { hi };

            hi.InitialConfigFile.FirstUsedDate = DateTime.Now;

            foreach (var item in oldConfigList)
            {
                var foundDuplicate = true;

                if (item.PropertyList.Count == hi.InitialConfigFile.PropertyList.Count)
                {
                    for (var oldProperty = 0; oldProperty < item.PropertyList.Count; oldProperty++)
                    {
                        var property = item.PropertyList[oldProperty];
                    	var otherPropertyList = hi.InitialConfigFile.PropertyList.Where(
                            x => (x.Name == property.Name &&
                    		      x.Value == property.Value)).ToList();
                        var otherProperty = otherPropertyList.Any() ? otherPropertyList[0] : null;

                        if (otherProperty == null)
                        {
                            foundDuplicate = false;
                            break;
                        }
                    }
                }
                else
                    foundDuplicate = false;

                if (foundDuplicate)
                {
                    hi.InitialConfigFile = item;
                    break;
                }
            }

            //Check tag config file
            if (hi.TagConfigFile != null)
            {
                oldConfigList = _session.QueryOver<ConfigFile>().
                Where(x => x.FilePath == hi.TagConfigFile.FilePath && x.DestinationProgram == hi.TagConfigFile.DestinationProgram).OrderBy(y => y.FirstUsedDate).Asc.List();

                //Hack: initialize used by list to keep it from being deleted
                //on program close (if job has not finished)
                hi.TagConfigFile.UsedByList = new List<HistoryItem> { hi };

                hi.TagConfigFile.FirstUsedDate = DateTime.Now;

                foreach (var item in oldConfigList)
                {
                    var foundDuplicate = true;

                    if (item.PropertyList.Count == hi.TagConfigFile.PropertyList.Count)
                    {
                        for (var oldProperty = 0; oldProperty < item.PropertyList.Count; oldProperty++)
                        {
                            var property = item.PropertyList[oldProperty];
							var otherPropertyList = hi.InitialConfigFile.PropertyList.Where(
							x => (x.Name == property.Name &&
								  x.Value == property.Value)).ToList();
							var otherProperty = otherPropertyList.Any() ? otherPropertyList[0] : null;
                            if (otherProperty == null)
                            {
                                foundDuplicate = false;
                                break;
                            }
                        }
                    }
                    else
                        foundDuplicate = false;

                    if (foundDuplicate)
                    {
                        hi.TagConfigFile = item;
                        break;
                    }
                }

            }
            #endregion

            var values = new object[6];

            //fill values list with appropriate info
            values[0] = hi.JobName;
            values[1] = (new DirectoryInfo(hi.OutputDirectory.TrimEnd('+').TrimEnd('*'))).Name;

            if (hi.JobType == JobType.Library)
                values[2] = string.Format("{0} / {1}", Path.GetFileName(hi.ProteinDatabase),
                                          Path.GetFileName(hi.SpectralLibrary));
            else
                values[2] = Path.GetFileName(hi.ProteinDatabase);

            if (hi.TagConfigFile != null)
            {
                values[3] = String.Format("{0} / {1}", hi.InitialConfigFile.Name ?? Path.GetFileNameWithoutExtension(hi.InitialConfigFile.FilePath),
                                          hi.TagConfigFile.Name ?? Path.GetFileNameWithoutExtension(hi.TagConfigFile.FilePath));
                values[4] = JobType.Tag;
            }
            else
            {
                values[3] = hi.InitialConfigFile.Name ?? Path.GetFileNameWithoutExtension(hi.InitialConfigFile.FilePath);
                values[4] = hi.JobType;
            }

            if (hi.CurrentStatus == "Finished")
                values[5] = 100;
            else
                values[5] = 0;

            //Add row and set current status
            JobQueueDGV.Rows.Insert(row, values);
            JobQueueDGV.Rows[row].Tag = hi.CurrentStatus;

            //Adjust row appearance if status is special
            if (hi.CurrentStatus == "Locked")
                for (int x = 0; x < 6; x++)
                    JobQueueDGV.Rows[row].Cells[x].Style.BackColor = Color.Wheat;
            else if (hi.CurrentStatus == "Unsuccessful")
                JobQueueDGV[5, row].Style.BackColor = Color.LightPink;

            //Add tooltip to name cell listing file names
            var tempString = string.Empty;
            foreach (var i in hi.FileList)
                tempString += i.FilePath + Environment.NewLine;
            tempString = tempString.Trim();
            JobQueueDGV.Rows[row].Cells[0].ToolTipText = tempString;

            //Hold on to history item for future use
            JobQueueDGV.Rows[row].Cells[0].Tag = hi;

            //Add full output directory to tooltip
            JobQueueDGV.Rows[row].Cells[1].ToolTipText = hi.OutputDirectory;

            //Database File Name
            JobQueueDGV.Rows[row].Cells[2].ToolTipText = hi.ProteinDatabase +
                                                         (hi.SpectralLibrary == null
                                                              ? string.Empty
                                                              : Environment.NewLine + hi.SpectralLibrary);

            //Configs
            bool databaseCustom = hi.InitialConfigFile.FilePath == "--Custom--";
            if (hi.TagConfigFile != null)
            {
                bool tagCustom = hi.TagConfigFile.FilePath == "--Custom--";

                JobQueueDGV.Rows[row].Cells[3].ToolTipText =
                    string.Format("{0}{1}{2}{1}{3}",
                                  databaseCustom ? CreatePropertyStringFromConfig(hi.InitialConfigFile)
                                      : hi.InitialConfigFile.FilePath, Environment.NewLine, new string('-', 20),
                                  tagCustom ? CreatePropertyStringFromConfig(hi.TagConfigFile)
                                      : hi.TagConfigFile.FilePath);
            }
            else
                JobQueueDGV.Rows[row].Cells[3].ToolTipText = databaseCustom
                                                                 ? CreatePropertyStringFromConfig(hi.InitialConfigFile)
                                                                 : hi.InitialConfigFile.FilePath;

            //last button
            JobQueueDGV.Rows[row].Cells[6].Value = "X";

            hi.RowNumber = row;
            _session.SaveOrUpdate(hi.InitialConfigFile);
            if (hi.TagConfigFile != null)
                _session.SaveOrUpdate(hi.TagConfigFile);

            _session.SaveOrUpdate(hi);
            _session.Flush();
        }



        /// <summary>
        /// Attempts to arrange the DataGridView to the next valid job
        /// </summary>
        /// <returns>True if there is a valid job to run</returns>
        private bool CanArrangeToNextJob()
        {
            var currentTop = LastCompleted + 1;

            IsValidJob(currentTop);

            while (JobQueueDGV.Rows[currentTop].Tag.ToString() != string.Empty)
            {
                currentTop++;
                if (currentTop == JobQueueDGV.Rows.Count - 1)
                    break;
                IsValidJob(currentTop);
            }

            if (currentTop > LastCompleted + 1)
            {
                if (currentTop == JobQueueDGV.Rows.Count - 1)
                    return false;
                var nextValidJob = JobQueueDGV.Rows[currentTop];
                JobQueueDGV.Rows.RemoveAt(currentTop);
                JobQueueDGV.Rows.Insert(LastCompleted + 1, nextValidJob);
            }

            SaveRowNumbers();
            return true;
        }

        /// <summary>
        /// Checks multiple factors to determine if a new job can be sent to the ProgramHandler
        /// </summary>
        private void CheckForRunableJob()
        {
            if (_programmaticallyPaused || _manuallyPaused || JobProcess == null || JobProcess.JobIsRunning() ||
                LastCompleted >= JobQueueDGV.Rows.Count - 2 || !CanArrangeToNextJob())
                return;
            
            var hi = (HistoryItem) JobQueueDGV[0, LastCompleted + 1].Tag;
            //reload config to make sure current state is recorded
            ReloadConfig(hi.InitialConfigFile);
            ReloadConfigTooltip(LastCompleted + 1);             

            //if job is set to add new folder, adjust outputFolder accordingly
            if (hi.OutputDirectory.EndsWith("+"))
            {
                var baseOutputDirectory =
                    hi.OutputDirectory.TrimEnd('+');
                var baseNewFolder = hi.JobName;
                var newFolderPath = Path.Combine(baseOutputDirectory, baseNewFolder);

                if (Directory.Exists(newFolderPath))
                {
                    var x = 2;
                    while (Directory.Exists(newFolderPath + x))
                        x++;
                    newFolderPath += x;
                    baseNewFolder += x;
                }
                    
                Directory.CreateDirectory(newFolderPath);
                newFolderPath += "*";
                JobQueueDGV[1, LastCompleted + 1].Value = baseNewFolder;
                JobQueueDGV[1, LastCompleted + 1].ToolTipText = newFolderPath;
                hi.OutputDirectory = newFolderPath;
                _session.SaveOrUpdate(JobQueueDGV[0, LastCompleted + 1].Tag);
                _session.Flush();
            }

            hi.StartTime = DateTime.Now;
            _session.SaveOrUpdate(JobQueueDGV[0, LastCompleted + 1].Tag);
            _session.Flush();
            JobQueueDGV.Rows[LastCompleted + 1].Tag = "Running";
            JobQueueDGV.Rows[LastCompleted + 1].Cells[5].Tag = string.Empty;
            JobProcess.StartNewJob(LastCompleted + 1, hi);
        }

        /// <summary>
        /// Reload config tooltip in case a config file was deleted
        /// </summary>
        /// <param name="row"></param>
        private void ReloadConfigTooltip(int row)
        {
            var hi = (HistoryItem) JobQueueDGV[0, row].Tag;
            var newToolTip = new StringBuilder();

            if (hi.InitialConfigFile.FilePath == "--Custom--")
                foreach (var property in hi.InitialConfigFile.PropertyList)
                    newToolTip.AppendLine(property.Name + " = " + property.Value);
            else
                newToolTip.AppendLine(hi.InitialConfigFile.FilePath);
            if (hi.TagConfigFile != null)
            {
                newToolTip.AppendLine(Environment.NewLine + new string('-', 20) + Environment.NewLine);
                if (hi.TagConfigFile.FilePath == "--Custom--")
                    foreach (var property in hi.TagConfigFile.PropertyList)
                        newToolTip.AppendLine(property.Name + " = " + property.Value);
            }

            JobQueueDGV[3, row].ToolTipText = newToolTip.ToString().Trim();
        }

        /// <summary>
        /// Reloads database representation of a valid config file
        /// </summary>
        /// <param name="config"></param>
        private void ReloadConfig(ConfigFile config)
        {
            if (Path.GetExtension(config.FilePath) == ".cfg")
            {
                if (File.Exists(config.FilePath))
                {
                    var parameterType = Util.parameterTypes;

                    var fileIn = new StreamReader(config.FilePath);
                    var completeFile = fileIn.ReadToEnd();
                    fileIn.Close();
                    fileIn.Dispose();
                    var propertyList = completeFile.Split(Environment.NewLine.ToCharArray(),
                                                          StringSplitOptions.RemoveEmptyEntries);

                    config.PropertyList.Clear();
                    foreach (var line in propertyList)
                    {
                        var breakDown = line.Split("=".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        config.PropertyList.Add(new ConfigProperty
                                                    {
                                                        Name = breakDown[0].Trim(),
                                                        Value = breakDown[1].Trim(),
                                                        Type = parameterType.ContainsKey(breakDown[0].Trim())
                                                                             ? parameterType[breakDown[0].Trim()]
                                                                             : "unknown",
                                                        ConfigAssociation = config                                                        
                                                    });
                    }
                    _session.SaveOrUpdate(config);
                    _session.Flush();
                }
                else
                {
                    //Occurs when the file has been deleted, convert to custom config
                    config.Name = "File missing (" + config.FilePath + ")";
                    config.FilePath = "--Custom--";
                    _session.SaveOrUpdate(config);
                    _session.Flush();
                }
            }
        }

        /// <summary>
        /// Sets the rowStatusLabel text to reflect the state of the currently selected row
        /// </summary>
        internal void UpdateStatusText(string status, bool marqueeMode)
        {
            var progressCell = (DataGridViewProgressCell)JobQueueDGV[5, LastCompleted + 1];
            var editedStatus = status.Replace("<<JobName>>", ((HistoryItem)JobQueueDGV[0, LastCompleted + 1].Tag).JobName);
            progressCell.Message = editedStatus;
            progressCell.ProgressBarStyle = marqueeMode ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
        }

        private void SetPercentage(int value)
        {
            var progressCell = (DataGridViewProgressCell)JobQueueDGV[5, LastCompleted + 1];
            TrayIcon.Text = string.Format("BumberDash - {0} ({1}%)", JobQueueDGV[0, LastCompleted + 1].Value, value);
            progressCell.Value = value;
        }

        /// <summary>
        /// Allows ProgramHandler to append lines to the log
        /// </summary>
        /// <param name="line"></param>
        internal void AddLogLine(string line)
        {
            var jobName = JobQueueDGV[0, LastCompleted + 1].Tag != null
                              ? ((HistoryItem) JobQueueDGV[0, LastCompleted + 1].Tag).JobName
                              : string.Empty;
            var editedLine = line.Replace("<<JobName>>", jobName);
            _jobLog.logText.AppendText(Environment.NewLine + editedLine);
            var lineList = _jobLog.logText.Text.Split(Environment.NewLine.ToCharArray(),StringSplitOptions.RemoveEmptyEntries);
            var buildingString = string.Empty;
            if (lineList.Count() > 5)
            {
                for (var x = lineList.Count() - 5; x < lineList.Count(); x++)
                    buildingString += lineList[x] + Environment.NewLine;
                MiniLogBox.Text =
                    buildingString.Trim();
            }
        }

        private void ShowError(string status)
        {
            MessageBox.Show(status);
            AddLogLine(status);
        }

        private bool DetectLatestIDPicker()
        {
            var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Bumbershoot");
            if (Directory.Exists(basePath))
            {
                var DI = new DirectoryInfo(basePath);
                var dirList = DI.GetDirectories("IDPicker*").OrderByDescending(x => x.Name).ToList();
                if (!dirList.Any())
                    return false;
                if (File.Exists(Path.Combine(dirList[0].FullName, "IDPicker.exe")))
                {
                    Properties.Settings.Default.IDPickerLocation = Path.Combine(dirList[0].FullName, "IDPicker.exe");
                    Properties.Settings.Default.Save();
                    return true;
                }
                if (File.Exists(Path.Combine(dirList[0].FullName, "IdPickerGui.exe")))
                {
                    Properties.Settings.Default.IDPickerLocation = Path.Combine(dirList[0].FullName, "IdPickerGui.exe");
                    Properties.Settings.Default.Save();
                    return true;
                }
            }
            return false;
        }

        #endregion

        private void resetIDPickerLocationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to reset the IDPicker Location?", "Reset IdPicker Locantion", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                if (DetectLatestIDPicker())
                    iDPickerToolStripMenuItem.Visible = true;
                else
                    MessageBox.Show("Unable to find current version of IDPicker");
            }
        }

        private void JobQueueDGV_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == JobQueueDGV.Rows.Count - 1)
                ShowAddBox();
        }

        private void JobQueueDGV_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            DataGridViewCell cell = JobQueueDGV.Rows[e.RowIndex].Cells[1];

            if (e.RowIndex != JobQueueDGV.Rows.Count - 1)
            {
                try
                {
                    System.Diagnostics.Process.Start(cell.ToolTipText.TrimEnd('+').TrimEnd('*'));
                }
                catch
                {
                    MessageBox.Show("Cannot open folder");
                }
            }

            CheckForRunableJob();
        }
    }
}
