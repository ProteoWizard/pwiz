/*
 * Author: David Shteynberg <dshteyn .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Tools;
using System;
using System.Diagnostics;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Skyline.Model;

namespace pwiz.Skyline.ToolsUI
{
    
    public static class PythonInstallerUI
    {
        private static bool? _userNoToCuda;
        public static MultiButtonMsgDlg EnableNvidiaGpuDlg { get; set; }
        public static DialogResult InstallPythonVirtualEnvironment(Control parent, PythonInstaller pythonInstaller)
        {
            DialogResult result;
            var tasks = pythonInstaller.PendingTasks.IsNullOrEmpty() ? pythonInstaller.ValidatePythonVirtualEnvironment() : pythonInstaller.PendingTasks;
            _userNoToCuda = null;
            pythonInstaller.NumTotalTasks = tasks.Count;
            pythonInstaller.NumCompletedTasks = 0;
            bool abortTask = false;
            foreach (var task in tasks)
            {
                try
                {
                    if (task.Name == PythonTaskName.download_cuda_library || task.Name == PythonTaskName.install_cuda_library ||
                        task.Name == PythonTaskName.download_cudnn_library || task.Name == PythonTaskName.install_cudnn_library)
                    {
                        if (_userNoToCuda != true)
                        {
                            var choice = DialogResult.None;
                            EnableNvidiaGpuDlg = new MultiButtonMsgDlg(string.Format(ToolsUIResources.PythonInstaller_Install_Cuda_Library), DialogResult.Yes.ToString(), DialogResult.No.ToString(), true);
                            choice = EnableNvidiaGpuDlg.ShowDialog();
                            if (choice == DialogResult.No)
                            {
                                _userNoToCuda = true;
                                if (pythonInstaller.NumTotalTasks > 0) pythonInstaller.NumTotalTasks--;
                                abortTask = true;
                            }
                            else if (choice == DialogResult.Cancel)
                            {
                                if (pythonInstaller.NumTotalTasks > 0) pythonInstaller.NumTotalTasks--;
                                return choice;
                            }
                            else if (choice == DialogResult.Yes)
                            {
                                _userNoToCuda = false;
                                pythonInstaller.WriteInstallNvidiaBatScript();
                                var nvidiaChoice = MessageDlg.Show(parent, string.Format(ModelResources.NvidiaInstaller_Requesting_Administrator_elevation, PythonInstaller.InstallNvidiaLibrariesBat), false, MessageBoxButtons.OKCancel);
                                //Download
                                if (nvidiaChoice == DialogResult.Cancel)
                                {
                                    _userNoToCuda = true;
                                    if (pythonInstaller.NumTotalTasks > 0) pythonInstaller.NumTotalTasks--;
                                }
                                else if (nvidiaChoice == DialogResult.OK)
                                {
                                    // Attempt to run
                                    abortTask = !PerformTaskAction(parent, task);
                                }
                            }
                        }
                        else if (_userNoToCuda == true)
                        {
                            if (pythonInstaller.NumTotalTasks > 0) pythonInstaller.NumTotalTasks--;
                            abortTask = true;
                        }
                        else
                        {
                            abortTask = !PerformTaskAction(parent, task);
                        }
                    }
                    else if (task.Name == PythonTaskName.enable_longpaths)
                    {
                        var choice = MessageDlg.Show(parent, string.Format(ToolsUIResources.PythonInstaller_Requesting_Administrator_elevation), false, MessageBoxButtons.OKCancel);
                        if (choice == DialogResult.Cancel)
                        {
                            if (pythonInstaller.NumTotalTasks > 0) pythonInstaller.NumTotalTasks--;
                            return choice;
                        }
                        else if (choice == DialogResult.OK)
                        {
                            // Attempt to enable Windows Long Paths
                            pythonInstaller.EnableWindowsLongPaths();
                        }
                    }
                    else if (task.IsAction)
                    {
                        abortTask = !PerformTaskAction(parent,task);
                    }
                    else
                    {
                        throw new PythonInstallerUnsupportedTaskException(task);
                    }
                    if (!abortTask)
                        pythonInstaller.NumCompletedTasks++;
                }
                catch (Exception ex)
                {
                    MessageDlg.ShowWithException(parent, (ex.InnerException ?? ex).Message, ex);
                    return DialogResult.Cancel;
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

        private static bool PerformTaskAction(Control parent, PythonTask task, int startProgress = 0)
        {
            //IProgressStatus proStatus = null;
            using var waitDlg = new LongWaitDlg();
    
            if (task.IsActionWithNoArg)
            {
                waitDlg.Message = task.InProgressMessage;
                waitDlg.PerformWork(parent, 50, task.AsActionWithNoArg);
            }
            else if (task.IsActionWithProgressMonitor)
            {
                waitDlg.Message = task.InProgressMessage;
                waitDlg.ProgressValue = startProgress;
                waitDlg.PerformWork(parent, 50, task.AsActionWithProgressMonitor);
            }
            else
            {
                waitDlg.Message = task.InProgressMessage;
                waitDlg.PerformWork(parent, 50, task.AsActionWithLongWaitBroker);
            }   
                
            //if (proStatus != null && proStatus.IsCanceled)
            //    return false;
            
            return !waitDlg.IsCanceled;
        }
    }
}
