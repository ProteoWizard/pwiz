using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace SkylineNightly
{
    public partial class WaitDialog : Form
    {
        private readonly BackgroundWorker _worker;
        private bool _canceled;

        public static bool Show(string title, int steps, Action<WaitDialog> longAction)
        {
            using (var wait = new WaitDialog(title, steps, longAction))
            {
                return wait.ShowDialog() != DialogResult.Cancel;
            }
        }

        private WaitDialog(string title, int steps, Action<WaitDialog> longAction)
        {
            InitializeComponent();
            Text = title;
            _worker = new BackgroundWorker {WorkerSupportsCancellation = true};
            _worker.DoWork += delegate
            {
                longAction(this);
                Invoke(new Action(Close));
            };
            _worker.RunWorkerAsync();
            progressBar.Maximum = steps < 1 ? 100 : steps;
            progressBar.Style = steps < 1 ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
        }

        public int Step
        {
            get { return progressBar.Step; }
            set { progressBar.Step = value; }
        }

        private void Done()
        {
            if (!_canceled)
                Close();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            _canceled = true;
            _worker.CancelAsync();
            Close();
        }
    }
}
