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
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI.Irt
{
    public enum AddIrtPeptidesAction { skip, replace, average }

    public partial class AddIrtPeptidesDlg : FormEx
    {
        public AddIrtPeptidesDlg(int peptideCount,
            int runsConvertedCount,
            int runsFailedCount,
            IEnumerable<string> existingPeptides,
            IEnumerable<string> overwritePeptides,
            IEnumerable<string> keepPeptides)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            PeptidesCount = peptideCount;
            RunsConvertedCount = runsConvertedCount;
            RunsFailedCount = runsFailedCount;

            if (peptideCount == 0)
                labelPeptidesAdded.Text = Resources.AddIrtPeptidesDlg_AddIrtPeptidesDlg_No_new_peptides_will_be_added_to_the_iRT_database;
            else if (peptideCount == 1)
                labelPeptidesAdded.Text = Resources.AddIrtPeptidesDlg_AddIrtPeptidesDlg_1_new_peptide_will_be_added_to_the_iRT_database;
            else
                labelPeptidesAdded.Text = string.Format(labelPeptidesAdded.Text, peptideCount);

            if (runsConvertedCount == 0)
                labelRunsConverted.Visible = false;
            else
            {
                labelRunsConverted.Text = runsConvertedCount > 1
                                              ? string.Format(labelRunsConverted.Text, runsConvertedCount)
                                              : Resources.AddIrtPeptidesDlg_AddIrtPeptidesDlg_1_run_was_successfully_converted;
            }

            if (runsFailedCount == 0)
                labelRunsFailed.Visible = false;
            else
            {
                labelRunsFailed.Text = runsFailedCount > 1
                                           ? string.Format(labelRunsFailed.Text, runsFailedCount)
                                           : Resources.AddIrtPeptidesDlg_AddIrtPeptidesDlg_1_run_was_not_converted_due_to_insufficient_correlation;
            }
                
            listExisting.Items.AddRange(existingPeptides.Cast<object>().ToArray());
            listOverwrite.Items.AddRange(overwritePeptides.Cast<object>().ToArray());
            listKeep.Items.AddRange(keepPeptides.Cast<object>().ToArray());

            labelExisting.Text = string.Format(labelExisting.Text, listExisting.Items.Count);
            labelOverwrite.Text = string.Format(labelOverwrite.Text, listOverwrite.Items.Count);
            labelKeep.Text = string.Format(labelKeep.Text, listKeep.Items.Count);

            panelExisting.Anchor &= ~AnchorStyles.Bottom;
            if (listOverwrite.Items.Count == 0)
            {
                panelOverwrite.Visible = false;
                panelKeep.Top -= panelOverwrite.Height;
                panelExisting.Top -= panelOverwrite.Height;
                Height -= panelOverwrite.Height;
            }
            if (listKeep.Items.Count == 0)
            {
                panelKeep.Visible = false;
                panelExisting.Top -= panelKeep.Height;
                Height -= panelKeep.Height;
            }
            panelExisting.Anchor |= AnchorStyles.Bottom;
            if (listExisting.Items.Count == 0)
            {
                panelExisting.Visible = false;
                Height -= panelExisting.Height;
            }
            if (!panelOverwrite.Visible && !panelKeep.Visible && !panelExisting.Visible)
                FormBorderStyle = FormBorderStyle.FixedDialog;
        }

        public int PeptidesCount { get; private set; }
        public int RunsConvertedCount { get; private set; }
        public int RunsFailedCount { get; private set; }
        public int KeepPeptidesCount { get { return listKeep.Items.Count; } }
        public int OverwritePeptidesCount { get { return listOverwrite.Items.Count; } }
        public int ExistingPeptidesCount { get { return listExisting.Items.Count; } }

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
