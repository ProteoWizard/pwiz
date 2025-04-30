using System;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.ToolsUI
{
    public partial class PythonInstallerDlg : FormEx
    {
        private const string HYPHEN = TextUtil.HYPHEN;
        private const string SPACE = TextUtil.SPACE;

        private PythonInstaller Installer { get; }

        public PythonInstallerDlg(PythonInstaller installer)
        {
            InitializeComponent();
            Installer = installer;
            PopulateDlgText();
        }

        public void OkDialog()
        {
            var tasks = Installer.PendingTasks.IsNullOrEmpty() ?
                Installer.ValidatePythonVirtualEnvironment() : Installer.PendingTasks;
            Installer.NumTotalTasks = tasks.Count;
            Installer.NumCompletedTasks = 0;
            foreach (var task in tasks)
            {
                try
                {
                   using var waitDlg = new LongWaitDlg();
                   waitDlg.ProgressValue = 0;
                   waitDlg.Message = task.InProgressMessage();
                   waitDlg.PerformWork(this, 50, progressMonitor => task.DoAction(progressMonitor));
                   Installer.NumCompletedTasks++;
                }
                catch (Exception ex)
                {
                    MessageDlg.Show(this, task.FailureMessage());
                    MessageDlg.ShowWithException(this, (ex.InnerException ?? ex).Message, ex);
                    break;
                }
            }
            Debug.WriteLine($@"total: {Installer.NumTotalTasks}, completed: {Installer.NumCompletedTasks}");
            if (Installer.NumCompletedTasks == Installer.NumTotalTasks)
            {
                Installer.PendingTasks.Clear();
                MessageDlg.Show(this, ToolsUIResources.PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment);
                DialogResult = DialogResult.OK;
            }
            else
            {
                MessageDlg.Show(this, ToolsUIResources.PythonInstaller_OkDialog_Failed_to_set_up_Python_virtual_environment);
                DialogResult = DialogResult.Cancel;
            }
        }
        
        private void PopulateDlgText()
        {
            this.labelDescription.Text = $@"This tool requires Python {Installer.PythonVersion} and the following packages. A dedicated python virtual environment {Installer.VirtualEnvironmentName} will be created for the tool. Click the ""Install"" button to start installation.";
            var packagesStringBuilder = new StringBuilder();
            foreach (var package in Installer.PythonPackages)
            {
                packagesStringBuilder.Append(HYPHEN + SPACE + package.Name);
                if (package.Version != null)
                {
                    packagesStringBuilder.Append(SPACE + package.Version);
                }
                packagesStringBuilder.Append(Environment.NewLine);
            }
            this.textBoxPackages.Text = packagesStringBuilder.ToString();
        }

        private void btnInstall_Click(object sender, EventArgs e)
        {
            OkDialog();
        }
    }
}
