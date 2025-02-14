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
using pwiz.Skyline.Model;

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

        public const int WAIT_TIME = 15 * 60 * 1000;
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

        public PythonTestUtil(string pythonVersion, string toolName, bool cleanSlate = true)
        {
            _pythonVersion = pythonVersion;
            _toolName = toolName;
            if (cleanSlate)
                PythonInstaller.DeleteToolsPythonDirectory();
        }
        /// <summary>
        /// Cancels Python install
        /// </summary>
        /// <param name="buildLibraryDlg">Build Library dialog</param>
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
        /// <summary>
        /// Installs Python
        /// </summary>
        /// <param name="buildLibraryDlg">Build Library</param>
        /// <param name="nvidiaClickNo">true clicks No, false clicks Yes, null clicks Cancel to Nvidia Detected Dialog</param>
        /// <returns></returns>
        public bool InstallPython(BuildLibraryDlg buildLibraryDlg, bool? nvidiaClickNo = true)
        {
            PythonInstaller.SimulatedInstallationState = PythonInstaller.eSimulatedInstallationState.NONE; // Normal tests systems will have registry set suitably

            if (nvidiaClickNo == true)
                PythonInstaller.SimulatedInstallationState = PythonInstaller.eSimulatedInstallationState.NONVIDIA;

            bool havePythonPrerequisite = false;

            AbstractFunctionalTest.RunUI(() => { havePythonPrerequisite = buildLibraryDlg.PythonRequirementMet(); });

            if (!havePythonPrerequisite)
            {
                MessageDlg confirmDlg = null;
                AbstractFunctionalTest.RunLongDlg<MultiButtonMsgDlg>(buildLibraryDlg.OkWizardPage, pythonDlg =>
                {
                    Assert.AreEqual(string.Format(ToolsUIResources.PythonInstaller_BuildPrecursorTable_Python_0_installation_is_required,
                            _pythonVersion, _toolName), pythonDlg.Message);

                    if (!PythonInstallerTaskValidator.ValidateEnableLongpaths())
                    {
                        MessageDlg longPathDlg = AbstractFunctionalTest.ShowDialog<MessageDlg>(pythonDlg.OkDialog);

                        Assert.AreEqual(string.Format(ToolsUIResources.PythonInstaller_Requesting_Administrator_elevation),
                            longPathDlg.Message);

                        if (IsRunningElevated())
                        {
                            NvidiaTestHelper( pythonDlg, nvidiaClickNo );

                            if (nvidiaClickNo == false)
                            {
                                MultiButtonMsgDlg nvidiaDlg = AbstractFunctionalTest.ShowDialog<MultiButtonMsgDlg>(pythonDlg.OkDialog);

                                Assert.AreEqual(string.Format(ModelResources.NvidiaInstaller_Requesting_Administrator_elevation, PythonInstaller.GetInstallNvidiaLibrariesBat()),
                                    nvidiaDlg.Message);

                                confirmDlg = AbstractFunctionalTest.ShowDialog<MessageDlg>(nvidiaDlg.ClickYes, WAIT_TIME);
                                ConfirmPythonSuccess(confirmDlg);
                            }
                            else if (nvidiaClickNo == true)
                            {
                                MultiButtonMsgDlg nvidiaDlg = AbstractFunctionalTest.ShowDialog<MultiButtonMsgDlg>(pythonDlg.OkDialog);
                                Assert.AreEqual(string.Format(ModelResources.NvidiaInstaller_Requesting_Administrator_elevation, PythonInstaller.GetInstallNvidiaLibrariesBat()),
                                    nvidiaDlg.Message);
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
                        NvidiaTestHelper( pythonDlg, nvidiaClickNo );
                        if (nvidiaClickNo == false)
                        {
                            MultiButtonMsgDlg nvidiaDlg = AbstractFunctionalTest.ShowDialog<MultiButtonMsgDlg>(pythonDlg.OkDialog);
                            Assert.AreEqual(string.Format(ToolsUIResources.PythonInstaller_Install_Cuda_Library),
                                nvidiaDlg.Message);

                            confirmDlg = AbstractFunctionalTest.ShowDialog<MessageDlg>(nvidiaDlg.ClickYes, WAIT_TIME);

                            Assert.AreEqual(string.Format(ModelResources.NvidiaInstaller_Requesting_Administrator_elevation, PythonInstaller.GetInstallNvidiaLibrariesBat()), confirmDlg.Message);
                            AbstractFunctionalTest.OkDialog(confirmDlg, confirmDlg.OkDialog);
                            ConfirmPythonSuccess(confirmDlg);
                        }
                        else if (nvidiaClickNo == true)
                        {
                            confirmDlg = AbstractFunctionalTest.ShowDialog<MessageDlg>(pythonDlg.OkDialog, WAIT_TIME);
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
        /// <summary>
        /// Checks for Python prerequisite
        /// </summary>
        /// <param name="buildLibraryDlg">Build Library dialog</param>
        /// <returns></returns>
        public bool HavePythonPrerequisite(BuildLibraryDlg buildLibraryDlg)
        {
            bool havePythonPrerequisite = false;
            AbstractFunctionalTest.RunUI(() => { havePythonPrerequisite = buildLibraryDlg.PythonRequirementMet(); });
            return havePythonPrerequisite;
        }
        /// <summary>
        /// Runs Nvidia Dialog
        /// </summary>
        /// <param name="nvidiaDlg">Nvidia Detected Dialog</param>
        /// <param name="pythonDlg">Python Installer Dialog</param>
        /// <param name="clickNo">true clicks No, false clicks Yes, null clicks Cancel to Nvidia Detected Dialog</param>
        private void RunNvidiaDialog(MultiButtonMsgDlg nvidiaDlg, MultiButtonMsgDlg pythonDlg, bool? clickNo = true)
        {
            if (clickNo == true)
            {
                // Say 'No'
                MessageDlg confirmDlg = AbstractFunctionalTest.ShowDialog<MessageDlg>(nvidiaDlg.ClickNo, WAIT_TIME);
                ConfirmPythonSuccess(confirmDlg);
            }
            else if (clickNo == false)
            {
                // Say 'Yes'
                MessageDlg confirmDlg = AbstractFunctionalTest.ShowDialog<MessageDlg>(nvidiaDlg.ClickYes, WAIT_TIME);
                ConfirmInstallNvidiaBatMessage(confirmDlg);
            }
            else // clickNo == null
            {
                // Say 'Cancel'
                AbstractFunctionalTest.OkDialog(nvidiaDlg, nvidiaDlg.ClickCancel);
                // confirmDlg = AbstractFunctionalTest.ShowDialog<MessageDlg>(nvidiaDlg.ClickCancel, WAIT_TIME);
                //ConfirmPythonFailed(confirmDlg);
            }
    
            if (!nvidiaDlg.IsDisposed)
                nvidiaDlg.Dispose();
        }

        /// <summary>
        /// Helps with Nvidia GPU Detections
        /// </summary>
        /// <param name="pythonDlg">Python set up is required dialog</param>
        /// <param name="nvidiaClickNo">What to tell Nvidia Dialog: Yes=install, No=don't install, null=cancel operation</param>
        private void NvidiaTestHelper( MultiButtonMsgDlg pythonDlg, bool? nvidiaClickNo)
        {
            //PythonInstaller.SimulatedInstallationState = PythonInstaller.eSimulatedInstallationState.NAIVE;
            if (PythonInstaller.TestForNvidiaGPU() == true && !PythonInstaller.NvidiaLibrariesInstalled())
            {
                Console.WriteLine(@"Info: NVIDIA GPU DETECTED on test node");

                MultiButtonMsgDlg nvidiaDlg = AbstractFunctionalTest.ShowMultiButtonMsgDlg(pythonDlg.OkDialog, ToolsUIResources.PythonInstaller_Install_Cuda_Library, WAIT_TIME);

                RunNvidiaDialog(nvidiaDlg, pythonDlg, nvidiaClickNo);
            }
            else
            {
                if (PythonInstaller.TestForNvidiaGPU() != true) 
                    Console.WriteLine(@"Info: NVIDIA GPU *NOT* DETECTED on test node");
                else
                    Console.WriteLine(@"Info: Nvidia libraries already installed");

                //Not cancelled
                var confirmDlg = AbstractFunctionalTest.ShowDialog<MessageDlg>(pythonDlg.OkDialog, WAIT_TIME);
                ConfirmPythonSuccess(confirmDlg);

            }




        }

        /// <summary>
        /// Tries to set EnableLongPaths
        /// </summary>
        /// <param name="longPathDlg">EnableLongPaths registry dialog</param>
        /// <returns></returns>
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

        /// <summary>
        /// Confirms Python installation success
        /// </summary>
        /// <param name="confirmDlg">Message dialog success</param>
        private void ConfirmPythonSuccess(MessageDlg confirmDlg)
        {
            ConfirmPython(confirmDlg);
        }
        /// <summary>
        /// Confirms Python installation failure
        /// </summary>
        /// <param name="confirmDlg">Message dialog failed</param>
        private void ConfirmPythonFailed(MessageDlg confirmDlg)
        {
            ConfirmPython(confirmDlg, false);
        }

        /// <summary>
        /// Confirms Python installation
        /// </summary>
        /// <param name="confirmDlg">Message dialog</param>
        /// <param name="confirmSuccess">true for success, false for failure</param>
        private void ConfirmPython(MessageDlg confirmDlg, bool confirmSuccess = true)
        {
            var expectMsg = string.Format(ToolsUIResources
                .PythonInstaller_OkDialog_Failed_to_set_up_Python_virtual_environment);
            if (confirmSuccess)
                expectMsg = string.Format(ToolsUIResources
                    .PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment);

            Assert.AreEqual(expectMsg, confirmDlg.Message);
            AbstractFunctionalTest.OkDialog(confirmDlg, confirmDlg.OkDialog);
        }

        /// <summary>
        /// Second dialog after Nvidia is detected to direct user to admin instructions for setting up Nvidia
        /// </summary>
        /// <param name="confirmDlg">Message dialog to the user with admin instructions</param>
        private void ConfirmInstallNvidiaBatMessage(MessageDlg confirmDlg)
        {
            AssertEx.AreComparableStrings(string.Format(ModelResources.NvidiaInstaller_Requesting_Administrator_elevation, PythonInstaller.GetInstallNvidiaLibrariesBat()),
                confirmDlg.Message);
            AbstractFunctionalTest.OkDialog(confirmDlg, confirmDlg.OkDialog);
        }
    }
}
