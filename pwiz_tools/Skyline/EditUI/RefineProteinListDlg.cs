/*
 * Original author: Alex MacLean <alex .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.IO;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.EditUI
{
    public partial class RefineProteinListDlg : FormEx
    {
        private readonly SrmDocument _document;

        public RefineProteinListDlg(SrmDocument document)
        {
            InitializeComponent();

            _document = document;
        }

        public HashSet<string> AcceptedProteins { get; private set; }
        public RefinementSettings.ProteinSpecType ProteinSpecType { get; private set; }        

        public string ProteinsText
        {
            get { return textProteins.Text; }
            set { textProteins.Text = value; }
        }
        public bool Accession
        {
            get { return proteinAccessions.Checked; }
            set { proteinAccessions.Checked = value; }
        }
        public bool Preferred
        {
            get { return proteinPreferredNames.Checked; }
            set { proteinPreferredNames.Checked = value; }
        }
   
        public void OkDialog()
        {
            var reader = new StringReader(ProteinsText);
            var notFoundLines = new List<string>();
            var acceptedProteins = new HashSet<string>();
            var setProteinNames = GetProteinNames();

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line))
                    continue;
              
                if (!setProteinNames.Contains(line))
                    notFoundLines.Add(line);
                else
                    acceptedProteins.Add(line);
            }

            if (acceptedProteins.Count == 0)
            {
                MessageDlg.Show(this, Resources.RefineListDlgProtein_OkDialog_None_of_the_specified_proteins_are_in_the_document_);
                return;
            }
            if (notFoundLines.Count > 0)
            {
                string message;
                if (notFoundLines.Count == 1)
                {
                    message = string.Format(Resources.RefineListDlgProtein_OkDialog_The_protein___0___is_not_in_the_document__Do_you_want_to_continue_, notFoundLines[0]);
                }
                else if (notFoundLines.Count < 15)
                {
                    message = TextUtil.LineSeparate(Resources.RefineListDlgProtein_OkDialog_The_following_proteins_are_not_in_the_document_, string.Empty,
                                                    TextUtil.LineSeparate(notFoundLines),string.Empty,
                                                    Resources.RefineListDlgProtein_OkDialog_Do_you_want_to_continue);
                }
                else
                {
                    message = string.Format(Resources.RefineListDlgProtein_OkDialog_Of_the_specified__0__proteins__1__are_not_in_the_document__Do_you_want_to_continue_,
                                            notFoundLines.Count + acceptedProteins.Count, notFoundLines.Count);
                }
                if (MultiButtonMsgDlg.Show(this, message, MultiButtonMsgDlg.BUTTON_OK) != DialogResult.OK)
                    return;
            }
            AcceptedProteins = acceptedProteins;
            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private HashSet<string> GetProteinNames()
        {
            var setProteinNames = new HashSet<string>();
            foreach (var nodePep in _document.PeptideGroups)
            {
                if (proteinNames.Checked)
                {
                    setProteinNames.Add(nodePep.Name);
                    ProteinSpecType = RefinementSettings.ProteinSpecType.name;
                }
                else if (proteinAccessions.Checked)
                {
                    setProteinNames.Add(nodePep.ProteinMetadata.Accession);
                    ProteinSpecType = RefinementSettings.ProteinSpecType.accession;
                }
                else
                {
                    setProteinNames.Add(nodePep.ProteinMetadata.PreferredName);
                    ProteinSpecType = RefinementSettings.ProteinSpecType.preferred;
                }
            }
            return setProteinNames;
        }

        private void textProteins_Enter(object sender, EventArgs e)
        {
            AcceptButton = null;
        }

        private void textPeptides_Leave(object sender, EventArgs e)
        {
            AcceptButton = btnOk;
        }
    }
}
