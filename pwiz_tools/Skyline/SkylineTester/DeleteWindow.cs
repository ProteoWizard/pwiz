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
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;

namespace SkylineTester
{
    public partial class DeleteWindow : Form
    {
        private readonly string _deletePath;
        private string[] _allFiles;
        private BackgroundWorker _deleteWorker;

        public DeleteWindow(string deletePath)
        {
            InitializeComponent();

            _deletePath = deletePath;
            Load += OnLoad;
        }

        private void OnLoad(object sender, EventArgs eventArgs)
        {
            Text = "Deleting {0}...".With(Path.GetFileName(_deletePath));
            _allFiles = Directory.GetFiles(_deletePath, "*.*", SearchOption.AllDirectories);
            progressBarDelete.Maximum = _allFiles.Length;

            _deleteWorker = new BackgroundWorker {WorkerSupportsCancellation = true};
            _deleteWorker.DoWork += DeleteTask;
            _deleteWorker.RunWorkerAsync();

            _updateTimer = new Timer {Interval = 500};
            _updateTimer.Tick += (o, args) =>
            {
                lock (progressBarDelete)
                {
                    progressBarDelete.Value = _progressValue;
                    labelDeletingFile.Text = "Deleting " + _fileName;
                }
            };
            _updateTimer.Start();
        }

        private void RunUI(Action action)
        {
            Invoke(action);
        }

        private int _progressValue;
        private string _fileName;
        private Timer _updateTimer;

        private void DeleteTask(object sender, EventArgs eventArgs)
        {
            for (int i = 0; i < _allFiles.Length && !_deleteWorker.CancellationPending; i++)
            {
                var file = _allFiles[i];
                var index = i;

                var fileParts = file.Split('\\');
                var fileDisplay = fileParts.Length > 3
                    ? "...\\" + fileParts[fileParts.Length - 3] + "\\" + fileParts[fileParts.Length - 2] + "\\" + fileParts[fileParts.Length - 1]
                    : file;
                lock (progressBarDelete)
                {
                    _progressValue = index;
                    _fileName = fileDisplay;
                }

                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);   // Protect against failing on read-only files
                    Try.Multi<Exception>(() => File.Delete(file));
                }
                catch (Exception)
                {
                    bool retry = false;
                    RunUI(() =>
                    {
                        retry = MessageBox.Show(this, "Can't delete " + file, "File busy",
                            MessageBoxButtons.RetryCancel) == DialogResult.Retry;
                        if (!retry)
                        {
                            _updateTimer.Stop();
                            Close();
                        }
                    });
                    if (!retry)
                        return;
                    i--;
                }
            }

            while (!_deleteWorker.CancellationPending)
            {
                try
                {
                    Try.Multi<Exception>(() => Directory.Delete(_deletePath, true));
                    break;
                }
                catch (IOException ex)
                {
                    bool retry = false;
                    RunUI(() =>
                    {
                        retry = MessageBox.Show(this, ex.Message, "Folder busy",
                            MessageBoxButtons.RetryCancel) == DialogResult.Retry;
                    });
                    if (!retry)
                        break;
                }
            }

            _updateTimer.Stop();
            RunUI(Close);
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            _deleteWorker.CancelAsync();
        }
    }
}
