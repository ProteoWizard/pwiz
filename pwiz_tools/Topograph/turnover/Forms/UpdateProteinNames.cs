using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using pwiz.Topograph.Fasta;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.ui.Forms
{
    public partial class UpdateProteinNames : WorkspaceForm
    {
        private bool _running;
        private bool _canceled;
        private String _statusText;
        private int _progress;
        public UpdateProteinNames(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _canceled = true;
            while (true)
            {
                lock(this)
                {
                    if (!_running)
                    {
                        return;
                    }
                    Monitor.Wait(this);
                }
            }
        }

        protected override void OnShown(EventArgs e)
        {
            _running = true;
            new Action(DoWork).BeginInvoke(null, null);
        }

        public String FastaFilePath { get; set; }

        private void DoWork()
        {
            try
            {
                var fastaImporter = new FastaUpdater();
                _statusText = "Reading FASTA file";
                fastaImporter.ReadFasta(File.OpenText(FastaFilePath));
                fastaImporter.Update(Workspace, UpdateProgress);
                if (!IsDisposed)
                {
                    BeginInvoke(new Action(Close));
                }
            }
            finally
            {
                lock(this)
                {
                    _running = false;
                    Monitor.PulseAll(this);
                }
            }
        }

        private bool UpdateProgress(String status, int progress)
        {
            _statusText = status;
            _progress = progress;
            return !_canceled;
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            tbxStatus.Text = _statusText;
            progressBar.Value = _progress;
        }
    }
}
