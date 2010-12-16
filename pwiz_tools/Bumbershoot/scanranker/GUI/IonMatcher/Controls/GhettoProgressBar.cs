using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace IonMatcher
{
    public class GhettoProgressControl : UserControl
    {

        public Form form;
        public ProgressBar ghettoProgressBar;

        public GhettoProgressControl(int steps)
        {
            form = new Form();
            form.SizeGripStyle = SizeGripStyle.Show;
            form.ShowInTaskbar = true;
            form.TopLevel = true;
            form.TopMost = true;
            form.AutoSize = true;
            form.AutoSizeMode = AutoSizeMode.GrowOnly;
            form.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.MaximizeBox = false;
            form.MinimizeBox = true;
            form.Text = "Progress...";
            form.Size = new System.Drawing.Size(450, 50);

            ghettoProgressBar = new ProgressBar();
            ghettoProgressBar.Dock = DockStyle.Fill;
            ghettoProgressBar.Style = ProgressBarStyle.Continuous;
            ghettoProgressBar.Step = 1;
            ghettoProgressBar.Minimum = 0;
            ghettoProgressBar.Maximum = steps;
            ghettoProgressBar.Value = 0;
            form.Controls.Add(ghettoProgressBar);
            form.Show();

            Application.DoEvents();

        }

        public void showProgress(string text)
        {
            form.Text = text;
            ghettoProgressBar.PerformStep();
            Application.DoEvents();
        }

        public void updateMax(int max)
        {
            ghettoProgressBar.Maximum = max;
            Application.DoEvents();
        }

        public int getMax() 
        {
            return ghettoProgressBar.Maximum;
        }
    }
}
