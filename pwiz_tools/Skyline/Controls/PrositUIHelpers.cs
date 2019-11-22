/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
