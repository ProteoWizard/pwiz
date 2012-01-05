/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Windows.Forms;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.SettingsUI.Irt
{
    public enum AddIrtPeptidesAction { skip, replace, average }

    public partial class AddIrtPeptidesDlg : Form
    {
        public AddIrtPeptidesDlg(int peptideCount,
            int runsConvertedCount,
            int runsFailedCount,
            IEnumerable<string> existingPeptides,
            IEnumerable<string> overwritePeptides)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            if (peptideCount == 0)
                labelPeptidesAdded.Text = "No new peptides will be added to the iRT database.";
            else if (peptideCount == 1)
                labelPeptidesAdded.Text = "1 new peptide will be added to the iRT database.";
            else
                labelPeptidesAdded.Text = string.Format(labelPeptidesAdded.Text, peptideCount);

            if (runsConvertedCount == 0)
                labelRunsConverted.Visible = false;
            else
            {
                labelRunsConverted.Text = runsConvertedCount > 1
                                              ? string.Format(labelRunsConverted.Text, runsConvertedCount)
                                              : "1 run was successfully converted.";
            }

            if (runsFailedCount == 0)
                labelRunsFailed.Visible = false;
            else
            {
                labelRunsFailed.Text = runsFailedCount > 1
                                           ? string.Format(labelRunsFailed.Text, runsFailedCount)
                                           : "1 run was not converted due to insufficient correlation.";
            }
                
            foreach (string existingPeptide in existingPeptides)
                listExisting.Items.Add(existingPeptide);
            foreach (string overwritePeptide in overwritePeptides)
                listOverwrite.Items.Add(overwritePeptide);

            labelExisting.Text = string.Format(labelExisting.Text, listExisting.Items.Count);

            if (listOverwrite.Items.Count == 0)
            {
                panelOverwrite.Visible = false;
                panelExisting.Top -= panelOverwrite.Height;
                panelExisting.Anchor &= ~AnchorStyles.Bottom;
                Height -= panelOverwrite.Height;
                panelExisting.Anchor |= AnchorStyles.Bottom;
            }
            if (listExisting.Items.Count == 0)
            {
                panelExisting.Visible = false;
                Height -= panelExisting.Height;
            }
            if (!panelOverwrite.Visible && !panelExisting.Visible)
                FormBorderStyle = FormBorderStyle.FixedDialog;
        }

        public AddIrtPeptidesAction Action
        {
            get
            {
                if (radioAverage.Checked)
                    return AddIrtPeptidesAction.average;
                if (radioReplace.Checked)
                    return AddIrtPeptidesAction.replace;
                return AddIrtPeptidesAction.skip;
            }

            set
            {
                switch (value)
                {
                    case AddIrtPeptidesAction.average:
                        radioAverage.Checked = true;
                        break;
                    case AddIrtPeptidesAction.replace:
                        radioReplace.Checked = true;
                        break;
                    default:
                        radioSkip.Checked = true;
                        break;
                }
            }
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }
    }
}
