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
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Skyline.Model;

namespace pwiz.Skyline.ToolsUI
{
    
    public static class PythonInstallerUI
    {
        private static string _userAnswerToCuda;
        public static MultiButtonMsgDlg EnableNvidiaGpuDlg { get; set; }

        private static IList<PythonTaskBase> _tasks;
        public static DialogResult InstallPythonVirtualEnvironment(Control parent, PythonInstaller pythonInstaller)
        {
            var result = DialogResult.OK;
            
            _tasks = new List<PythonTaskBase>(pythonInstaller.PendingTasks.IsNullOrEmpty()
                ? pythonInstaller.ValidatePythonVirtualEnvironment()
                : pythonInstaller.PendingTasks);

            _userAnswerToCuda = null;
            pythonInstaller.NumTotalTasks = _tasks.Count;
            pythonInstaller.NumCompletedTasks = 0;
            List<PythonTaskBase> abortedTasks = new List<PythonTaskBase>();
            bool abortTask = false;
            foreach (var task in _tasks)
            {
                try
                {
                    if (task.TaskName == PythonTaskName.setup_nvidia_libraries)
                    {
                        if (_userAnswerToCuda != @"No")
                        {
                            var choice = DialogResult.None;
                            EnableNvidiaGpuDlg = new MultiButtonMsgDlg(string.Format(ToolsUIResources.PythonInstaller_Install_Cuda_Library), DialogResult.Yes.ToString(), DialogResult.No.ToString(), true);

                            choice = EnableNvidiaGpuDlg.ShowDialog();
                            if (choice == DialogResult.No)
                            {
                                _userAnswerToCuda = @"No";
                                if (pythonInstaller.NumTotalTasks > 0) pythonInstaller.NumTotalTasks--;
                                abortTask = true;
                            }
                            else if (choice == DialogResult.Cancel)
                            {
                                if (!EnableNvidiaGpuDlg.IsDisposed) EnableNvidiaGpuDlg.Dispose();
                                if (pythonInstaller.NumTotalTasks > 0) pythonInstaller.NumTotalTasks--;
                                return choice;
                            }
                            else if (choice == DialogResult.Yes)
                            {
                                _userAnswerToCuda = @"Yes";
                                pythonInstaller.WriteInstallNvidiaBatScript();
                               
                                MultiButtonMsgDlg adminMultiButtonMsgDlg = 
                                    new MultiButtonMsgDlg(
                                        string.Format(ModelResources.NvidiaInstaller_Requesting_Administrator_elevation,
                                            PythonInstaller.InstallNvidiaLibrariesBat), ModelResources.NvidiaInstaller_Install);
                                
                                //if (!PythonInstaller.IsRunningElevated())
                                 //   adminMessageDlg.FindButton(DialogResult.OK).Enabled = false;

                                var nvidiaAdminChoice = adminMultiButtonMsgDlg.ShowDialog();
                                //Download
                                if (nvidiaAdminChoice == DialogResult.Cancel)
                                {
                                    _userAnswerToCuda = @"Cancel";
                                    if (!adminMultiButtonMsgDlg.IsDisposed) adminMultiButtonMsgDlg.Dispose();
                                    if (!EnableNvidiaGpuDlg.IsDisposed) EnableNvidiaGpuDlg.Dispose();
                                    if (pythonInstaller.NumTotalTasks > 0) pythonInstaller.NumTotalTasks--;
                                    return nvidiaAdminChoice;
                                }
                                else if (nvidiaAdminChoice == DialogResult.OK)
                                {
                                    // Attempt to run
                                    abortTask = !PerformTaskAction(parent, task);
                                }
                                if (!adminMultiButtonMsgDlg.IsDisposed) adminMultiButtonMsgDlg.Dispose();
                            }
                            if (!EnableNvidiaGpuDlg.IsDisposed) EnableNvidiaGpuDlg.Dispose();

                        }
                        else if (_userAnswerToCuda == @"No")
                        {
                            if (pythonInstaller.NumTotalTasks > 0) pythonInstaller.NumTotalTasks--;
                            abortTask = true;
                        }
                        else
                        {
                            abortTask = !PerformTaskAction(parent, task);
                        }
                    }
                    else if (task.TaskName == PythonTaskName.enable_longpaths)
                    {
                        AlertDlg adminMessageDlg =
                            new AlertDlg(string.Format(ToolsUIResources.PythonInstaller_Requesting_Administrator_elevation), MessageBoxButtons.OKCancel);
                        var choice = adminMessageDlg.ShowDialog();
                        if (choice == DialogResult.Cancel)
                        {
                            if (!adminMessageDlg.IsDisposed) adminMessageDlg.Dispose();
                            if (pythonInstaller.NumTotalTasks > 0) pythonInstaller.NumTotalTasks--;
                            return choice;
                        }
                        else if (choice == DialogResult.OK)
                        {
                            // Attempt to enable Windows Long Paths
                            pythonInstaller.EnableWindowsLongPaths();
                        }
                        if (!adminMessageDlg.IsDisposed) adminMessageDlg.Dispose();
                    }
                    else 
                    {
                        abortTask = !PerformTaskAction(parent,task);
                    }

                    if (!abortTask)
                    {
                        pythonInstaller.NumCompletedTasks++;
                    }
                    else
                    {
                        abortedTasks.Add(task);
                        if (_userAnswerToCuda == @"No" && abortedTasks.Count == 1)
                            return DialogResult.No;
                        return DialogResult.Cancel;
                    }
                }
                catch (Exception ex)
                {
                    MessageDlg.ShowWithException(parent, (ex.InnerException ?? ex).Message, ex);
                    return DialogResult.Cancel;
                }
            }
            Debug.WriteLine($@"total: {pythonInstaller.NumTotalTasks}, completed: {pythonInstaller.NumCompletedTasks}");
            
            if (_resultAlertDlg != null && ! _resultAlertDlg.IsDisposed) _resultAlertDlg.Dispose();

            pythonInstaller.CheckPendingTasks();

            if (pythonInstaller.HavePythonTasks)
            {
                if (pythonInstaller.IsPythonVirtualEnvironmentReady(abortedTasks))
                {
                    if (pythonInstaller.NumTotalTasks == pythonInstaller.NumCompletedTasks &&
                        pythonInstaller.NumCompletedTasks > 0)
                    {
                        _resultAlertDlg =
                            new AlertDlg(
                                ToolsUIResources
                                    .PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment,
                                MessageBoxButtons.OK);
                        _resultAlertDlg.ShowDialog();
                    }

                    pythonInstaller.PendingTasks.Clear();
                    result = DialogResult.OK;
                }
                else if (!pythonInstaller.IsPythonVirtualEnvironmentReady())
                {
                    _resultAlertDlg =
                        new AlertDlg(
                            ToolsUIResources.PythonInstaller_OkDialog_Failed_to_set_up_Python_virtual_environment,
                            MessageBoxButtons.OK);
                    _resultAlertDlg.ShowDialog();
                    result = DialogResult.Cancel;
                }
                else
                {
                    result = DialogResult.OK;
                }
            }

            if (pythonInstaller.HaveNvidiaTasks)
            {
                if (PythonInstaller.TestForNvidiaGPU() == true)
                {
                    if (_userAnswerToCuda == @"Yes")
                    {
                        if (pythonInstaller.IsNvidiaEnvironmentReady(abortedTasks))
                        {
                            _resultAlertDlg =
                                new AlertDlg(
                                    ToolsUIResources.NvidiaInstaller_OkDialog_Successfully_set_up_Nvidia,
                                    MessageBoxButtons.OK);
                            _resultAlertDlg.ShowDialog();
                            pythonInstaller.PendingTasks.Clear();
                            result = DialogResult.OK;
                        }
                        else
                        {
                            _resultAlertDlg =
                                new AlertDlg(
                                    ToolsUIResources.NvidiaInstaller_OkDialog_Failed_to_set_up_Nvidia,
                                    MessageBoxButtons.OK);
                            _resultAlertDlg.ShowDialog();
                        }
                    }
                }
            }

            Dispose();
            return result;
        }
        public static void Dispose()
        {
            if (_resultAlertDlg != null && !_resultAlertDlg.IsDisposed)
            {
                _resultAlertDlg.Dispose();
            }
        }
        private static AlertDlg _resultAlertDlg;
        private static bool PerformTaskAction(Control parent, PythonTaskBase task, int startProgress = 0)
        {
            //IProgressStatus proStatus = null;
            using var waitDlg = new LongWaitDlg();
            waitDlg.Message = task.InProgressMessage();
            waitDlg.PerformWork(parent, 50, progressMonitor => task.DoAction(progressMonitor));
            return !waitDlg.IsCanceled;
        }
    }
}
