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

namespace MSConvertGUI
{
    public partial class ProgressForm : Form
    {
        public class JobInfo
        {
            public MainLogic workProcess;
            public TextBox updateTextbox;
            public DataGridViewRow rowShown;
        }

        private IEnumerable<object> _filesToProcess;
        private string _outputFolder;
        private string _options;
        private List<MainLogic> _tasksRunningList;
        private Map<string, UnifiBrowserForm.Credentials> _unifiCredentialsByUrl;
        private Map<string, int> _usedOutputFilenames;
        private object _calculateSHA1Mutex = new object();
        private object _cancelMutex = new object();

        public ProgressForm(IEnumerable<object> filesToProcess, string outputFolder, string options, Map<string, UnifiBrowserForm.Credentials> unifiCredentialsByUrl)
        {
            InitializeComponent();
            _filesToProcess = filesToProcess;
            _outputFolder = outputFolder;
            _options = options;
            _tasksRunningList = new List<MainLogic>();
            _unifiCredentialsByUrl = unifiCredentialsByUrl;
            _usedOutputFilenames = new Map<string, int>();
        }

        internal void UpdatePercentage(int result, int maxValue, JobInfo info)
        {
            lock (_cancelMutex)
                if (info.workProcess.Canceled)
                    return;

            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => UpdatePercentage(result, maxValue, info)));
                return;
            }

            if (result >= 0)
            {
                ((DataGridViewProgressCell)info.rowShown.Cells[1]).Maximum = maxValue;
                info.rowShown.Cells[1].Value = result;
            }
            else
            {
                foreach (DataGridViewCell item in info.rowShown.Cells)
                    item.Style.ForeColor = Color.Red;
            }
        }

        internal void UpdateLog(string result, JobInfo info)
        {
            lock (_cancelMutex)
                if (info.workProcess.Canceled)
                    return;

            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => UpdateLog(result, info)));
                return;
            }

            info.updateTextbox.Text += result + Environment.NewLine;
            info.updateTextbox.SelectionStart = info.updateTextbox.Text.Length;
            info.updateTextbox.ScrollToCaret();
        }

        private void UpdateBarStatus(string status, ProgressBarStyle style, JobInfo info)
        {
            lock (_cancelMutex)
                if (info.workProcess.Canceled)
                    return;

            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => UpdateBarStatus(status, style, info)));
                return;
            }

            ((DataGridViewProgressCell) info.rowShown.Cells[1]).ProgressBarStyle = style;
            ((DataGridViewProgressCell) info.rowShown.Cells[1]).Text = status;
        }

        private void JobDataView_SelectionChanged(object sender, EventArgs e)
        {
            if (JobDataView.SelectedRows.Count < 1 || JobDataView.SelectedRows[0].Tag == null)
                return;

            var info = (JobInfo)JobDataView.SelectedRows[0].Tag;
            foreach (Control control in ProgressSplit.Panel2.Controls)
                control.Visible = false;
            info.updateTextbox.Visible = true;
        }

        private void ProgressForm_Load(object sender, EventArgs e)
        {
            var boxShown = false;

            foreach (var item in _filesToProcess)
            {
                var info = new JobInfo();

                var textBox = new TextBox
                                  {
                                      Text =
                                          string.Format("{0}{1}{2}{1}Starting...{1}", item, Environment.NewLine,
                                                        new string('-', 30)),
                                      Dock = DockStyle.Fill,
                                      Multiline = true,
                                      Tag = info,
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
                info.updateTextbox = textBox;

                JobDataView.Rows.Add(item, 0);
                var row = JobDataView.Rows[JobDataView.Rows.Count - 1];
                row.Tag = info;
                info.rowShown = row;

                var runProgram = new MainLogic(info, _usedOutputFilenames, _calculateSHA1Mutex)
                                     {
                                         PercentageUpdate = UpdatePercentage,
                                         LogUpdate = UpdateLog,
                                         StatusUpdate = UpdateBarStatus
                                     };
                _tasksRunningList.Add(runProgram);
                info.workProcess = runProgram;

                string workItem = (item as INetworkSource)?.Url ?? item.ToString();
                if (MainForm.IsNetworkSource(item))
                    workItem = _unifiCredentialsByUrl[workItem].GetUrlWithAuthentication(workItem);

                var config = runProgram.ParseCommandLine(_outputFolder, (workItem + "|" + _options).Trim('|'));
                runProgram.QueueWork(config);
            }

            MainLogic.RunQueue();
        }

        private void ProgressForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            lock (_cancelMutex)
            {
                MainLogic.ClearQueue();
                foreach (var item in _tasksRunningList)
                    item.ForceExit();
            }
        }
    }
}
