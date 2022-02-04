using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MSStatArgsCollector;
// ReSharper disable LocalizableElement

namespace TestHarness
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog
            {
                Filter = "CSV Files|*.csv|All files|*.*",

            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    tbxCsvFile.Text = dlg.FileName;
                }
            }
        }

        private void btnGroupComparison_Click(object sender, EventArgs e)
        {
            using (var reader = new StreamReader(tbxCsvFile.Text))
            {
                var newArgs = MSstatsGroupComparisonCollector.CollectArgs(this, reader, Arguments.ToArray());
                if (newArgs != null)
                {
                    Arguments = newArgs;
                }
            }
        }

        public IEnumerable<string> Arguments
        {
            get
            {
                var arguments = tbxOutput.Text.Split(new[] {Environment.NewLine}, StringSplitOptions.None);
                return string.IsNullOrEmpty(arguments[arguments.Length - 1])
                    ? arguments.Take(arguments.Length - 1)
                    : arguments;
            }
            set
            {
                tbxOutput.Text = string.Join(Environment.NewLine, value) + Environment.NewLine;
            }
        }

        private void btnQualityControl_Click(object sender, EventArgs e)
        {
            using (var reader = new StreamReader(tbxCsvFile.Text))
            {
                var newArgs = MSstatsQualityControlCollector.CollectArgs(this, reader, Arguments.ToArray());
                if (newArgs != null)
                {
                    Arguments = newArgs;
                }
            }
        }

        private void btnDesignSampleSize_Click(object sender, EventArgs e)
        {
            using (var reader = new StreamReader(tbxCsvFile.Text))
            {
                var newArgs = MSstatsSampleSizeCollector.CollectArgs(this, reader, Arguments.ToArray());
                if (newArgs != null)
                {
                    Arguments = newArgs;
                }
            }

        }
    }
}
