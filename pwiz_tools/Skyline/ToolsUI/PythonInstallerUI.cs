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
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Skyline.Model;

namespace pwiz.Skyline.ToolsUI
{
    public static class PythonInstallerUI
    {
        private static IList<PythonTaskBase> _tasks;
        public static bool InstallPythonVirtualEnvironment(Control parent, PythonInstaller pythonInstaller)
        {
            _tasks = new List<PythonTaskBase>(pythonInstaller.PendingTasks.IsNullOrEmpty()
                ? pythonInstaller.ValidatePythonVirtualEnvironment()
                : pythonInstaller.PendingTasks);

            var userAnswerToCuda = DialogResult.None;
            pythonInstaller.NumTotalTasks = _tasks.Count;
            pythonInstaller.NumCompletedTasks = 0;
            var abortedTasks = new List<PythonTaskBase>();
            var tasksToDo = new List<PythonTaskBase>();

            // First ask the user about installing NVIDIA and enabling long paths
            try
            {
                foreach (var task in _tasks)
                {
                    if (task.IsNvidiaTask)
                    {
                        // If not supposed to install NVIDIA just skip it
                        if (PythonInstaller.SimulatedInstallationState ==
                            PythonInstaller.eSimulatedInstallationState.NONVIDIAHARD ||
                            userAnswerToCuda == DialogResult.No)
                        {
                            if (pythonInstaller.NumTotalTasks > 0) 
                                pythonInstaller.NumTotalTasks--;
                        }
                        else
                        {
                            var choice = MessageDlg.Show(parent, string.Format(
                                    ToolsUIResources.PythonInstaller_Install_Nvidia_Library), false,
                                MessageBoxButtons.YesNoCancel);
                            if (choice == DialogResult.Cancel)
                                return false;
                            
                            if (choice == DialogResult.No)
                            {
                                userAnswerToCuda = DialogResult.No;
                                if (pythonInstaller.NumTotalTasks > 0)
                                    pythonInstaller.NumTotalTasks--;
                            }
                            else if (choice == DialogResult.Yes)
                            {
                                userAnswerToCuda = DialogResult.Yes;
                                PythonInstaller.WriteInstallNvidiaBatScript();
                                var nvidiaAdminChoice = MessageDlg.Show(parent, string.Format(
                                        ModelResources.NvidiaInstaller_Requesting_Administrator_elevation,
                                        PythonInstaller.InstallNvidiaLibrariesBat), false,
                                    MessageBoxButtons.OKCancel);

                                // Download
                                if (nvidiaAdminChoice == DialogResult.Cancel)
                                    return false;

                                // Attempt to run
                                tasksToDo.Add(task);
                            }
                        }
                    }
                    else if (task.IsEnableLongPathsTask)
                    {
                        var choice = MessageDlg.Show(parent,
                            ToolsUIResources.PythonInstaller_Requesting_Administrator_elevation, false,
                            MessageBoxButtons.OKCancel);
                        if (choice == DialogResult.Cancel)
                            return false;

                        // Attempt to enable Windows Long Paths
                        PythonInstaller.EnableWindowsLongPaths(true);
                    }
                    else
                    {
                        tasksToDo.Add(task);    // Add all the other tasks without asking
                    }
                }
            }
            catch (Exception ex)
            {
                MessageDlg.ShowWithException(parent, (ex.InnerException ?? ex).Message, ex);
                return false;
            }

            // Do the actual work
            bool continueInstallation = true;
            using var waitDlg = new LongWaitDlg();
            waitDlg.PerformWork(parent, 50, progressMonitor =>
            {
                foreach (var task in tasksToDo)
                {
                    waitDlg.Message = task.InProgressMessage;
                    waitDlg.ProgressValue = -1;
                    task.DoAction(progressMonitor);
                    if (!waitDlg.IsCanceled)
                    {
                        pythonInstaller.NumCompletedTasks++;
                    }
                    else
                    {
                        abortedTasks.Add(task);
                        if (userAnswerToCuda == DialogResult.No && abortedTasks.Count == 1)
                        {
                            continueInstallation = false;
                            return;
                        }

                        continueInstallation = false;
                        return;
                    }
                }
            });
            if (!continueInstallation)
                return false;

            // Debug.WriteLine($@"total: {pythonInstaller.NumTotalTasks}, completed: {pythonInstaller.NumCompletedTasks}");

            // Complicated logic below for choosing what message to show the user
            bool havePythonTasks = pythonInstaller.HavePythonTasks;
            bool haveNvidiaTasks = pythonInstaller.HaveNvidiaTasks;
            
            if (havePythonTasks)
            {
                if (!pythonInstaller.IsPythonVirtualEnvironmentReady(abortedTasks))
                {
                    MessageDlg.Show(parent, ToolsUIResources.PythonInstaller_OkDialog_Failed_to_set_up_Python_virtual_environment);
                    return false;
                }
                
                // Don't bother reporting the Python success, if it would be followed by NVIDIA failure,
                // but potentially show both success messages.
                if (pythonInstaller.NumTotalTasks == pythonInstaller.NumCompletedTasks &&
                    pythonInstaller.NumCompletedTasks > 0)
                {
                    MessageDlg.Show(parent, ToolsUIResources.PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment);
                    pythonInstaller.PendingTasks.Clear();
                }
            }

            if (haveNvidiaTasks)
            {
                if (PythonInstaller.TestForNvidiaGPU() == true && userAnswerToCuda == DialogResult.Yes)
                {
                    if (!pythonInstaller.IsNvidiaEnvironmentReady(abortedTasks))
                    {
                        MessageDlg.Show(parent, ToolsUIResources.NvidiaInstaller_OkDialog_Failed_to_set_up_Nvidia);
                        return false;
                    }

                    MessageDlg.Show(parent, ToolsUIResources.NvidiaInstaller_OkDialog_Successfully_set_up_Nvidia);
                    pythonInstaller.PendingTasks.Clear();
                }
            }
            return true;
        }
    }
}
