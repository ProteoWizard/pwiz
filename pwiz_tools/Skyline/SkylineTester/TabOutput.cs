using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace SkylineTester
{
    public partial class SkylineTesterWindow
    {
        private void Stop(object sender, EventArgs e)
        {
            ToggleRunButtons(null);
        }

        private void linkLogFile_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (File.Exists(_logFile))
            {
                var editLogFile = new Process {StartInfo = {FileName = _logFile}};
                editLogFile.Start();
            }
        }

    }
}
