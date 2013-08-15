/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class RTDetails : FormEx
    {
        public RTDetails(RetentionTimeStatistics statistics)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            for (int i = 0; i < statistics.Peptides.Count; i++)
            {
                gridStatistics.Rows.Add(statistics.Peptides[i],
                                        string.Format("{0:F02}", statistics.ListHydroScores[i]), // Not L10N
                                        string.Format("{0}", statistics.ListPredictions[i]), // Not L10N
                                        statistics.ListRetentionTimes[i]);
            }
        }

        private void gridStatistics_KeyDown(object sender, KeyEventArgs e)
        {
            // Handle Ctrl + C for copy
            if (e.KeyCode == Keys.V && e.Control)
            {
                StringBuilder sb = new StringBuilder();
                foreach (DataGridViewRow row in gridStatistics.Rows)
                {
                    if (row.IsNewRow)
                        continue;
                    
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        if (sb[sb.Length - 1] != '\n') // Not L10N
                            sb.Append('\t'); // Not L10N
                        sb.Append(cell.Value);
                    }
                    sb.Append('\n'); // Not L10N
                }
                try
                {
                    ClipboardEx.Clear();
                    ClipboardEx.SetText(sb.ToString());
                }
                catch (ExternalException)
                {
                    MessageDlg.Show(this, ClipboardHelper.GetOpenClipboardMessage(Resources.RTDetails_gridStatistics_KeyDown_Failed_setting_data_to_clipboard));
                }
            }
        }
    }
}
