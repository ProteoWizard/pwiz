using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Tools;
using System;
using System.Diagnostics;
using System.Windows.Forms;
using pwiz.Common.Collections;


namespace pwiz.Skyline.ToolsUI
{
    
    public static class PythonInstallerUI
    {
        public static MultiButtonMsgDlg EnableLongPathsDlg { get; set; }
        public static DialogResult InstallPythonVirtualEnvironment(Control parent, PythonInstaller pythonInstaller)
        {
            DialogResult result;
            var tasks = pythonInstaller.PendingTasks.IsNullOrEmpty() ? pythonInstaller.ValidatePythonVirtualEnvironment() : pythonInstaller.PendingTasks;
            pythonInstaller.NumTotalTasks = tasks.Count;
            pythonInstaller.NumCompletedTasks = 0;
            foreach (var task in tasks)
            {
                try
                {
                    if (task.Name == PythonTaskName.enable_longpaths)
                    {
                        EnableLongPathsDlg = new MultiButtonMsgDlg(string.Format(ToolsUIResources.PythonInstaller_Enable_Windows_Long_Paths), DialogResult.Yes.ToString(), DialogResult.No.ToString(), true);
                        var choice = EnableLongPathsDlg.ShowDialog();
                        if (choice == DialogResult.No || choice == DialogResult.Cancel)
                        {
                            if (pythonInstaller.NumTotalTasks > 0) pythonInstaller.NumTotalTasks--;
                            break;
                        }
                        else if (choice == DialogResult.Yes)
                        {
                            // Attempt to enable Windows Long Paths
                            pythonInstaller.EnableWindowsLongPaths();
                        }
                    }
                    else if (task.IsActionWithNoArg)
                    {
                        using var waitDlg = new LongWaitDlg();
                        waitDlg.Message = task.InProgressMessage;
                        waitDlg.PerformWork(parent, 50, task.AsActionWithNoArg);
                    }
                    else if (task.IsActionWithProgressMonitor)
                    {
                        using var waitDlg = new LongWaitDlg();
                        waitDlg.ProgressValue = 0;
                        waitDlg.PerformWork(parent, 50, task.AsActionWithProgressMonitor);
                    }
                    else
                    {
                        throw new PythonInstallerUnsupportedTaskException(task);
                    }
                    pythonInstaller.NumCompletedTasks++;
                }
                catch (Exception ex)
                {
                    //MessageDlg.Show(parent, task.FailureMessage);
                    MessageDlg.ShowWithException(parent, (ex.InnerException ?? ex).Message, ex);
                    break;
                }
            }
            Debug.WriteLine($@"total: {pythonInstaller.NumTotalTasks}, completed: {pythonInstaller.NumCompletedTasks}");
            if (pythonInstaller.NumCompletedTasks == pythonInstaller.NumTotalTasks)
            {
                pythonInstaller.PendingTasks.Clear();
                MessageDlg.Show(parent, ToolsUIResources.PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment);
                result = DialogResult.OK;
            }
            else
            {
                MessageDlg.Show(parent, ToolsUIResources.PythonInstaller_OkDialog_Failed_to_set_up_Python_virtual_environment);
                result = DialogResult.Cancel;
            }
            return result;
        }
    }
}
