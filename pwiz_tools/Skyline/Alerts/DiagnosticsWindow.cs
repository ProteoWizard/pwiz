using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using JetBrains.Annotations;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Alerts
{
    public partial class DiagnosticsWindow : Form
    {
        public DiagnosticsWindow()
        {
            InitializeComponent();
        }

        private void btnMemoryUsage_Click(object sender, EventArgs e)
        {
            var process = Process.GetCurrentProcess();
            var lines = new[]
            {
                "Total memory: " + process.PrivateMemorySize64,
                "Managed memory: " + System.GC.GetTotalMemory(true),
            };
            AppendStatusLines(lines);
        }

        public void AppendStatusLines(IEnumerable<string> lines)
        {
            tbxOutput.AppendText(TextUtil.LineSeparate(lines) + Environment.NewLine);
        }

        [StringFormatMethod("message")]
        public void AppendStatusLine(string message, params object[] args)
        {
            string line;
            if (0 < args?.Length)
            {
                line = string.Format(message, args);
            }
            else
            {
                line = message;

            }
            AppendStatusLines(new []{line});
        }

        private void btnGC_Click(object sender, EventArgs e)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            stopwatch.Stop();
            AppendStatusLine("Garbage collection completed in {0}", stopwatch.Elapsed);
        }


    }
}
