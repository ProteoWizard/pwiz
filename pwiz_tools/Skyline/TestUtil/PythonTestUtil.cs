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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using System;
using System.Management;
using System.Security.Principal;

namespace pwiz.SkylineTestUtil
{ public class PythonTestUtil
    {
        private string _pythonVersion
        {
            get; set;
        }
        private string _toolName
        {
            get; set;
        }

        private bool _undoRegistry;
        public static bool IsRunningElevated()
        {
            // Get current user's Windows identity
            WindowsIdentity identity = WindowsIdentity.GetCurrent();

            // Convert identity to WindowsPrincipal to check for roles
            WindowsPrincipal principal = new WindowsPrincipal(identity);

            // Check if the current user is in the Administrators role
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        public bool HaveNvidiaGPU()
        {
            bool nvidiaGpu = false;
            try
            {
                // Query for video controllers using WMI
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"SELECT * FROM Win32_VideoController");

                foreach (ManagementObject obj in searcher.Get())
                {
                    //  GPU information
                    nvidiaGpu = obj[@"Name"].ToString().StartsWith(@"NVIDIA");
                    if (nvidiaGpu) break;
                }
            }
            catch (ManagementException e)
            {
                Console.WriteLine(@"An error occurred while querying for WMI data: " + e.Message);
            }
            return nvidiaGpu;
        }

        public PythonTestUtil(string pythonVersion, string toolName)
        {
            _pythonVersion = pythonVersion;
            _toolName = toolName;

        }

        public void CancelPython(BuildLibraryDlg buildLibraryDlg)
        {
            // Test the control path where Python is not installed, and the user is prompted to deal with admin access
            PythonInstaller.SimulatedInstallationState = PythonInstaller.eSimulatedInstallationState.NAIVE; // Simulates not having the needed registry settings
            var installPythonDlg = AbstractFunctionalTest.ShowDialog<MultiButtonMsgDlg>(() => buildLibraryDlg.OkWizardPage()); // Expect the offer to install Python
            //PauseTest("install offer");
            AssertEx.AreComparableStrings(ToolsUIResources.PythonInstaller_BuildPrecursorTable_Python_0_installation_is_required, installPythonDlg.Message);
            AbstractFunctionalTest.CancelDialog(installPythonDlg, installPythonDlg.CancelDialog); // Cancel it immediately
            //PauseTest("back to wizard");
            installPythonDlg = AbstractFunctionalTest.ShowDialog<MultiButtonMsgDlg>(() => buildLibraryDlg.OkWizardPage()); // Expect the offer to install Python
            // PauseTest("install offer again");
            AssertEx.AreComparableStrings(ToolsUIResources.PythonInstaller_BuildPrecursorTable_Python_0_installation_is_required, installPythonDlg.Message);
            var needAdminDlg = AbstractFunctionalTest.ShowDialog<MessageDlg>(installPythonDlg.OkDialog); // Expect to be told about needing admin access
            // PauseTest("need admin msg");
            AssertEx.AreComparableStrings(ToolsUIResources.PythonInstaller_Requesting_Administrator_elevation, needAdminDlg.Message);
            AbstractFunctionalTest.CancelDialog(needAdminDlg, needAdminDlg.CancelDialog);
            // PauseTest("back to wizard");
        }
        public bool InstallPython(BuildLibraryDlg buildLibraryDlg)
        {
            PythonInstaller.SimulatedInstallationState = PythonInstaller.eSimulatedInstallationState.NONE; // Normal tests systems will have registry set suitably
     
            bool havePythonPrerequisite = false;
            AbstractFunctionalTest.RunUI(() => { havePythonPrerequisite = buildLibraryDlg.PythonRequirementMet(); });

            if (!havePythonPrerequisite)
            {
                MessageDlg confirmDlg = null;
                MultiButtonMsgDlg nvidiaDlg = null;
                AbstractFunctionalTest.RunLongDlg<MultiButtonMsgDlg>(buildLibraryDlg.OkWizardPage, pythonDlg =>
                {
                    Assert.AreEqual(string.Format(ToolsUIResources.PythonInstaller_BuildPrecursorTable_Python_0_installation_is_required,
                            _pythonVersion, _toolName), pythonDlg.Message);

                    if (!PythonInstallerTaskValidator.ValidateEnableLongpaths())
                    {
                        MessageDlg longPathDlg = AbstractFunctionalTest.ShowDialog<MessageDlg>(pythonDlg.OkDialog);

                        Assert.AreEqual(
                            string.Format(ToolsUIResources.PythonInstaller_Requesting_Administrator_elevation),
                            longPathDlg.Message);

                        if (IsRunningElevated())
                        {
                            if (PythonInstaller.TestForNvidiaGPU() == true && !PythonInstaller.NvidiaLibrariesInstalled())
                            {
                                Console.WriteLine(@"Info: NVIDIA GPU DETECTED on test node");

                                nvidiaDlg = AbstractFunctionalTest.ShowMultiButtonMsgDlg(pythonDlg.OkDialog, ToolsUIResources.PythonInstaller_Install_Cuda_Library);

                                RunNvidiaDialog(nvidiaDlg);
                            }
                            else
                            {
                                if (PythonInstaller.TestForNvidiaGPU() != true) 
                                    Console.WriteLine(@"Info: NVIDIA GPU *NOT* DETECTED on test node");
                                else
                                    Console.WriteLine(@"Info: Nvidia libraries already installed");

                                confirmDlg = AbstractFunctionalTest.ShowDialog<MessageDlg>(RunLongPathsDialog(longPathDlg).OkDialog);
                                ConfirmPythonSuccess(confirmDlg);

                            }
                        }
                        else
                        {
                            Assert.Fail($@"Error: Cannot finish {_toolName}BuildLibraryTest because {PythonInstaller.REG_FILESYSTEM_KEY}\{PythonInstaller.REG_LONGPATHS_ENABLED} is not set and have insufficient permissions to set it");
                        }
                    }
                    else
                    {
                        Console.WriteLine(@"Info: LongPathsEnabled registry key is already set to 1");


                        if (PythonInstaller.TestForNvidiaGPU() == true && !PythonInstaller.NvidiaLibrariesInstalled())
                        {
                            Console.WriteLine(@"Info: NVIDIA GPU DETECTED on test node");
                            
                            nvidiaDlg = AbstractFunctionalTest.ShowMultiButtonMsgDlg(pythonDlg.OkDialog, ToolsUIResources.PythonInstaller_Install_Cuda_Library);

                            RunNvidiaDialog(nvidiaDlg);
                        }
                        else
                        {
                            if (PythonInstaller.TestForNvidiaGPU() != true)
                            {
                                Console.WriteLine(@"Info: NVIDIA GPU *NOT* DETECTED on test node");
                            }
                            else
                            {
                                Console.WriteLine(@"Info: Nvidia libraries already installed");
                            }
                            confirmDlg = AbstractFunctionalTest.ShowDialog<MessageDlg>(pythonDlg.OkDialog, 600000);
                            ConfirmPythonSuccess(confirmDlg);
                        }
                    }
         

                }, dlg => {
                    dlg.Close();
                });
                if (_undoRegistry)
                {
                    PythonInstallerTaskValidator.DisableWindowsLongPaths();
                }

                return true;
            }

            return false;
        }

        public bool HavePythonPrerequisite(BuildLibraryDlg buildLibraryDlg)
        {
            bool havePythonPrerequisite = false;
            AbstractFunctionalTest.RunUI(() => { havePythonPrerequisite = buildLibraryDlg.PythonRequirementMet(); });
            return havePythonPrerequisite;
        }

        private void RunNvidiaDialog(MultiButtonMsgDlg nvidiaDlg)
        {
            // Running as non-admin, say 'No'
            MessageDlg confirmDlg = AbstractFunctionalTest.ShowDialog<MessageDlg>(nvidiaDlg.ClickNo, 600000);
            ConfirmPythonSuccess(confirmDlg);
    
            if (!nvidiaDlg.IsDisposed)
                nvidiaDlg.Dispose();
        }
        private MessageDlg RunLongPathsDialog(MessageDlg longPathDlg)
        {
            Console.WriteLine(@"Info: Trying to set LongPathsEnabled registry key to 1");
            AbstractFunctionalTest.OkDialog(longPathDlg, longPathDlg.OkDialog);
            
            MessageDlg okDlg = AbstractFunctionalTest.ShowDialog<MessageDlg>(longPathDlg.OkDialog);
            
            Console.WriteLine(@"Info: Successfully set LongPathsEnabled registry key to 1");
            _undoRegistry = true;

            if (!longPathDlg.IsDisposed)
                longPathDlg.Dispose();

            return okDlg;
        }

        private void ConfirmPythonSuccess(MessageDlg confirmDlg)
        {
            Assert.AreEqual(string.Format(ToolsUIResources.PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment),
                confirmDlg.Message);
            AbstractFunctionalTest.OkDialog(confirmDlg, confirmDlg.OkDialog);
            if (!confirmDlg.IsDisposed)
                confirmDlg.Dispose();
        }
    }
}
