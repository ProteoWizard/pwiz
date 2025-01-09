using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using System;
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
                        Assert.AreEqual(string.Format(ToolsUIResources.PythonInstaller_Enable_Windows_Long_Paths), longPathDlg.Message);

                        if (IsRunningElevated())
                        {
                            RunDlg<MessageDlg>(longPathDlg.ClickYes, okDlg =>
                            {
                                confirmDlg = okDlg;
                                Console.WriteLine(@"Info: Trying to set LongPathsEnabled registry key to 1");
                                if (ToolsUIResources
                                        .PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment !=
                                    okDlg.Message)
                                {
                                    Console.ReadLine();
                                }
                                Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment, okDlg.Message);
                                Console.WriteLine(@"Info: Successfully set LongPathsEnabled registry key to 1");
                                _undoRegistry = true;
                                okDlg.OkDialog();
                            });
                            if (!longPathDlg.IsDisposed)
                                longPathDlg.Dispose();
                        }
                        else
                        {
                            Assert.Fail($@"Error: Cannot finish {_toolName}BuildLibraryTest because LongPathsEnabled is not set and have insufficient permissions to set it");
                        }
                    }
                    else
                    {
                        Console.WriteLine(@"Info: LongPathsEnabled registry key is set to 1");
                        confirmDlg = WaitForOpenForm<MessageDlg>();
                        Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment, confirmDlg.Message);
                        OkDialog(confirmDlg, confirmDlg.OkDialog);
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
