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
