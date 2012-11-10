//
// $Id: LogForm.cs 48 2011-21-11 16:18:05Z holmanjd $
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
using System.Windows.Forms;

namespace BumberDash.Forms
{
    public partial class LogForm : Form
    {
        private bool _actuallyClose;

        public LogForm()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, EventArgs e)
        {
            Hide();
        }

        private void LogForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_actuallyClose)
            {
                e.Cancel = true;
                Hide();
            }
        }

        internal void FullyClose()
        {
            _actuallyClose = true;
            Close();
        }

        private void PauseButton_Click(object sender, EventArgs e)
        {
            if (PauseButton.Text == "Pause Log")
            {
                pausedLogText.AppendText(logText.Text);
                pausedLogText.Visible = true;
                ScrollToBottom();
                logText.Visible = false;
                PauseButton.Text = "Resume Log";
            }
            else
            {
                pausedLogText.Clear();
                logText.Visible = true;
                ScrollToBottom();
                pausedLogText.Visible = false;
                PauseButton.Text = "Pause Log";
            }
        }

        internal void ScrollToBottom()
        {
            if (pausedLogText.Visible)
            {
                pausedLogText.SelectionStart = pausedLogText.Text.Length;
                pausedLogText.ScrollToCaret();
            }
            if (logText.Visible)
            {
                logText.SelectionStart = logText.Text.Length;
                logText.ScrollToCaret();
            }
        }
    }
}
