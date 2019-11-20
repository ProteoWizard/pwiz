using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Prosit;
using pwiz.Skyline.Model.Prosit.Models;
using pwiz.Skyline.ToolsUI;

namespace pwiz.Skyline.Controls
{
    public class PrositUIHelpers
    {
        public static void CheckPrositSettings(IWin32Window owner, SkylineWindow skylineWindow)
        {
            if (!PrositHelpers.PrositSettingsValid)
            {
                using (var dlg = new AlertDlg(PrositResources.BuildLibraryDlg_dataSourceFilesRadioButton_CheckedChanged_Some_Prosit_settings_are_not_set__Would_you_like_to_set_them_now_, MessageBoxButtons.YesNo))
                {
                    dlg.ShowDialog(owner);
                    if (dlg.DialogResult == DialogResult.Yes)
                        skylineWindow.ShowToolOptionsUI(dlg, ToolOptionsUI.TABS.Prosit);
                }
            }
        }
    }
}
