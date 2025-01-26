using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using System;
using System.Management;
using System.Security.Principal;

namespace pwiz.SkylineTestUtil
{
    [TestClass]
    public class PythonTestUtil : AbstractFunctionalTestEx
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
        //[TestMethod]
        public bool InstallPython(BuildLibraryDlg buildLibraryDlg)
        {
            bool havePythonPrerequisite = false;

            RunUI(() => { havePythonPrerequisite = buildLibraryDlg.PythonRequirementMet(); });

            if (!havePythonPrerequisite)
            {
                MessageDlg confirmDlg = null;
                MultiButtonMsgDlg nvidiaDlg = null;
                RunLongDlg<MultiButtonMsgDlg>(buildLibraryDlg.OkWizardPage, pythonDlg =>
                {
                    Assert.AreEqual(string.Format(
                            ToolsUIResources.PythonInstaller_BuildPrecursorTable_Python_0_installation_is_required,
                            _pythonVersion, _toolName), pythonDlg.Message);

                    OkDialog(pythonDlg, pythonDlg.OkDialog);
                    if (!PythonInstallerTaskValidator.ValidateEnableLongpaths())
                    {
                        MultiButtonMsgDlg longPathDlg = null;
                        longPathDlg = WaitForOpenForm<MultiButtonMsgDlg>();
                        Assert.AreEqual(
                            string.Format(ToolsUIResources.PythonInstaller_Requesting_Administrator_elevation),
                            longPathDlg.Message);

                        if (IsRunningElevated())
                        {
                            if (PythonInstaller.TestForNvidiaGPU() == true && !PythonInstaller.NvidiaLibrariesInstalled())
                            {
                                Console.WriteLine(@"Info: NVIDIA GPU DETECTED on test node");
                                RunDlg<MultiButtonMsgDlg>(longPathDlg.ClickOk, okDlg =>
                                {
                                    nvidiaDlg = okDlg;
                                    Console.WriteLine(@"Info: Trying to set LongPathsEnabled registry key to 1");
                                    Assert.AreEqual(
                                        string.Format(ToolsUIResources.PythonInstaller_Install_Cuda_Library),
                                        okDlg.Message);
                                    Console.WriteLine(@"Info: Successfully set LongPathsEnabled registry key to 1");
                                    _undoRegistry = true;
                                });

                                if (!longPathDlg.IsDisposed)
                                    longPathDlg.Dispose();
                                
                                nvidiaDlg = WaitForOpenForm<MultiButtonMsgDlg>();

                                Assert.AreEqual(string.Format(ToolsUIResources.PythonInstaller_Install_Cuda_Library),
                                    nvidiaDlg.Message);
                                
                                RunDlg<MessageDlg>(nvidiaDlg.ClickNo, okDlg =>
                                {
                                    confirmDlg = okDlg;
                                    Console.WriteLine(@"Info: Skipping installing of Nvidia Libraries on test node");

                                    confirmDlg = WaitForOpenForm<MessageDlg>(600000);
                                    Assert.AreEqual(
                                        ToolsUIResources
                                            .PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment,
                                        confirmDlg.Message);
                                    okDlg.OkDialog();
                                });
                                if (!nvidiaDlg.IsDisposed)
                                    nvidiaDlg.Dispose();
                            }
                            else
                            {
                                if (PythonInstaller.TestForNvidiaGPU() != true) 
                                    Console.WriteLine(@"Info: NVIDIA GPU *NOT* DETECTED on test node");
                                else
                                    Console.WriteLine(@"Info: Nvidia libraries already installed");

                                RunDlg<MessageDlg>(longPathDlg.ClickOk, okDlg =>
                                {
                                    confirmDlg = okDlg;
                                    Console.WriteLine(@"Info: Trying to set LongPathsEnabled registry key to 1");
                                    Assert.AreEqual(
                                        ToolsUIResources
                                            .PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment,
                                        okDlg.Message);
                                    Console.WriteLine(@"Info: Successfully set LongPathsEnabled registry key to 1");
                                    _undoRegistry = true;
                                    okDlg.OkDialog();
                                });
                                if (!longPathDlg.IsDisposed)
                                    longPathDlg.Dispose();
                            }
                        }
                        else
                        {
                            Assert.Fail($@"Error: Cannot finish {_toolName}BuildLibraryTest because LongPathsEnabled is not set and have insufficient permissions to set it");
                        }
                    }
                    else
                    {
                        Console.WriteLine(@"Info: LongPathsEnabled registry key is already set to 1");


                        if (PythonInstaller.TestForNvidiaGPU() == true && !PythonInstaller.NvidiaLibrariesInstalled())
                        {
                            Console.WriteLine(@"Info: NVIDIA GPU DETECTED on test node");

                            nvidiaDlg = WaitForOpenForm<MultiButtonMsgDlg>();
                            Assert.AreEqual(string.Format(ToolsUIResources.PythonInstaller_Install_Cuda_Library),
                                nvidiaDlg.Message);
                            RunDlg<MessageDlg>(nvidiaDlg.ClickNo, okDlg =>
                            {
                                confirmDlg = okDlg;

                                confirmDlg = WaitForOpenForm<MessageDlg>(600000);
                                Assert.AreEqual(string.Format(ToolsUIResources.PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment),
                                    confirmDlg.Message);
                                okDlg.OkDialog();
                            });
                            if (!nvidiaDlg.IsDisposed)
                                nvidiaDlg.Dispose();
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
                            confirmDlg = WaitForOpenForm<MessageDlg>(600000);
                            Assert.AreEqual(string.Format(ToolsUIResources.PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment), confirmDlg.Message);
                            OkDialog(confirmDlg, confirmDlg.OkDialog);
                            if (!confirmDlg.IsDisposed)
                                confirmDlg.Dispose();

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

        protected override void DoTest()
        {
            throw new NotImplementedException();
        }
    }
}
