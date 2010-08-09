using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DataMapping;
using DataAccess;

namespace BumberDash
{
    public partial class QueueForm : Form
    {
        #region Globals

        private string _configLocation = String.Format(@"{0}\ConfigEditor.exe", Application.StartupPath);

        private Rectangle dragBoxFromMouseDown; //Created on valid mousedown
        internal ProgramHandler _jobProcess; //eventual implememtation of program running
        private AddJobForm _addJob; //Popup form for adding or editing jobs
        private LogForm _jobLog;
        private int rowIndexFromMouseDown; //holds clicked row
        private int rowIndexOfItemUnderMouseToDrop = -1; //Changes as row is dragged around
        private bool _editMode = false; //True if editing at all
        private bool _lockEdit = false; //true if editing a locked row (prevents unlock on edit end)
        internal int _lastCompleted = -1; //keeps track of where the scanner is in the list
        private bool programmaticallyPaused = false; //indicates if a job can run
        private bool manuallyPaused = false; //indicates if a job can run
        internal SessionManager _manager;

        #endregion
        
        /// <summary>
        /// Main form that the Bumbershoot GUI runs from
        /// </summary>
        public QueueForm()
        {
            InitializeComponent();
            _manager = new SessionManager();
        }

        /// <summary>
        /// Initialize the DataGridView with previous jobs and add row at bottom for easy job queuing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void QueueForm_Load(object sender, EventArgs e)
        {
            _jobLog = new LogForm();
            DatabaseObjects dbo = new DatabaseObjects(_manager.GetSession());

            NHibernate.Cfg.Configuration cfg = new NHibernate.Cfg.Configuration();
            cfg.Configure();
            NHibernate.Tool.hbm2ddl.SchemaUpdate schema = new NHibernate.Tool.hbm2ddl.SchemaUpdate(cfg);
            schema.Execute(false, true);

            IList<HistoryItem> historyItemList;
            string tempString;

            //Initialize program handler
            _jobProcess = new ProgramHandler(this);

            //Object to hold cell values
            object[] Values = new object[6];

            //Load all jobs from database
            historyItemList = dbo.GetHistoryItemList();
            foreach (HistoryItem hi in historyItemList)
            {
                Values[0] = hi.JobName;
                Values[1] = hi.OutputDirectory;
                Values[2] = hi.ProteinDatabase;
                if (hi is TagHistoryItem)
                {
                    Values[3] = String.Format("{0}{1}{2}", hi.InitialConfigFile.FilePath, System.Environment.NewLine, ((TagHistoryItem)hi).TagConfigFile.FilePath);
                    Values[4] = "Sequence Tagging";
                }
                else
                {
                    Values[3] = hi.InitialConfigFile.FilePath;
                    Values[4] = "Database Search";
                }
                if (hi.CurrentStatus == "Finished")
                    Values[5] = 100;
                else
                    Values[5] = 0;

                tempString = string.Empty;

                JobQueueDGV.Rows.Add(Values);
                JobQueueDGV.Rows[JobQueueDGV.Rows.Count - 1].Tag = hi.CurrentStatus;

                if (hi.CurrentStatus == "Locked")
                    for (int x = 0; x < 6; x++)
                        JobQueueDGV.Rows[JobQueueDGV.Rows.Count - 1].Cells[x].Style.BackColor = Color.Wheat;
                else if (hi.CurrentStatus == "Unsuccessful")
                    JobQueueDGV[5, JobQueueDGV.Rows.Count - 1].Style.BackColor = Color.LightPink;

                foreach (InputFile i in hi.FileList)
                    tempString += i.FilePath + System.Environment.NewLine;
                tempString = tempString.Trim();
                JobQueueDGV.Rows[JobQueueDGV.Rows.Count - 1].Cells[0].ToolTipText = tempString;
                JobQueueDGV.Rows[JobQueueDGV.Rows.Count - 1].Cells[0].Tag = hi.HistoryItemID;
                SimplifyAppearance(JobQueueDGV.Rows.Count - 1);
                JobQueueDGV.Rows[JobQueueDGV.Rows.Count - 1].Cells[6].Value = "X";
                JobQueueDGV.Rows[JobQueueDGV.Rows.Count - 1].Cells[6].Tag = hi.CPUs;
            }


            //Add line at end for quick job creation
            Values[0] = "Click to add new job";
            Values[1] = string.Empty;
            Values[2] = string.Empty;
            Values[3] = string.Empty;
            Values[4] = string.Empty;
            Values[5] = 0;
            JobQueueDGV.Rows.Add(Values);
            JobQueueDGV.Rows[JobQueueDGV.Rows.Count - 1].DefaultCellStyle.BackColor = Color.LightGray;

            for (int x = JobQueueDGV.Rows.Count-2; x >= 0; x--)
            {
                if ((string)JobQueueDGV.Rows[x].Tag == "Finished" || (string)JobQueueDGV.Rows[x].Tag == "Unsuccessful")
                {
                    _lastCompleted = x;
                    break;
                }
            }

            //Configure IDPicker Location
            if (!File.Exists(Properties.Settings.Default.IDPickerLocation))
                iDPickerToolStripMenuItem.Visible = false;


        }


        #region JobQueue DataGridView Events

