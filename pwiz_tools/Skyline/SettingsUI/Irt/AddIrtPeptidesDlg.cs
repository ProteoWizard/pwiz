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
        public AddIrtPeptidesDlg(IEnumerable<string> existingPeptides, IEnumerable<string> overwritePeptides)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            foreach (string existingPeptide in existingPeptides)
                listExisting.Items.Add(existingPeptide);
            foreach (string overwritePeptide in overwritePeptides)
                listOverwrite.Items.Add(overwritePeptide);

            if (listOverwrite.Items.Count == 0)
            {
                labelOverwrite.Visible = false;
                listOverwrite.Visible = false;
                Height -= listOverwrite.Bottom - radioAverage.Bottom;
            }
            else if (listExisting.Items.Count == 0)
            {
                labelExisting.Visible = false;
                listExisting.Visible = false;
                labelChoice.Visible = false;
                radioSkip.Visible = false;
                radioReplace.Visible = false;
                radioAverage.Visible = false;
                Height -= listOverwrite.Bottom - listExisting.Bottom;
                labelOverwrite.Top = labelExisting.Top;
                listOverwrite.Top = listExisting.Top;
            }
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
