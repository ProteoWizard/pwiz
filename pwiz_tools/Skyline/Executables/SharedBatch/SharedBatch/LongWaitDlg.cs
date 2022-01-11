using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace SharedBatch
{
    public partial class LongWaitDlg : Form
    {

        private int _maxPercent;
        private Timer _timer;

        public LongWaitDlg(Form parent, string programName, string labelText)
        {
            InitializeComponent();
            Icon = parent.Icon;
            Text = programName;
            ParentForm = parent;
            progressBar.Style = ProgressBarStyle.Continuous;
            label1.Text = labelText;
            CancelToken = new CancellationTokenSource();
            Location = new Point(parent.Location.X + parent.Size.Width / 2 - Width / 2,
                parent.Location.Y + parent.Size.Height / 2 - Height / 2);
        }

        public Form ParentForm { get; private set; }

        // for testing only
        public LongWaitDlg()
        {
            InitializeComponent();
            CancelToken = new CancellationTokenSource();
        }

        public bool Cancelled { get; private set; }
        public bool Completed { get; private set; }

        public readonly CancellationTokenSource CancelToken;

        private void LongWaitDlg_Shown(object sender, EventArgs e)
        {
            _maxPercent = -1;
            _timer = new Timer { Interval = 10 };
            _timer.Tick += UpdateProgress;
            _timer.Start();

        }

        private void UpdateProgress(object sender, EventArgs e)
        {
            if (progressBar.Value < Math.Min(_maxPercent, 99)) progressBar.Value++;
        }

        public void UpdateProgress(int percentComplete, int maxPercent)
        {
            if (Cancelled) return;
            try
            {
                Invoke(new Action(() =>
                {
                    progressBar.Value = percentComplete;
                    _maxPercent = Math.Min(maxPercent, 100);
                }));
            }
            catch (InvalidOperationException)
            {
                if (!Cancelled) throw;
            }
        }

        private void LongWaitDlg_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!Completed)
            {
                Cancelled = true;
                CancelToken.Cancel();
            }
            _timer.Stop();
        }

        public void Finish()
        {
            Completed = true;
            Invoke(new Action(() =>
            {
                if (Visible) Close();
            }));
        }
    }
}
