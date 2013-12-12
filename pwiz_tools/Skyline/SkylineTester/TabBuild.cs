using System;
using System.IO;
using System.Windows.Forms;

namespace SkylineTester
{
    public partial class SkylineTesterWindow
    {
        private void RunBuild(object sender, EventArgs e)
        {
            if (!ToggleRunButtons(tabBuild))
                return;

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var subversion = Path.Combine(programFiles, @"Subversion\bin\svn.exe");
            if (!File.Exists(subversion))
            {
                subversion = Path.Combine(programFiles, @"VisualSVN\bin\svn.exe");
                if (!File.Exists(subversion))
                {
                    // TODO: Offer to install for the user.
                    MessageBox.Show("Must install Subversion");
                    return;
                }
            }

            Tabs.SelectTab(tabOutput);

            _buildDir = Path.GetDirectoryName(Environment.CurrentDirectory) ?? "";
            _buildDir = Path.Combine(_buildDir, "Build");
            _appendToLog = true;

            StartProcess(
                subversion,
                @"checkout https://svn.code.sf.net/p/proteowizard/code/trunk/pwiz " + _buildDir,
                _buildDir,
                SubversionExit,
                true);
        }

        private void SubversionExit(object sender, EventArgs e)
        {
            if (!buttonStopLog.Enabled)
                return;

            StartProcess(
                Path.Combine(_buildDir, @"pwiz_tools\build-apps.bat"),
                @"32 --i-agree-to-the-vendor-licenses toolset=msvc-10.0 nolog",
                _buildDir,
                ProcessExit,
                true);
        }
    }
}
