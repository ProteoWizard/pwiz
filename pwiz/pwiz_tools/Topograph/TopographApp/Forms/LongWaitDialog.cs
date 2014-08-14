/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.Windows.Forms;
using pwiz.Topograph.Util;
using pwiz.Topograph.ui.Properties;

namespace pwiz.Topograph.ui.Forms
{
    public partial class LongWaitDialog : Form, ILongOperationUi
    {
        private bool _closed;
        public LongWaitDialog(IWin32Window parentWindow, String title)
        {
            InitializeComponent();
            Icon = Resources.TopographIcon;
            ParentWindow = parentWindow;
            Text = title;
        }

        public LongOperationBroker LongOperationBroker
        {
            get; private set;
        }

        public IWin32Window ParentWindow { get; set; }
        
        public void DisplayLongOperationUi(LongOperationBroker broker)
        {
            LongOperationBroker = broker;
            Timer1OnTick(timer1, new EventArgs());
            ShowDialog(ParentWindow);
        }

        public void UpdateLongOperationUi()
        {
        }

        public void LongOperationEnded()
        {
            try
            {
                if (IsHandleCreated && !_closed)
                {
                    BeginInvoke(new Action(Close));
                }
            }
            catch (Exception exception)
            {
                Trace.TraceWarning("Exception:{0}", exception);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (!LongOperationBroker.Cancel())
            {
                e.Cancel = true;
                return;
            }
            _closed = true;
            LongOperationBroker.WaitUntilFinished();
        }

        private void BtnCancelOnClick(object sender, EventArgs e)
        {
            Close();
        }

        private void Timer1OnTick(object sender, EventArgs e)
        {
            if (LongOperationBroker != null)
            {
                tbxMessage.Text = LongOperationBroker.StatusMessage;
                btnCancel.Enabled = LongOperationBroker.IsCancellable && !LongOperationBroker.WasCancelled;
            }
        }
    }
}