        /// <summary>
        /// Starts dargging DataGridView row if the mouse button is held down
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
           private void JobQueueDGV_MouseMove(object sender, MouseEventArgs e)
            {
                if ((e.Button & MouseButtons.Left) == MouseButtons.Left)
                {
                    // If the mouse moves outside the rectangle, start the drag.
                    if (dragBoxFromMouseDown != Rectangle.Empty &&
                    !dragBoxFromMouseDown.Contains(e.X, e.Y))
                    {
                        // Proceed with the drag and drop, passing in the list item.                    
                        DragDropEffects dropEffect = JobQueueDGV.DoDragDrop(
                        JobQueueDGV.Rows[rowIndexFromMouseDown],
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
                rowIndexFromMouseDown = JobQueueDGV.HitTest(e.X, e.Y).RowIndex;

                if (rowIndexFromMouseDown != -1)
                {
                    programmaticallyPaused = true;

                    if (rowIndexFromMouseDown != JobQueueDGV.Rows.Count - 1 && e.Button == MouseButtons.Right)
                    {
                        //guarantee right clicked cell is selected
                        if (JobQueueDGV.CurrentCell == JobQueueDGV.Rows[rowIndexFromMouseDown].Cells[0])
                            JobQueueDGV.CurrentCell = JobQueueDGV.Rows[rowIndexFromMouseDown].Cells[1];
                        else
                            JobQueueDGV.CurrentCell = JobQueueDGV.Rows[rowIndexFromMouseDown].Cells[0];
                        
                        //check for lock status
                        if ((string)JobQueueDGV.Rows[rowIndexFromMouseDown].Tag == "Locked")
                            lockToolStripMenuItem.Text = "Unlock";
                        else
                            lockToolStripMenuItem.Text = "Lock";

                        //show context menu
                        JQRowMenu.Show(JobQueueDGV, new Point(e.X, e.Y));
                    }
                    else if (rowIndexFromMouseDown != JobQueueDGV.Rows.Count - 1 &&  rowIndexFromMouseDown > _lastCompleted+1)
                    {
                        // Remember the point where the mouse down occurred. 
                        // The DragSize indicates the size that the mouse can move 
                        // before a drag event should be started.                
                        Size dragSize = SystemInformation.DragSize;
                        // Create a rectangle using the DragSize, with the mouse position being
                        // at the center of the rectangle.
                        dragBoxFromMouseDown = new Rectangle(new Point(e.X - (dragSize.Width / 2),
                        e.Y - (dragSize.Height / 2)),
                        dragSize);
                    }
                    else
                        // Reset the rectangle if the mouse is not over an item in the ListBox.
                        dragBoxFromMouseDown = Rectangle.Empty;
                }
                else
                    // Reset the rectangle if the mouse is not over an item in the ListBox.
                    dragBoxFromMouseDown = Rectangle.Empty;
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
                

                if (rowIndexOfItemUnderMouseToDrop != overIndex)
                {
                    rowIndexOfItemUnderMouseToDrop = overIndex;
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
                if (rowIndexOfItemUnderMouseToDrop != -1 && (rowIndexOfItemUnderMouseToDrop > _lastCompleted + 1 || (rowIndexOfItemUnderMouseToDrop == _lastCompleted + 1 && !_jobProcess.JobIsRunning())))
                {
                    // The mouse locations are relative to the screen, so they must be 
                    // converted to client coordinates.
                    Point clientPoint = JobQueueDGV.PointToClient(new Point(e.X, e.Y));
                    // Get the row index of the item the mouse is below. 
                    rowIndexOfItemUnderMouseToDrop =
                        JobQueueDGV.HitTest(clientPoint.X, clientPoint.Y).RowIndex;

                    // If the drag operation was a move then remove and insert the row.
                    if (
                        e.Effect == DragDropEffects.Move &&
                        rowIndexOfItemUnderMouseToDrop != rowIndexFromMouseDown &&
                        rowIndexOfItemUnderMouseToDrop != rowIndexFromMouseDown + 1
                        )
                    {
                        DataGridViewRow rowToMove = e.Data.GetData(
                            typeof(DataGridViewRow)) as DataGridViewRow;

                        JobQueueDGV.Rows.RemoveAt(rowIndexFromMouseDown);

                        if (rowIndexOfItemUnderMouseToDrop < rowIndexFromMouseDown)
                        {
                            JobQueueDGV.Rows.Insert(rowIndexOfItemUnderMouseToDrop, rowToMove);
                            JobQueueDGV.CurrentCell = JobQueueDGV.Rows[rowIndexOfItemUnderMouseToDrop].Cells[0];
                        }
                        else
                        {
                            JobQueueDGV.Rows.Insert(rowIndexOfItemUnderMouseToDrop - 1, rowToMove);
                            JobQueueDGV.CurrentCell = JobQueueDGV.Rows[rowIndexOfItemUnderMouseToDrop - 1].Cells[0];
                        }
                    }
                }
                if (programmaticallyPaused)
                    programmaticallyPaused = false;
                rowIndexOfItemUnderMouseToDrop = -1;
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
                    if (e.RowIndex != -1 && e.RowIndex == rowIndexOfItemUnderMouseToDrop && (rowIndexOfItemUnderMouseToDrop > _lastCompleted + 1 || (rowIndexOfItemUnderMouseToDrop == _lastCompleted + 1 && !_jobProcess.JobIsRunning())))
                    {
                        Pen p = new Pen(Color.Black, 2);
                        e.Graphics.DrawLine(p, e.CellBounds.Left, e.CellBounds.Top - 1, e.CellBounds.Right, e.CellBounds.Top - 1);
                    }
                }
            }

        /// <summary>
        /// Bring up AddJobForm if user has clicked the "Add Job" row
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
           private void JobQueueDGV_Click(object sender, EventArgs e)
            {
                if (JobQueueDGV.SelectedRows[0].Index == JobQueueDGV.Rows.Count - 1)
                {
                    if (_addJob == null || _addJob.IsDisposed)
                    {
                        _addJob = new AddJobForm(this);
                        _addJob.Show();
                        PopulateAddJobLists();
                    }
                    else if (_addJob.CanFocus)
                    {
                        _addJob.Text = "Add Job";
                        _addJob.Select();
                    }

                    _addJob.AddJobRunButton.Text = "Run";

                    if (JobQueueDGV.Rows.Count > 1)
                        JobQueueDGV.CurrentCell = JobQueueDGV.Rows[JobQueueDGV.Rows.Count - 2].Cells[0];

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
               if (programmaticallyPaused && JQRowMenu.Visible == false)
               {
                   programmaticallyPaused = false;
                   CheckForRunableJob();
               }
           }

        /// <summary>
        /// Change displayed text when a different row has been selected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
           private void JobQueueDGV_SelectionChanged(object sender, EventArgs e)
           {
               UpdateStatusText();
           }

        /// <summary>
        /// Open output older if a job is double clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
           private void JobQueueDGV_DoubleClick(object sender, EventArgs e)
           {
               DataGridViewCell cell = JobQueueDGV.Rows[JobQueueDGV.SelectedRows[0].Index].Cells[1];

               if (JobQueueDGV.SelectedRows[0].Index != JobQueueDGV.Rows.Count - 1)
               {
                   try
                   {
                       System.Diagnostics.Process.Start(cell.ToolTipText);
                   }
                   catch
                   {
                       MessageBox.Show("Cannot open folder");
                   }
               }

               CheckForRunableJob();
           }
     
        /// <summary>
        /// Handle kill button being clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
           private void JobQueueDGV_CellContentClick(object sender, DataGridViewCellEventArgs e)
           {
               if (e.ColumnIndex == 6 && e.RowIndex != JobQueueDGV.Rows.Count-1)
                   KillCheck(e.RowIndex);
           }

        #endregion


        #region QueueForm <-> AddJobForm Functions

        /// <summary>
        /// Mark row for editing and show an AddJobForm specified for editing
        /// </summary>
        /// <param name="row"></param>
           private void ShowEditBox(int row)
           {
               int percentageValue = ParsePercentage(JobQueueDGV.Rows[row].Cells[5].Value);

               if (!_editMode && row != JobQueueDGV.Rows.Count-1 && (row > _lastCompleted + 1 || (row == _lastCompleted + 1 && !_jobProcess.JobIsRunning())))
               {
                   _editMode = true;
                   if ((string)JobQueueDGV.Rows[row].Tag == "Locked")
                       _lockEdit = true;
                   JobQueueDGV.Rows[row].Tag = "Editing";

                   if (row < JobQueueDGV.Rows.Count - 1)
                   {
                       if (_addJob == null || _addJob.IsDisposed)
                       {
                           _addJob = new AddJobForm(this);
                           _addJob.Show();
                           PopulateAddJobLists();
                       }
                       else if (_addJob.CanFocus)
                       {
                           _addJob.Select();
                       }
                       _addJob.Text = "Edit Job";
                       _addJob.NameBox.Text = JobQueueDGV.Rows[row].Cells[0].Value.ToString();
                       _addJob.CPUsBox.Value = decimal.Parse(JobQueueDGV.Rows[row].Cells[6].Tag.ToString());
                       _addJob.InputFilesBox.Text = JobQueueDGV.Rows[row].Cells[0].ToolTipText;
                       _addJob.OutputDirectoryBox.Text = JobQueueDGV.Rows[row].Cells[1].ToolTipText;
                       _addJob.DatabaseLocBox.Text = JobQueueDGV.Rows[row].Cells[2].ToolTipText;
                       if (JobQueueDGV.Rows[row].Cells[4].Value.ToString() == "Database Search")
                       {
                           _addJob.MyriConfigBox.Text = JobQueueDGV.Rows[row].Cells[3].ToolTipText;
                           _addJob.DatabaseRadio.Checked = true;
                       }
                       else
                       {
                           string[] configFiles = JobQueueDGV.Rows[row].Cells[3].ToolTipText.Split(System.Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                           _addJob.DTConfigBox.Text = configFiles[0];
                           _addJob.TRConfigBox.Text = configFiles[1];
                           _addJob.TagRadio.Checked = true;
                       }
                       _addJob.AddJobRunButton.Text = "Save";
                   }

                   CheckForRunableJob();
               }
           }

        /// <summary>
        /// Fill dropdown lists in AddjobForm with previously used options
        /// </summary>
           private void PopulateAddJobLists()
           {
               DatabaseObjects dbo = new DatabaseObjects(_manager.GetSession());
               Dictionary<string, string> usedInputList = new Dictionary<string, string>();
               Dictionary<string, string> usedOutputList = new Dictionary<string, string>();
               Dictionary<string, string> usedDatabaseList = new Dictionary<string, string>();
               Dictionary<string, string> usedMyriList = new Dictionary<string, string>();
               Dictionary<string, string> usedDTList = new Dictionary<string, string>();
               Dictionary<string, string> usedTRList = new Dictionary<string, string>();
               IList<HistoryItem> hiList;

               hiList = dbo.GetHistoryItemList();
               hiList.Reverse();

               foreach (HistoryItem hi in hiList)
               {
                   foreach (InputFile i in hi.FileList)
                   {
                       if (!usedInputList.ContainsKey(i.FilePath))
                       {
                           usedInputList.Add(i.FilePath, null);
                           _addJob.InputFilesBox.Items.Add(i.FilePath);
                       }
                   }

                   if (!usedOutputList.ContainsKey(hi.OutputDirectory))
                   {
                       usedOutputList.Add(hi.OutputDirectory, null);
                       _addJob.OutputDirectoryBox.Items.Add(hi.OutputDirectory);
                   }

                   if (!usedDatabaseList.ContainsKey(hi.ProteinDatabase))
                   {
                       usedDatabaseList.Add(hi.ProteinDatabase, null);
                       _addJob.DatabaseLocBox.Items.Add(hi.ProteinDatabase);
                   }

                   if (hi is TagHistoryItem)
                   {
                       if (!usedDTList.ContainsKey(hi.InitialConfigFile.FilePath))
                       {
                           usedDTList.Add(hi.InitialConfigFile.FilePath, null);
                           _addJob.DTConfigBox.Items.Add(hi.InitialConfigFile.FilePath);
                       }

                       if (!usedTRList.ContainsKey(((TagHistoryItem)hi).TagConfigFile.FilePath))
                       {
                           usedTRList.Add(((TagHistoryItem)hi).TagConfigFile.FilePath, null);
                           _addJob.TRConfigBox.Items.Add(((TagHistoryItem)hi).TagConfigFile.FilePath);
                       }
                   }
                   else
                   {
                       if (!usedMyriList.ContainsKey(hi.InitialConfigFile.FilePath))
                       {
                           usedMyriList.Add(hi.InitialConfigFile.FilePath, null);
                           _addJob.MyriConfigBox.Items.Add(hi.InitialConfigFile.FilePath);
                       }
                   }

               }
           }

        /// <summary>
        /// Return editing row to normal state
        /// </summary>
           internal void CancelEdit()
           {
               if (_editMode)
               {
                   int editIndex = _lastCompleted + 1;

                   while (editIndex < JobQueueDGV.Rows.Count - 1)
                   {
                       if ((string)JobQueueDGV.Rows[editIndex].Tag == "Editing")
                           break;
                       editIndex++;
                   }
                   if ((string)JobQueueDGV.Rows[editIndex].Tag == "Editing")
                   {
                       if (_lockEdit)
                       {
                           JobQueueDGV.Rows[editIndex].Tag = "Locked";
                           _lockEdit = false;
                       }
                       else
                           JobQueueDGV.Rows[editIndex].Tag = String.Empty;
                       IsValidJob(editIndex);
                   }
                   
                   _editMode = false;
               }
           }

        /// <summary>
        /// Handle information sent from AddJobForm
        /// </summary>
           internal void QueueJobFromForm()
           {
               if (IsValidJob(-1))
               {
                   #region If this was an edit job box
                   if (_editMode)
                   {
                       //retirieve the job that was being edited
                       int editIndex = _lastCompleted + 1;
                       programmaticallyPaused = true;
                       while (editIndex < JobQueueDGV.Rows.Count - 1)
                       {
                           if ((string)JobQueueDGV.Rows[editIndex].Tag == "Editing")
                               break;
                           editIndex++;
                       }

                       //see if search was successful
                       if ((string)JobQueueDGV.Rows[editIndex].Tag == "Editing")
                       {

                           //go through and update all values
                           DirectoryInfo DI = new DirectoryInfo(_addJob.OutputDirectoryBox.Text);

                           if (_addJob.NameBox.Text.Length > 0)
                               JobQueueDGV.Rows[editIndex].Cells[0].Value = _addJob.NameBox.Text;
                           else
                           {
                               JobQueueDGV.Rows[editIndex].Cells[0].Value = DI.Name;
                               _addJob.NameBox.Text = DI.Name;
                           }
                           JobQueueDGV.Rows[editIndex].Cells[1].Value = _addJob.OutputDirectoryBox.Text;
                           JobQueueDGV.Rows[editIndex].Cells[2].Value = _addJob.DatabaseLocBox.Text;
                           if (_addJob.DatabaseRadio.Checked)
                           {
                               JobQueueDGV.Rows[editIndex].Cells[3].Value = _addJob.MyriConfigBox.Text;
                               JobQueueDGV.Rows[editIndex].Cells[4].Value = "Database Search";

                               //database edit for Myrimatch job
                               JobQueueDGV.Rows[editIndex].Cells[0].Tag = EditOldJob(
                                   _addJob.NameBox.Text, _addJob.InputFilesBox.Text,
                                   _addJob.OutputDirectoryBox.Text, _addJob.DatabaseLocBox.Text,
                                   _addJob.MyriConfigBox.Text, null, int.Parse(JobQueueDGV.Rows[editIndex].Cells[0].Tag.ToString()),
                                   (int)_addJob.CPUsBox.Value);
                           }
                           else
                           {
                               JobQueueDGV.Rows[editIndex].Cells[3].Value = String.Format("{0}{1}{2}", _addJob.DTConfigBox.Text, System.Environment.NewLine, _addJob.TRConfigBox.Text);
                               JobQueueDGV.Rows[editIndex].Cells[4].Value = "Sequence Tagging";

                               //database edit for tag search
                               JobQueueDGV.Rows[editIndex].Cells[0].Tag = EditOldJob(
                                   _addJob.NameBox.Text, _addJob.InputFilesBox.Text,
                                   _addJob.OutputDirectoryBox.Text, _addJob.DatabaseLocBox.Text,
                                   _addJob.DTConfigBox.Text, _addJob.TRConfigBox.Text, int.Parse(JobQueueDGV.Rows[editIndex].Cells[0].Tag.ToString()),
                                   (int)_addJob.CPUsBox.Value);
                           }

                           JobQueueDGV.Rows[editIndex].Cells[0].ToolTipText = _addJob.InputFilesBox.Text;
                           JobQueueDGV.Rows[editIndex].Cells[6].Tag = _addJob.CPUsBox.Value.ToString();

                           //if cell was locked before edit started, return it to locked state
                           if (_lockEdit)
                           {
                               JobQueueDGV.Rows[editIndex].Tag = "Locked";
                               _lockEdit = false;
                           }
                           else
                               JobQueueDGV.Rows[editIndex].Tag = string.Empty;

                           _addJob.Close();
                           SimplifyAppearance(editIndex);
                           _editMode = false;

                       }
                       else
                       {
                           _editMode = false;
                           if (MessageBox.Show("Could not find original job. Save as new job?", "Edit error", MessageBoxButtons.YesNo).Equals(DialogResult.Yes))
                               QueueJobFromForm();
                       }

                       programmaticallyPaused = false;
                   }
                   #endregion

                   #region If this was an add job box
                   else
                   {
                       int jobIndex;
                       object[] newRow = new object[6];
                       DirectoryInfo DI = new DirectoryInfo(_addJob.OutputDirectoryBox.Text);

                       if (_addJob.NameBox.Text.Length > 0)
                           newRow[0] = _addJob.NameBox.Text;
                       else
                       {
                           newRow[0] = DI.Name;
                           _addJob.NameBox.Text = DI.Name;
                       }
                       newRow[1] = _addJob.OutputDirectoryBox.Text;
                       newRow[2] = _addJob.DatabaseLocBox.Text;
                       if (_addJob.DatabaseRadio.Checked)
                       {
                           newRow[3] = _addJob.MyriConfigBox.Text;
                           newRow[4] = "Database Search";
                           newRow[5] = 0;

                           jobIndex = SaveNewJob(_addJob.NameBox.Text, _addJob.InputFilesBox.Text,
                               _addJob.OutputDirectoryBox.Text, _addJob.DatabaseLocBox.Text,
                               _addJob.MyriConfigBox.Text, null,(int)_addJob.CPUsBox.Value);
                       }
                       else
                       {
                           newRow[3] = String.Format("{0}{1}{2}", _addJob.DTConfigBox.Text, System.Environment.NewLine, _addJob.TRConfigBox.Text);
                           newRow[4] = "Sequence Tagging";
                           newRow[5] = 0;

                           jobIndex = SaveNewJob(_addJob.NameBox.Text, _addJob.InputFilesBox.Text,
                               _addJob.OutputDirectoryBox.Text, _addJob.DatabaseLocBox.Text,
                               _addJob.DTConfigBox.Text, _addJob.TRConfigBox.Text, (int)_addJob.CPUsBox.Value);
                       }

                       JobQueueDGV.Rows.Insert(JobQueueDGV.Rows.Count - 1, newRow);
                       JobQueueDGV.Rows[JobQueueDGV.Rows.Count - 2].Tag = string.Empty;
                       JobQueueDGV.Rows[JobQueueDGV.Rows.Count - 2].Cells[0].ToolTipText = _addJob.InputFilesBox.Text;
                       JobQueueDGV.Rows[JobQueueDGV.Rows.Count - 2].Cells[0].Tag = jobIndex;
                       SimplifyAppearance(JobQueueDGV.Rows.Count - 2);
                       JobQueueDGV.Rows[JobQueueDGV.Rows.Count - 2].Cells[6].Value = "X";
                       JobQueueDGV.Rows[JobQueueDGV.Rows.Count - 2].Cells[6].Tag = _addJob.CPUsBox.Value.ToString();

                       _addJob.Close();
                   }
                   #endregion

                   CheckForRunableJob();
                   SaveRowNumbers();
               }
           }

        /// <summary>
        /// Place full values in tooltip text and display sinplified values to user
        /// </summary>
        /// <param name="editIndex"></param>
           private void SimplifyAppearance(int editIndex)
           {
               for (int y = 1; y < 3; y++)
               {
                   JobQueueDGV.Rows[editIndex].Cells[y].ToolTipText = JobQueueDGV.Rows[editIndex].Cells[y].Value.ToString();
                   JobQueueDGV.Rows[editIndex].Cells[y].Value = (new DirectoryInfo(JobQueueDGV.Rows[editIndex].Cells[y].Value.ToString())).Name;
               }

               if (JobQueueDGV.Rows[editIndex].Cells[4].Value.ToString() == "Database Search")
               {
                   JobQueueDGV.Rows[editIndex].Cells[3].ToolTipText = JobQueueDGV.Rows[editIndex].Cells[3].Value.ToString();
                   JobQueueDGV.Rows[editIndex].Cells[3].Value = (new DirectoryInfo(JobQueueDGV.Rows[editIndex].Cells[3].Value.ToString())).Name;
               }
               else
               {
                   JobQueueDGV.Rows[editIndex].Cells[3].ToolTipText = JobQueueDGV.Rows[editIndex].Cells[3].Value.ToString();
                   string[] configFiles = JobQueueDGV.Rows[editIndex].Cells[3].Value.ToString().Split(System.Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                   JobQueueDGV.Rows[editIndex].Cells[3].Value = String.Format("\"{0}\" -> \"{1}\"", (new DirectoryInfo(configFiles[0])).Name, (new DirectoryInfo(configFiles[1])).Name);
               }
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
                    System.Diagnostics.Process.Start(cell.ToolTipText);
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
                try
                {
                    if (_addJob == null || _addJob.IsDisposed)
                    {
                        _addJob = new AddJobForm(this);
                        _addJob.Show();
                        PopulateAddJobLists();
                    }
                    else if (_addJob.CanFocus)
                    {
                        _addJob.Text = "Add Job";
                        _addJob.Select();
                    }

                    _addJob.AddJobRunButton.Text = "Run";

                    DataGridViewRow jobQueueDGVRow = JobQueueDGV.SelectedRows[0];
                    _addJob.NameBox.Text = jobQueueDGVRow.Cells[0].Value.ToString();
                    _addJob.InputFilesBox.Text = jobQueueDGVRow.Cells[0].ToolTipText;
                    _addJob.OutputDirectoryBox.Text = jobQueueDGVRow.Cells[1].ToolTipText;
                    _addJob.DatabaseLocBox.Text = jobQueueDGVRow.Cells[2].ToolTipText;
                    if (jobQueueDGVRow.Cells[4].Value.ToString() == "Database Search")
                    {
                        _addJob.MyriConfigBox.Text = jobQueueDGVRow.Cells[3].ToolTipText;
                        _addJob.DatabaseRadio.Checked = true;
                    }
                    else
                    {
                        string[] configFiles = jobQueueDGVRow.Cells[3].ToolTipText.Split(System.Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                        _addJob.DTConfigBox.Text = configFiles[0];
                        _addJob.TRConfigBox.Text = configFiles[1];
                        _addJob.TagRadio.Checked = true;
                    }
                }
                catch
                {
                    MessageBox.Show("Not all fields could be cloned");
                }

                CheckForRunableJob();
                
            }
 
        /// <summary>
        /// Prevents row from being selected to run, or unlocks job to be run
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
           private void lockToolStripMenuItem_Click(object sender, EventArgs e)
           {
               DatabaseObjects dbo = new DatabaseObjects(_manager.GetSession());

               if (lockToolStripMenuItem.Text == "Lock")
               {
                   if ((string)JobQueueDGV.SelectedRows[0].Tag == string.Empty || (string)JobQueueDGV.SelectedRows[0].Tag == "Editing")
                   {
                       JobQueueDGV.SelectedRows[0].Tag = "Locked";
                       dbo.UpdateStatus(int.Parse(JobQueueDGV.SelectedRows[0].Cells[0].Tag.ToString()),"Locked");
                       for (int x = 0; x < 6; x++)
                           JobQueueDGV.SelectedRows[0].Cells[x].Style.BackColor = Color.Wheat;
                   }
               }
               else
               {
                   JobQueueDGV.SelectedRows[0].Tag = string.Empty;
                   dbo.UpdateStatus(int.Parse(JobQueueDGV.SelectedRows[0].Cells[0].Tag.ToString()), string.Empty);
                   for (int x = 0; x < 6; x++)
                       JobQueueDGV.SelectedRows[0].Cells[x].Style.BackColor = Color.White;
                   IsValidJob(JobQueueDGV.SelectedRows[0].Index);
               }

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

               programmaticallyPaused = false;
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

       #endregion


        #region Top Menu

        /// <summary>
        /// Exit the program
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
           private void exitToolStripMenuItem_Click(object sender, EventArgs e)
           {
               if (_jobProcess.JobIsRunning())
               {
                   if (MessageBox.Show("Abort current job and exit?", "Exit program", MessageBoxButtons.YesNo).Equals(DialogResult.Yes))
                       System.Environment.Exit(0);
                   return;
               }
               System.Environment.Exit(0);
           }

        /// <summary>
        /// Toolstrip handling of adding new job
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
           private void newJobToolStripMenuItem_Click(object sender, EventArgs e)
           {
               if (_addJob == null || _addJob.IsDisposed)
               {
                   _addJob = new AddJobForm(this);
                   _addJob.Show();
                   PopulateAddJobLists();
               }
               else if (_addJob.CanFocus)
               {
                   _addJob.Text = "Add Job";
                   _addJob.Select();
               }

               _addJob.AddJobRunButton.Text = "Run";
           }

        /// <summary>
        /// Start IDPicker from toolstrip
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
           private void iDPickerToolStripMenuItem_Click(object sender, EventArgs e)
           {
               System.Diagnostics.Process.Start(Properties.Settings.Default.IDPickerLocation);
           }

        /// <summary>
        /// Start full version of Config Editor from toolstrip
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
           private void confiToolStripMenuItem_Click(object sender, EventArgs e)
           {
               System.Diagnostics.Process.Start(_configLocation);
           }

       #endregion


        #region Validation


        /// <summary>
        /// Directs validation to neccessary function based on row
        /// </summary>
        /// <param name="row"> -1 if job is from form, 
        /// row number if job is from DataGridView</param>
        /// <returns></returns>
           private bool IsValidJob(int row)
           {
               // From the AddJob form
               if (row == -1)
                   return IsValidFormJob();

               // From the JobQueue DataGridView
               else if (row < JobQueueDGV.Rows.Count - 1)
                   return IsValidRowJob(row);

               //In case an unexpected row was sent
               else
               {
                   MessageBox.Show(String.Format("Invalid Row: {0}", row));
                   return false;
               }
           }

        /// <summary>
        /// Checks all fields in AddJob form and marks for validity
        /// </summary>
        /// <returns>True only if all fields are valid</returns>
           private bool IsValidFormJob()
           {
               bool AllValid = true;
               string[] inputFiles; //used to get multiple input files
               string foobar; //temporary formatting string


               // Get all input files and validate that they exist
               inputFiles = _addJob.InputFilesBox.Text.Split(System.Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
               foreach (string str in inputFiles)
               {
                   foobar = str.Trim("\"".ToCharArray());
                   if (!File.Exists(foobar) && !(
                       Path.GetExtension(foobar).Equals(".raw") || Path.GetExtension(foobar).Equals(".wiff") ||
                       Path.GetExtension(foobar).Equals(".yep") || Path.GetExtension(foobar).Equals(".mzML") ||
                       Path.GetExtension(foobar).Equals(".mgf") || Path.GetExtension(foobar).Equals(".mzXML")
                       ))
                   {
                       AllValid = false;
                       _addJob.InputFilesBox.BackColor = Color.LightPink;
                       break;
                   }
               }

               if (AllValid)
                   _addJob.InputFilesBox.BackColor = Color.White;

               // Validate Output Directory
               if (Directory.Exists(_addJob.OutputDirectoryBox.Text) && !string.IsNullOrEmpty(_addJob.OutputDirectoryBox.Text))
                   _addJob.OutputDirectoryBox.BackColor = Color.White;
               else
               {
                   AllValid = false;
                   _addJob.OutputDirectoryBox.BackColor = Color.LightPink;
               }

               // Validate Database Location
               if (File.Exists(_addJob.DatabaseLocBox.Text) && Path.GetExtension(_addJob.DatabaseLocBox.Text).Equals(".fasta"))
                   _addJob.DatabaseLocBox.BackColor = Color.White;
               else
               {
                   AllValid = false;
                   _addJob.DatabaseLocBox.BackColor = Color.LightPink;
               }

               // Validate Config Files
               //If Database Search
               if (_addJob.DatabaseRadio.Checked == true)
               {
                   if (File.Exists(_addJob.MyriConfigBox.Text) && Path.GetExtension(_addJob.MyriConfigBox.Text).Equals(".cfg"))
                       _addJob.MyriConfigBox.BackColor = Color.White;
                   else
                   {
                       AllValid = false;
                       _addJob.MyriConfigBox.BackColor = Color.LightPink;
                       if (Path.GetExtension(_addJob.MyriConfigBox.Text).Equals(".pepXML"))
                       {
                           MessageBox.Show("Please use the edit button to convert the .pepXML file to a new .cfg file");
                       }
                   }
               }
               //If Tag Sequencing
               else
               {
                   if (File.Exists(_addJob.DTConfigBox.Text) && Path.GetExtension(_addJob.DTConfigBox.Text).Equals(".cfg"))
                       _addJob.DTConfigBox.BackColor = Color.White;
                   else
                   {
                       AllValid = false;
                       _addJob.DTConfigBox.BackColor = Color.LightPink;
                       if (Path.GetExtension(_addJob.MyriConfigBox.Text).Equals(".tags"))
                       {
                           MessageBox.Show("Please use the edit button to convert the .tags file to a new .cfg file");
                       }
                   }
                   if (File.Exists(_addJob.TRConfigBox.Text) && Path.GetExtension(_addJob.DTConfigBox.Text).Equals(".cfg"))
                       _addJob.TRConfigBox.BackColor = Color.White;
                   else
                   {
                       AllValid = false;
                       _addJob.TRConfigBox.BackColor = Color.LightPink;
                       if (Path.GetExtension(_addJob.MyriConfigBox.Text).Equals(".pepXML"))
                       {
                           MessageBox.Show("Please use the edit button to convert the .pepXML file to a new .cfg file");
                       }
                   }

               }

               return AllValid;
           }

        /// <summary>
        /// Checks row for validity. Mainly used when queuing job to make sure no files have been deleted.
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
           private bool IsValidRowJob(int row)
           {
               bool AllValid = true;
               string[] inputFiles; //used to get multiple input files
               string foobar; //temporary formatting string

               //only need to check if it is unlocked (Check fires automatically on unlock)
               //Prevents validation from removing visual indication of lock
               if ((string)JobQueueDGV.Rows[row].Tag != "Locked")
               {
                   // Get all input files and validate that they exist
                   inputFiles = JobQueueDGV.Rows[row].Cells[0].ToolTipText.Split(System.Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                   foreach (string str in inputFiles)
                   {
                       //Input Box
                       foobar = str.Trim("\"".ToCharArray());
                       if (!File.Exists(foobar))
                       {
                           AllValid = false;
                           JobQueueDGV.Rows[row].Cells[0].Style.BackColor = Color.LightPink;
                           break;
                       }
                   }

                   if (AllValid)
                       JobQueueDGV.Rows[row].Cells[0].Style.BackColor = Color.White;

                   // Validate Output Directory
                   if (Directory.Exists((string)JobQueueDGV.Rows[row].Cells[1].ToolTipText))
                       JobQueueDGV.Rows[row].Cells[1].Style.BackColor = Color.White;
                   else
                   {
                       AllValid = false;
                       JobQueueDGV.Rows[row].Cells[1].Style.BackColor = Color.LightPink;
                   }

                   // Validate Database Location
                   if (File.Exists((string)JobQueueDGV.Rows[row].Cells[2].ToolTipText))
                       JobQueueDGV.Rows[row].Cells[2].Style.BackColor = Color.White;
                   else
                   {
                       AllValid = false;
                       JobQueueDGV.Rows[row].Cells[2].Style.BackColor = Color.LightPink;
                   }

                   // Validate Config Files
                   //If Database Search
                   if (JobQueueDGV.Rows[row].Cells[4].Value.ToString() == "Database Search")
                   {
                       if (File.Exists((string)JobQueueDGV.Rows[row].Cells[3].ToolTipText))
                           JobQueueDGV.Rows[row].Cells[3].Style.BackColor = Color.White;
                       else
                       {
                           AllValid = false;
                           JobQueueDGV.Rows[row].Cells[3].Style.BackColor = Color.LightPink;
                       }
                   }

                   //If Tag Sequencing
                   else
                   {
                       if (File.Exists(JobQueueDGV.Rows[row].Cells[3].ToolTipText.Split(System.Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries)[0]))
                           JobQueueDGV.Rows[row].Cells[3].Style.BackColor = Color.White;
                       else
                       {
                           AllValid = false;
                           JobQueueDGV.Rows[row].Cells[3].Style.BackColor = Color.LightPink;
                       }
                       if (File.Exists(JobQueueDGV.Rows[row].Cells[3].ToolTipText.Split(System.Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries)[1]))
                           JobQueueDGV.Rows[row].Cells[3].Style.BackColor = Color.White;
                       else
                       {
                           AllValid = false;
                           JobQueueDGV.Rows[row].Cells[3].Style.BackColor = Color.LightPink;
                       }
                   }

                   if (AllValid)
                       JobQueueDGV.Rows[row].Tag = string.Empty;
                   else
                       JobQueueDGV.Rows[row].Tag = "Invalid";
               }
               return AllValid;
           }

       #endregion


        #region Database Manipulation

        /// <summary>
        /// Adds new job to database
        /// </summary>
        /// <param name="recievedName"></param>
        /// <param name="recievedInput"></param>
        /// <param name="recievedOutput"></param>
        /// <param name="recievedDatabase"></param>
        /// <param name="recievedFirstConfig"></param>
        /// <param name="recievedSecondConfig"></param>
        /// <param name="numberOfCpus"></param>
        /// <returns></returns>
            private int SaveNewJob(string recievedName, string recievedInput,
                string recievedOutput, string recievedDatabase,
                string recievedFirstConfig, string recievedSecondConfig,
                int numberOfCpus)
               {
                   bool isDatabaseScan = string.IsNullOrEmpty(recievedSecondConfig);
                   DatabaseObjects dbo = new DatabaseObjects(_manager.GetSession());
                   HistoryItem hi = new HistoryItem();
                   TagHistoryItem thi = new TagHistoryItem();
                   InputFile jobFile;
                   ConfigFile firstConfig;
                   ConfigFile secondConfig = null;
                   string tempString;
                   string[] explode;

                   if (isDatabaseScan)
                       firstConfig = ProcessConfigFileString(recievedFirstConfig, "MyriMatch");
                   else
                   {
                       firstConfig = ProcessConfigFileString(recievedFirstConfig, "DirecTag");
                       secondConfig = ProcessConfigFileString(recievedSecondConfig, "TagRecon");
                   }


                   if (isDatabaseScan)
                   {
                       hi.CurrentStatus = string.Empty;
                       hi.JobName = recievedName;
                       hi.StartTime = null;
                       hi.EndTime = null;
                       hi.OutputDirectory = recievedOutput;
                       hi.ProteinDatabase = recievedDatabase;
                       hi.InitialConfigFile = firstConfig;
                       hi.CPUs = numberOfCpus;
                       dbo.SaveItem(hi);
                   }
                   else
                   {
                       thi.CurrentStatus = string.Empty;
                       thi.JobName = recievedName;
                       thi.StartTime = null;
                       thi.EndTime = null;
                       thi.OutputDirectory = recievedOutput;
                       thi.ProteinDatabase = recievedDatabase;
                       thi.InitialConfigFile = firstConfig;
                       thi.TagConfigFile = secondConfig;
                       thi.CPUs = numberOfCpus;
                       dbo.SaveItem(thi);
                   }

                   explode = recievedInput.Split(System.Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                   foreach (string str in explode)
                   {
                       tempString = str;
                       jobFile = new InputFile();
                       jobFile.FilePath = tempString;
                       if (isDatabaseScan)
                           jobFile.HistoryItem = hi;
                       else
                           jobFile.HistoryItem = thi;

                       dbo.SaveItem(jobFile);
                   }

                   if (isDatabaseScan)
                       return hi.HistoryItemID;
                   else
                       return thi.HistoryItemID;
               }
            
        /// <summary>
        /// Edits preciously saved job in the database
        /// </summary>
        /// <param name="recievedName"></param>
        /// <param name="recievedInput"></param>
        /// <param name="recievedOutput"></param>
        /// <param name="recievedDatabase"></param>
        /// <param name="recievedFirstConfig"></param>
        /// <param name="recievedSecondConfig"></param>
        /// <param name="jobID"></param>
        /// <param name="numberOfCpus"></param>
        /// <returns></returns>
            private int EditOldJob(string recievedName, string recievedInput,
                string recievedOutput, string recievedDatabase,
                string recievedFirstConfig, string recievedSecondConfig, int jobID,
                int numberOfCpus)
            {
                bool isDatabaseScan = string.IsNullOrEmpty(recievedSecondConfig);
                DatabaseObjects dbo = new DatabaseObjects(_manager.GetSession());
                HistoryItem hi = new HistoryItem();
                TagHistoryItem thi = new TagHistoryItem();
                InputFile jobFile;
                ConfigFile firstConfig;
                ConfigFile secondConfig = null;
                string tempString;
                string[] explode;

                if (isDatabaseScan)
                {
                    
                    firstConfig = ProcessConfigFileString(recievedFirstConfig, "MyriMatch");
                }
                else
                {
                    firstConfig = ProcessConfigFileString(recievedFirstConfig, "DirecTag");
                    secondConfig = ProcessConfigFileString(recievedSecondConfig, "TagRecon");
                }

                hi = dbo.GetSpecificHistoryItemByID(jobID);


                if (isDatabaseScan)
                {
                    hi.CurrentStatus = string.Empty;
                    hi.JobName = recievedName;
                    hi.StartTime = null;
                    hi.EndTime = null;
                    hi.OutputDirectory = recievedOutput;
                    hi.ProteinDatabase = recievedDatabase;
                    hi.CPUs = numberOfCpus;
                    hi.InitialConfigFile = firstConfig;
                    foreach (InputFile i in hi.FileList)
                    {
                        dbo.DeleteItem(i);
                    }
                    dbo.SaveItem(hi);
                }
                else if (!(hi is TagHistoryItem))
                {
                    thi.CurrentStatus = string.Empty;
                    thi.JobName = recievedName;
                    thi.StartTime = null;
                    thi.EndTime = null;
                    thi.OutputDirectory = recievedOutput;
                    thi.ProteinDatabase = recievedDatabase;
                    thi.CPUs = numberOfCpus;
                    thi.InitialConfigFile = firstConfig;
                    thi.TagConfigFile = secondConfig;
                    dbo.SaveItem(thi);
                    DeleteJobByID(jobID);
                }
                else
                {
                    thi = (TagHistoryItem)hi;
                    thi.CurrentStatus = string.Empty;
                    thi.JobName = recievedName;
                    thi.StartTime = null;
                    thi.EndTime = null;
                    thi.OutputDirectory = recievedOutput;
                    thi.ProteinDatabase = recievedDatabase;
                    thi.CPUs = numberOfCpus;
                    thi.InitialConfigFile = firstConfig;
                    thi.TagConfigFile = secondConfig;
                    foreach (InputFile i in thi.FileList)
                    {
                        dbo.DeleteItem(i);
                    }
                    dbo.SaveItem(thi);
                }

                explode = recievedInput.Split(System.Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                foreach (string str in explode)
                {
                    tempString = str;
                    jobFile = new InputFile();
                    jobFile.FilePath = tempString;
                    if (isDatabaseScan)
                        jobFile.HistoryItem = hi;
                    else
                        jobFile.HistoryItem = thi;

                    dbo.SaveItem(jobFile);
                }

                if (isDatabaseScan)
                    return hi.HistoryItemID;
                else
                    return thi.HistoryItemID;
            }
            
        /// <summary>
        /// Goes through the config file and determine if it needs to be saved to the database 
        /// (whether by being a new config file or an edited version of an old one)
        /// </summary>
        /// <param name="configFileLocation">Absolute location of the config file</param>
        /// <param name="destinationProgram">Program that will be using this config file</param>
        /// <returns></returns>
            public ConfigFile ProcessConfigFileString(string configFileLocation, string destinationProgram)
               {
                   #region Lists of valid paramaters
                   string[] boolList =
                    {
                        "UseChargeStateFromMS",
                        "AdjustPrecursorMass",
                        "DuplicateSpectra",
                        "UseSmartPlusThreeModel",
                        "MassReconMode",
                        "UseNETAdjustment",
                        "ComputeXCorr",
                        "UseAvgMassOfSequences"
                    };
                   string[] intList =
                {
                    "DeisotopingMode",
                    "NumMinTerminiCleavages",
                    "CPUs",
                    "StartSpectraScanNum",
                    "StartProteinIndex",
                    "NumMaxMissedCleavages",
                    "EndSpectraScanNum",
                    "EndProteinIndex",
                    "ProteinSampleSize",
                    "MaxDynamicMods",
                    "MaxNumPreferredDeltaMasses",
                    "NumChargeStates",
                    "NumIntensityClasses",
                    "MaxResults",
                    "MaxPeakCount",
                    "NumSearchBestAdjustments",
                    "TagLength",
                    "MinCandidateLength",
                    "MaxTagCount"
                };
                   string[] doubleList =
                {
                    "MinSequenceMass",
                    "IsotopeMzTolerance",
                    "FragmentMzTolerance",
                    "ComplementMzTolerance",
                    "TicCutoffPercentage",
                    "PrecursorMzTolerance",
                    "MaxSequenceMass",
                    "ClassSizeMultiplier",
                    "MaxPrecursorAdjustment",
                    "MinPrecursorAdjustment",
                    "BlosumThreshold",
                    "MaxModificationMassPlus",
                    "MaxModificationMassMinus",
                    "NTerminusMzTolerance",
                    "CTerminusMzTolerance",
                    "NTerminusMassTolerance",
                    "CTerminusMassTolerance",
                    "IntensityScoreWeight",
                    "MzFidelityScoreWeight",
                    "ComplementScoreWeight",
                    "MaxTagScore"
                };
                   string[] stringList =
                {
                    "CleavageRules",
                    "PrecursorMzToleranceUnits",
                    "FragmentMzToleranceUnits",
                    "Blosum",
                    "UnimodXML",
                    "ExplainUnknownMassShiftsAs",
                    "StaticMods",
                    "DynamicMods",
                    "PreferredDeltaMasses"
                };
                   #endregion

                   bool allValid = true;
                   ConfigFile config;
                   ConfigProperty configParameter;
                   DatabaseObjects dbo = new DatabaseObjects(_manager.GetSession());
                   StreamReader fileIn;
                   List<string[]> recievedParameters = new List<string[]>();
                   List<string[]> databaseParameters = new List<string[]>();
                   string tempString;
                   string[] lineList;
                   string[] lineContents;

                   //First open the file and get the information
                   try
                   {
                       fileIn = new StreamReader(configFileLocation);
                       tempString = fileIn.ReadToEnd();
                       fileIn.Close();
                       fileIn.Dispose();
                   }
                   catch
                   {
                       return null;
                   }

                   //split up the file and find out what it contains
                   lineList = tempString.Split(System.Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                   foreach (string str in lineList)
                   {
                       lineContents = str.Split("=".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                       for (int x = 0; x < lineContents.Length; x++)
                           lineContents[x] = lineContents[x].Trim();
                       recievedParameters.Add(lineContents);
                   }

                   //Now see if the config file path is already recored
                   config = dbo.retrieveLatestConfigFileByFilePath(configFileLocation);

                   //Do a check on whether it has been changed
                   if (config == null)
                   {
                       //dont have to check for changes, just perform initial setup
                       allValid = false;
                       config = new ConfigFile();
                       config.DestinationProgram = destinationProgram;
                       config.FilePath = configFileLocation;
                       config.FirstUsedDate = DateTime.Now;
                   }
                   else
                   {
                       //make sure new parameters perfectly match old parameters
                       tempString = string.Empty;
                       foreach (ConfigProperty cp in config.PropertyList)
                           tempString += String.Format("{0} = {1}{2}", cp.Name, cp.Value, System.Environment.NewLine);
                       lineList = tempString.Split(System.Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                       foreach (string str in lineList)
                       {
                           lineContents = str.Split("=".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                           for (int x = 0; x < lineContents.Length; x++)
                               lineContents[x] = lineContents[x].Trim();
                           databaseParameters.Add(lineContents);
                       }

                       if (recievedParameters.Count == databaseParameters.Count)
                       {
                           recievedParameters.Sort((x, y) => string.Compare(x[0], y[0]));
                           databaseParameters.Sort((x, y) => string.Compare(x[0], y[0]));
                           for (int x = 0; x < recievedParameters.Count; x++)
                           {
                               for (int y = 0; y < recievedParameters[x].Length; y++)
                               {
                                   if (recievedParameters[x][y] != databaseParameters[x][y])
                                   {
                                       allValid = false;
                                       break;
                                   }
                               }
                               if (!allValid)
                                   break;
                           }
                           if (!allValid)
                           {
                               config = new ConfigFile();
                               config.DestinationProgram = destinationProgram;
                               config.FilePath = configFileLocation;
                               config.FirstUsedDate = DateTime.Now;
                           }
                       }
                       else
                       {
                           allValid = false;
                           config = new ConfigFile();
                           config.DestinationProgram = destinationProgram;
                           config.FilePath = configFileLocation;
                           config.FirstUsedDate = DateTime.Now;
                       }
                   }

                   //if a new one had to be made then record new properties
                   if (!allValid)
                   {
                       dbo.SaveItem(config);

                       for (int x = 0; x < recievedParameters.Count; x++)
                       {
                           configParameter = new ConfigProperty();
                           configParameter.Name = recievedParameters[x][0];
                           configParameter.Value = recievedParameters[x][1];
                           configParameter.ConfigAssociation = config;
                           if (stringList.Contains(recievedParameters[x][0]))
                               configParameter.Type = "string";
                           else if (doubleList.Contains(recievedParameters[x][0]))
                               configParameter.Type = "double";
                           else if (intList.Contains(recievedParameters[x][0]))
                               configParameter.Type = "int";
                           else if (boolList.Contains(recievedParameters[x][0]))
                               configParameter.Type = "bool";
                           else
                               configParameter.Type = "unknown";
                           dbo.SaveItem(configParameter);
                       }
                   }

                   return config;
               }

        /// <summary>
        /// Removes job from history in database
        /// </summary>
        /// <param name="jobID"></param>
            private void DeleteJobByID(int jobID)
            {
                DatabaseObjects dbo = new DatabaseObjects(_manager.GetSession());
                HistoryItem hi = dbo.GetSpecificHistoryItemByID(jobID);

                foreach (InputFile i in hi.FileList)
                {
                    dbo.DeleteItem(i);
                }

                dbo.DeleteItem(hi);
            }

        /// <summary>
        /// Called from ProgramHandler to indicate to form 
        /// and database that a job has been completed
        /// </summary>
            public void InidcateJobDone()
            {
                DatabaseObjects dbo = new DatabaseObjects(_manager.GetSession());
                bool unsuccessful = JobQueueDGV.Rows[_lastCompleted + 1].Tag.ToString() == "Unsuccessful";

                if (unsuccessful)
                    JobQueueDGV[5, _lastCompleted + 1].Value = 0;
                else
                    JobQueueDGV.Rows[_lastCompleted+1].Tag = "Finished";
                TrayIcon.Text = "BumberDash";
                _lastCompleted++;
                dbo.IndicateJobEnd(int.Parse(JobQueueDGV[0, _lastCompleted].Tag.ToString()),unsuccessful);
                CheckForRunableJob();
            }

        /// <summary>
        /// Called on program close to remove any config files that are not 
        /// used by recorded jobs (if a job has been deleted, edited, etc)
        /// </summary>
            public void RemoveUnusedConfigs()
            {
                DatabaseObjects dbo = new DatabaseObjects(_manager.GetSession());

                IList<ConfigFile> configFiles = dbo.GetConfigFileList();

                foreach (ConfigFile cf in configFiles)
                {
                    if (!cf.UsedByList.Any() && !cf.UsedByList2.Any())
                    {
                        foreach (ConfigProperty cp in cf.PropertyList)
                        {
                            dbo.DeleteItem(cp);
                        }
                        dbo.DeleteItem(cf);
                    }
                }
            }

        /// <summary>
        /// Saves row numbers to database so datagridview will look the same on reload
        /// </summary>
            public void SaveRowNumbers()
            {
                DatabaseObjects dbo = new DatabaseObjects(_manager.GetSession());

                for (int x = 0; x < JobQueueDGV.Rows.Count-1; x++)
                {
                    dbo.SaveRowNumber(int.Parse(JobQueueDGV[0,x].Tag.ToString()), x);
                }
            }

        #endregion


        #region Deletion

        /// <summary>
        /// Checks if job can be deleted
        /// </summary>
        /// <param name="row"></param>
           private void KillCheck(int row)
           {
               programmaticallyPaused = true;
               if (row != JobQueueDGV.Rows.Count - 1)
               {
                   switch (JobQueueDGV.Rows[row].Tag.ToString())
                   {
                       case "":
                       case "Locked":
                           DeleteQueued(row);
                           break;
                       case "Editing":
                           MessageBox.Show("Cannot remove job while it is being edited");
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
               

               programmaticallyPaused = false;
               SaveRowNumbers();
               CheckForRunableJob();
           }

        /// <summary>
        /// Delete a job that has been queued, but has not yet been processed
        /// </summary>
        /// <param name="row"></param>
           private void DeleteQueued(int row)
           {
               programmaticallyPaused = true;
               if (MessageBox.Show("Are you sure you want to remove this job?",
                                      "Delete Job",
                                      MessageBoxButtons.YesNo)
                                        .Equals(DialogResult.Yes))
               {
                   DeleteJobByID(int.Parse(JobQueueDGV[0, row].Tag.ToString()));
                   JobQueueDGV.Rows.RemoveAt(row);
               }
               else
               {
                   JobQueueDGV.Rows[JobQueueDGV.SelectedRows[0].Index].Tag = string.Empty;
                   IsValidJob(JobQueueDGV.SelectedRows[0].Index);
               }
           }

        /// <summary>
        /// Delete a job that is currently being processed, ending the processing early. 
           /// Job can then either be requeued or it can be removed from history.
        /// </summary>
        /// <param name="row"></param>
           private void DeleteRunning(int row)
           {
               DatabaseObjects dbo = new DatabaseObjects(_manager.GetSession());
               string[] fileNames;
               FileInfo fi;

               //Remove associated pepXMLs? (yes/no)-> Save job for later? (no)-> [[Remove associated files as well?]]

               switch (MessageBox.Show("Delete associated result files as well?",
                   "Abort job",
                   MessageBoxButtons.YesNoCancel))
               {
                   case DialogResult.Yes:
                       _jobProcess.ForceKill();
                       fileNames = JobQueueDGV.Rows[row].Cells[0].ToolTipText.Split(System.Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                       if (JobQueueDGV.Rows[row].Cells[4].Value.ToString() == "Database Search")
                       {
                           for (int x = 0; x < fileNames.Length;x++)
                           {
                               fi = new FileInfo(fileNames[x].Trim("\"".ToCharArray()));
                               fileNames[x] = fi.Name.Remove(fi.Name.Length - fi.Extension.Length);
                               DeleteFile(String.Format(@"{0}\{1}.pepXML", JobQueueDGV.Rows[row].Cells[1].ToolTipText, fileNames[x]));
                           }
                       }
                       else
                       {
                           for (int x = 0; x < fileNames.Length;x++)
                           {
                               fi = new FileInfo(fileNames[x].Trim("\"".ToCharArray()));
                               fileNames[x] = fi.Name.Remove(fi.Name.Length - fi.Extension.Length);
                               DeleteFile(String.Format(@"{0}\{1}-tags.tags", JobQueueDGV.Rows[row].Cells[1].ToolTipText, fileNames[x]));
                               DeleteFile(String.Format(@"{0}\{1}-tags.pepXML", JobQueueDGV.Rows[row].Cells[1].ToolTipText, fileNames[x]));
                           }
                       }
                       if (MessageBox.Show("Save job for later?", "Save job information?", MessageBoxButtons.YesNo).Equals(DialogResult.Yes))
                       {
                           if (_jobProcess != null && JobQueueDGV.Rows[row].Tag.ToString() == "Finished")
                               _jobProcess.DeletedAbove();
                           JobQueueDGV.Rows[row].Cells[5].Value = 0;
                           JobQueueDGV.Rows[row].Tag = "Locked";
                           dbo.UpdateStatus(int.Parse(JobQueueDGV[0, row].Tag.ToString()), "Locked");
                           for (int x = 0; x < 6; x++)
                               JobQueueDGV.Rows[row].Cells[x].Style.BackColor = Color.Wheat;
                       }
                       else
                       {
                           if (_jobProcess != null && JobQueueDGV.Rows[row].Tag.ToString() == "Finished")
                               _jobProcess.DeletedAbove();
                           DeleteJobByID(int.Parse(JobQueueDGV[0, row].Tag.ToString()));
                           JobQueueDGV.Rows.RemoveAt(row);
                       }
                       break;

                   case DialogResult.No:
                       _jobProcess.ForceKill();
                       if (MessageBox.Show("Save job for later?", "Save job information?", MessageBoxButtons.YesNo).Equals(DialogResult.Yes))
                       {
                           if (_jobProcess != null && JobQueueDGV.Rows[row].Tag.ToString() == "Finished")
                               _jobProcess.DeletedAbove();
                           JobQueueDGV.Rows[row].Cells[5].Value = 0;
                           JobQueueDGV.Rows[row].Tag = "Locked";
                           for (int x = 0; x < 6; x++)
                               JobQueueDGV.Rows[row].Cells[x].Style.BackColor = Color.Wheat;
                       }
                       else
                       {
                           if (_jobProcess != null && JobQueueDGV.Rows[row].Tag.ToString() == "Finished")
                               _jobProcess.DeletedAbove();
                           DeleteJobByID(int.Parse(JobQueueDGV[0, row].Tag.ToString()));
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
           private void DeleteFile(string fileToDelete)
           {
               try
               {
                   File.Delete(fileToDelete);
               }
               catch (Exception ex)
               {
                   MessageBox.Show("Could not delete file due to exception: " + ex.ToString());
               }
           }

        /// <summary>
        /// Deletes a job that has already been processed and warns ProgramHandler 
        /// that the row index of the job it is working on has changed.
        /// </summary>
        /// <param name="row"></param>
           private void DeleteFinished(int row)
           {
               //Remove associated pepXMLs? (yes)-> ((Remove associated files as well?))
               string[] fileNames;
               FileInfo fi;

               switch (MessageBox.Show("Delete associated result files as well?",
                   "Abort job",
                   MessageBoxButtons.YesNoCancel))
               {
                   case DialogResult.Yes:
                       fileNames = JobQueueDGV.Rows[row].Cells[0].ToolTipText.Split(System.Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                       if (JobQueueDGV.Rows[row].Cells[4].Value.ToString() == "Database Search")
                       {
                           for (int x = 0; x < fileNames.Length; x++)
                           {
                               fi = new FileInfo(fileNames[x].Trim("\"".ToCharArray()));
                               fileNames[x] = fi.Name.Remove(fi.Name.Length - fi.Extension.Length);
                               DeleteFile(String.Format(@"{0}\{1}.pepXML", JobQueueDGV.Rows[row].Cells[1].ToolTipText, fileNames[x]));
                           }
                       }
                       else
                       {
                           for (int x = 0; x < fileNames.Length; x++)
                           {
                               fi = new FileInfo(fileNames[x].Trim("\"".ToCharArray()));
                               fileNames[x] = fi.Name.Remove(fi.Name.Length - fi.Extension.Length);
                               DeleteFile(String.Format(@"{0}\{1}-tags.tags", JobQueueDGV.Rows[row].Cells[1].ToolTipText, fileNames[x]));
                               DeleteFile(String.Format(@"{0}\{1}-tags.pepXML", JobQueueDGV.Rows[row].Cells[1].ToolTipText, fileNames[x]));
                           }
                       }
                       DeleteJobByID(int.Parse(JobQueueDGV[0, row].Tag.ToString()));
                       JobQueueDGV.Rows.RemoveAt(row);
                       if (_jobProcess != null)
                           _jobProcess.DeletedAbove();
                       break;

                   case DialogResult.No:
                       DeleteJobByID(int.Parse(JobQueueDGV[0, row].Tag.ToString()));
                       JobQueueDGV.Rows.RemoveAt(row);
                       if (_jobProcess != null)
                           _jobProcess.DeletedAbove();
                       break;
                   case DialogResult.Cancel:
                       break;
               }
           }

        #endregion
        
        
        /// <summary>
        /// Converts null values into 0 so it can be handled by percentage column
        /// </summary>
        /// <param name="Percentage"></param>
        /// <returns></returns>
       private int ParsePercentage(object Percentage)
        {
            if (Percentage == null)
                return 0;
            else
                return int.Parse(Percentage.ToString());
        }

        /// <summary>
        /// Attempts to arrange the DataGridView to the next valid job
        /// </summary>
        /// <returns>True if there is a valid job to run</returns>
       private bool CanArrangeToNextJob()
        {
            int currentTop = _lastCompleted + 1;

            IsValidJob(currentTop);

            while (JobQueueDGV.Rows[currentTop].Tag.ToString() != string.Empty)
            {
                currentTop++;
                if (currentTop == JobQueueDGV.Rows.Count-1)
                    break;
                IsValidJob(currentTop);
            }

            if (currentTop > _lastCompleted + 1)
            {
                if (currentTop == JobQueueDGV.Rows.Count - 1)
                    return false;
                DataGridViewRow nextValidJob = JobQueueDGV.Rows[currentTop];
                JobQueueDGV.Rows.RemoveAt(currentTop);
                JobQueueDGV.Rows.Insert(_lastCompleted + 1, nextValidJob);
            }

            SaveRowNumbers();
            return true;
        }

        /// <summary>
        /// Checks multiple factors to determine if a new job can be sent to the ProgramHandler
        /// </summary>
       internal void CheckForRunableJob()
       {
           DatabaseObjects dbo = new DatabaseObjects(_manager.GetSession());
           string[] configFiles;

           if (!programmaticallyPaused && !manuallyPaused && _jobProcess != null &&
               !_jobProcess.JobIsRunning() && _lastCompleted < JobQueueDGV.Rows.Count - 2 &&
               CanArrangeToNextJob())
           {
               configFiles = JobQueueDGV.Rows[_lastCompleted+1].Cells[3].ToolTipText.Split(System.Environment.NewLine.ToCharArray(),StringSplitOptions.RemoveEmptyEntries);

               //run edit job to guarntee actual config file and database config file match
               if (JobQueueDGV.Rows[_lastCompleted + 1].Cells[4].Value.ToString() == "Database Search")
                   EditOldJob(JobQueueDGV.Rows[_lastCompleted + 1].Cells[0].Value.ToString(),
                       JobQueueDGV.Rows[_lastCompleted + 1].Cells[0].ToolTipText,
                       JobQueueDGV.Rows[_lastCompleted + 1].Cells[1].ToolTipText,
                       JobQueueDGV.Rows[_lastCompleted + 1].Cells[2].ToolTipText,
                       configFiles[0],
                       null,
                       int.Parse(JobQueueDGV.Rows[_lastCompleted + 1].Cells[0].Tag.ToString()),
                       int.Parse(JobQueueDGV.Rows[_lastCompleted + 1].Cells[6].Tag.ToString()));
               else
                   EditOldJob(JobQueueDGV.Rows[_lastCompleted + 1].Cells[0].Value.ToString(),
                       JobQueueDGV.Rows[_lastCompleted + 1].Cells[0].ToolTipText,
                       JobQueueDGV.Rows[_lastCompleted + 1].Cells[1].ToolTipText,
                       JobQueueDGV.Rows[_lastCompleted + 1].Cells[2].ToolTipText,
                       configFiles[0],
                       configFiles[1],
                       int.Parse(JobQueueDGV.Rows[_lastCompleted + 1].Cells[0].Tag.ToString()),
                       int.Parse(JobQueueDGV.Rows[_lastCompleted + 1].Cells[6].Tag.ToString()));

               dbo.IndicateJobBegin(int.Parse(JobQueueDGV.Rows[_lastCompleted + 1].Cells[0].Tag.ToString()));
               JobQueueDGV.Rows[_lastCompleted + 1].Tag = "Running";
               JobQueueDGV.Rows[_lastCompleted + 1].Cells[5].Tag = string.Empty;
               _jobProcess.StartNewJob(_lastCompleted + 1);
           }

           if (JobQueueDGV.SelectedRows.Count > 0)
               UpdateStatusText();
       }

        /// <summary>
        /// Sets the rowStatusLabel text to reflect the state of the currently selected row

        /// </summary>
       internal void UpdateStatusText()
       {
           if (JobQueueDGV.SelectedRows.Count > 0)
           {
               rowStatusLabel.Text = (string)JobQueueDGV.SelectedRows[0].Tag;
               if (rowStatusLabel.Text == "Running")
                   rowStatusLabel.Text = (string)JobQueueDGV.SelectedRows[0].Cells[5].Tag;
           }
       }

        /// <summary>
        /// Releases internal queue pause when right click menu is closed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
       private void JQRowMenu_Closed(object sender, ToolStripDropDownClosedEventArgs e)
       {
           programmaticallyPaused = false;
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
               manuallyPaused = false;
               pauseButton.Text = "Pause";
               CheckForRunableJob();
           }
           else
           {
               manuallyPaused = true;
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
           if (_jobProcess != null && _jobProcess.JobIsRunning() && MessageBox.Show("Are you sure you want to exit? Progress on the current job will be lost.", "Exit Program", MessageBoxButtons.YesNo).Equals(DialogResult.Yes))
           {
               _jobProcess.ForceKill();
           }
           else if (_jobProcess != null && _jobProcess.JobIsRunning())
               e.Cancel = true;

           if (!e.Cancel)
               _jobLog.FullyClose();

           //clean up database
           RemoveUnusedConfigs();
       }

       private void setIDPickerLocationToolStripMenuItem_Click(object sender, EventArgs e)
       {
           OpenFileDialog openFile = new OpenFileDialog();
           openFile.Filter = "Executable (.exe)|*.exe";
           openFile.Title = "ID Picker Location";
           openFile.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
           openFile.RestoreDirectory = true;
           openFile.CheckFileExists = true;
           openFile.CheckPathExists = true;
           openFile.Multiselect = false;

           if (openFile.ShowDialog() == DialogResult.OK)
           {
               Properties.Settings.Default.IDPickerLocation = openFile.FileName;
               Properties.Settings.Default.Save();
               iDPickerToolStripMenuItem.Visible = true;
           }
       }

       internal void AddLogLine(string line)
       {
           _jobLog.logText.AppendText(System.Environment.NewLine + line);
           //_jobLog.logText.SelectionStart = _jobLog.logText.Text.Length;
           //_jobLog.logText.ScrollToCaret();
       }

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

        private void QueueForm_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                TrayIcon.Visible = true;
                TrayIcon.ShowBalloonTip(5000);
            }
            else
            {
                
            }
        }

        private void TrayIcon_DoubleClick(object sender, EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            TrayIcon.Visible = false;
        }

        public void IndicateRowError(int row)
        {            
            JobQueueDGV[5, row].Style.BackColor = Color.LightPink;
            JobQueueDGV.Rows[row].Tag = "Unsuccessful";
        }

        private void showToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            TrayIcon.Visible = false;
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutForm af = new AboutForm();
            af.ShowDialog();
            af.Close();
            af.Dispose();
        }

       
    }
}
