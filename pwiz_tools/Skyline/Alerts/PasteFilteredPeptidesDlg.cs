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
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Alerts
{
    public partial class PasteFilteredPeptidesDlg : FormEx
    {
        private const int MAX_LINES = 10;

        private IList<string> _peptides;

        public PasteFilteredPeptidesDlg()
        {
            InitializeComponent();
        }

        public IList<string> Peptides
        {
            get { return _peptides; }
            set
            {
                _peptides = value;
                var listSizing = new List<string>(_peptides);
                if (listSizing.Count > MAX_LINES)
                    listSizing.RemoveRange(MAX_LINES, listSizing.Count - MAX_LINES);
 				labelList.Text = TextUtil.LineSeparate(listSizing);                
				Height += labelList.Height - panelList.Height;
                labelList.Text = TextUtil.LineSeparate(_peptides);
                if (_peptides.Count > 1)
                {
                    labelIssue.Text = Resources.PasteFilteredPeptidesDlg_Peptides_The_following_peptides_did_not_meet_the_current_filter_criteria_;
                    labelQuestion.Text = Resources.PasteFilteredPeptidesDlg_Peptides_Do_you_want_to_filter_them_from_the_pasted_list;
                }
            }
        }

        private void btnFilter_Click(object sender, EventArgs e)
        {
            YesDialog();
        }

        public void YesDialog()
        {
            DialogResult = DialogResult.Yes;
            Close();
        }

        private void btnKeep_Click(object sender, EventArgs e)
        {
            NoDialog();
        }

        public void NoDialog()
        {
            DialogResult = DialogResult.No;
            Close();
        }
    }
}
