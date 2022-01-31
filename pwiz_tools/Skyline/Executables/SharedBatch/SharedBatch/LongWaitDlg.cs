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
        private bool _loaded;
        private int _percent;

        public LongWaitDlg(Form parent, string programName, string labelText)
        {
            InitializeComponent();
            Icon = parent.Icon;
            ParentForm = parent;
            progressBar.Style = ProgressBarStyle.Continuous;
            label1.Text = labelText;
            CancelToken = new CancellationTokenSource();
            Location = new Point(parent.Location.X + parent.Size.Width / 2 - Width / 2,
                parent.Location.Y + parent.Size.Height / 2 - Height / 2);

            _timer = new Timer { Interval = 10 };
            _timer.Tick += UpdateProgress;
            _timer.Start();

            Shown += ((sender, args) =>
            {
                Text = programName;
                progressBar.Value = _percent;
                _loaded = true;
            });
        }

        public new Form ParentForm { get; private set; }

        // for testing only
        public LongWaitDlg()
        {
            InitializeComponent();
            CancelToken = new CancellationTokenSource();
        }

        public bool Cancelled { get; private set; }
        public bool Completed { get; private set; }

        public readonly CancellationTokenSource CancelToken;
        
        private void UpdateProgress(object sender, EventArgs e)
        {
            if (!_loaded) return;
            if (progressBar.Value < Math.Min(_maxPercent, 99)) progressBar.Value++;
            if (progressBar.Value < _percent) progressBar.Value = _percent;
        }

        public void UpdateProgress(int percentComplete, int maxPercent)
        {
            if (Cancelled)
                return;
            _maxPercent = Math.Min(maxPercent, 100);
            _percent = percentComplete;
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
            try
            {
                Invoke(new Action(() =>
                {
                    if (Visible) Close();
                }));
            }
            catch (ObjectDisposedException)
            {
                // pass
            }
        }
    }
}
