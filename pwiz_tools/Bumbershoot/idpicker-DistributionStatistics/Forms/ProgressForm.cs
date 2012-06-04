//
// $Id$
//
//
// Original author: Jay Holman <jay.holman .@. vanderbilt.edu>
//
// Copyright 2011 Vanderbilt University - Nashville, TN 37232
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CustomProgressCell;
using pwiz.CLI.util;
using Microsoft.WindowsAPICodePack.Taskbar;

namespace IDPicker
{
    public partial class ProgressForm : Form
    {
        private Dictionary<string, DataGridViewProgressCell> _progressCellByTaskName;
        private Dictionary<string, TextBox> _textBoxLogByTaskName;
        private Dictionary<string, string> _lastMessageByTaskName;
        private Dictionary<string, DataGridViewRow> _rowByTaskName;
        private bool _isClosing;
        private int _tasksDone;

        public bool Cancelled { get { return _isClosing; } }
 
        class IterationListenerProxy : IterationListener
        {
            public ProgressForm form { get; set; }

            public override Status update (UpdateMessage updateMessage)
            {
                return form.update(updateMessage);
            }
        }

        public EventHandler NonFatalErrorCaught;
        public IterationListener.Status update (IterationListener.UpdateMessage updateMessage)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(() => update(updateMessage)));
                return (IterationListener.Status) IterationListener.Status.Ok;
            }

            if (_isClosing)
                return IterationListener.Status.Cancel;

            string[] parts = updateMessage.message.Split('*');
            string taskName = parts.Length > 1 ? parts[0] : String.Empty;
            string message = parts.Length > 1 ? parts[1] : parts[0];
            int index = updateMessage.iterationIndex;
            int count = updateMessage.iterationCount;

            //Check for Qonverter failure
            if (message == "[QonverterError]" && parts.Length > 2)
            {
                var errorMessage = parts[2];
                NonFatalErrorCaught(new[]{"Non-fatal qonverter error: " + errorMessage,taskName}, null);
                return IterationListener.Status.Ok;
            }

            // if the update is not row-specific, update the window title
            string[] parts2 = Text.Split(':');
            if (String.IsNullOrEmpty(taskName))
            {
                Text = String.Format("{0}: {1} ({2}/{3})", parts2[0], message, index, count);
                return IterationListener.Status.Ok;
            }
            else if (parts.Length > 1)
                Text = parts2[0];

            var row = _rowByTaskName[taskName];
            var progressCell = _progressCellByTaskName[taskName];

            if (row.Index >= 2)//Environment.ProcessorCount)
            {
                JobDataView.Rows.Remove(row);
                JobDataView.Rows.Insert(0, row);
            }

            if (count == 0)
            {
                progressCell.ProgressBarStyle = ProgressBarStyle.Marquee;
                if (index == 0)
                {
                    progressCell.Text = message;
                    progressCell.Value = 0;
                }
                else
                {
                    progressCell.Text = String.Format("{0} ({1})", message, index + 1);
                }
            }
            else
            {
                progressCell.ProgressBarStyle = ProgressBarStyle.Continuous;
                progressCell.Maximum = count;
                progressCell.Value = index + 1;
                progressCell.Text = String.Format("{0} ({1}/{2})", message, index + 1, count);
            }

            lock (_lastMessageByTaskName)
                if (!_lastMessageByTaskName.ContainsKey(taskName) || _lastMessageByTaskName[taskName] != message)
                {
                    _lastMessageByTaskName[taskName] = message;
                    _textBoxLogByTaskName[taskName].AppendText(String.Format("{0}{1}", message, Environment.NewLine));
                }

            if (message.Contains("done"))
                if (TaskbarManager.IsPlatformSupported)
                {
                    TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Normal);
                    TaskbarManager.Instance.SetProgressValue(++_tasksDone, _rowByTaskName.Count);
                }

            return IterationListener.Status.Ok;
        }

        public ProgressForm(IEnumerable<string> taskNames, IterationListenerRegistry ilr)
        {
            InitializeComponent();

            _progressCellByTaskName = new Dictionary<string, DataGridViewProgressCell>();
            _textBoxLogByTaskName = new Dictionary<string, TextBox>();
            _lastMessageByTaskName = new Dictionary<string, string>();
            _rowByTaskName = new Dictionary<string, DataGridViewRow>();
            _tasksDone = 0;

            ilr.addListener(new IterationListenerProxy() { form = this }, 1000);

            var boxShown = false;

            foreach (var task in taskNames)
            {
                var textBox = _textBoxLogByTaskName[task] = new TextBox
                {
                    Text = String.Format("{0}{1}{0}Starting...{0}",
                                         Environment.NewLine,
                                         new string('-', 30)),
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical
                };

                if (boxShown)
                    textBox.Visible = false;
                else
                {
                    textBox.Visible = true;
                    boxShown = true;
                }

                ProgressSplit.Panel2.Controls.Add(textBox);

                JobDataView.Rows.Add(task, 0);
                var row = JobDataView.Rows[JobDataView.Rows.Count - 1];
                row.Tag = textBox;
                _rowByTaskName[task] = row;
                _progressCellByTaskName[task] = row.Cells[1] as DataGridViewProgressCell;
                _progressCellByTaskName[task].Text = "waiting";
            }
        }

        private void ProgressForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _isClosing = true;
        }

        private void JobDataView_SelectionChanged (object sender, EventArgs e)
        {
            if (JobDataView.SelectedRows.Count < 1 || JobDataView.SelectedRows[0].Tag == null)
                return;

            foreach (Control control in ProgressSplit.Panel2.Controls)
                control.Visible = false;
            (JobDataView.SelectedRows[0].Tag as TextBox).Visible = true;
        }
    }
}
