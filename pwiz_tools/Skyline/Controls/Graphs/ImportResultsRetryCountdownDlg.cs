/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class ImportResultsRetryCountdownDlg : FormEx
    {
        private int _secondsRemaining;
        private readonly string _retryString;
        private readonly Action _retryAction;
        private readonly Action _cancelAction;
        private readonly Timer _timer;

        public ImportResultsRetryCountdownDlg(int retrySeconds, Action retryAction, Action cancelAction)
        {
            InitializeComponent();

            _secondsRemaining = retrySeconds;
            _retryString = lblSeconds.Text;
            UpdateCount();
            _retryAction = retryAction;
            _cancelAction = cancelAction;
            _timer = new Timer { Interval = 1000 };
            _timer.Tick += _timer_Tick;
            _timer.Start();
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer.Dispose();
        }

        private void _timer_Tick(object sender, EventArgs e)
        {
            if (--_secondsRemaining == 0)
                RetryNow();
            else
                UpdateCount();
        }

        private void UpdateCount()
        {
            lblSeconds.Text = string.Format(_retryString, _secondsRemaining);
        }

        public void RetryNow()
        {
            _timer.Stop();
            Close();
            _retryAction();
        }

        public void Cancel()
        {
            _timer.Stop();
            Close();
            _cancelAction();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Cancel();
        }

        private void btnRetryNow_Click(object sender, EventArgs e)
        {
            RetryNow();
        }
    }
}
