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
using System.Text.RegularExpressions;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditExclusionDlg : FormEx
    {
        private PeptideExcludeRegex _exclusion;
        private readonly IEnumerable<PeptideExcludeRegex> _existing;

        public EditExclusionDlg(IEnumerable<PeptideExcludeRegex> existing)
        {
            InitializeComponent();

            _existing = existing;
        }

        public PeptideExcludeRegex Exclusion
        {
            get
            {
                return _exclusion;
            }

            set
            {
                _exclusion = value;
                if (_exclusion == null)
                {
                    textName.Text = string.Empty;
                    textExclusionRegex.Text = string.Empty;
                    radioSequence.Checked = true;
                    radioMatching.Checked = true;
                }
                else
                {
                    textName.Text = _exclusion.Name;
                    textExclusionRegex.Text = _exclusion.Regex;
                    if (_exclusion.IsMatchMod)
                        radioModSequence.Checked = true;
                    else
                        radioSequence.Checked = true;
                    if (_exclusion.IsIncludeMatch)
                        radioNotMatching.Checked = true;
                    else
                        radioMatching.Checked = true;
                }
            }
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            string name;
            if (!helper.ValidateNameTextBox(textName, out name))
                return;

            if (_existing.Contains(exc => !ReferenceEquals(_exclusion, exc) && Equals(name, exc.Name)))
            {
                helper.ShowTextBoxError(textName, Resources.EditExclusionDlg_OkDialog_The_peptide_exclusion__0__already_exists, name);
                return;
            }

            string exRegex = textExclusionRegex.Text.Trim();
            if (string.IsNullOrEmpty(exRegex))
            {
                helper.ShowTextBoxError(textExclusionRegex, Resources.EditExclusionDlg_OkDialog__0__must_contain_a_valid_regular_expression_);
                return;
            }
            try
            {
// ReSharper disable ObjectCreationAsStatement
                new Regex(exRegex);
// ReSharper restore ObjectCreationAsStatement
            }
            catch (Exception)
            {
                helper.ShowTextBoxError(textExclusionRegex, Resources.EditExclusionDlg_OkDialog_The_text__0__is_not_a_valid_regular_expression, exRegex);
                return;
            }
            bool includeMatch = radioNotMatching.Checked;
            bool matchMod = radioModSequence.Checked;

            PeptideExcludeRegex exclusion = new PeptideExcludeRegex(name, exRegex, includeMatch, matchMod);

            _exclusion = exclusion;
            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void linkRegex_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            WebHelpers.OpenLink(this, "http://www.regular-expressions.info/reference.html"); // Not L10N
        }
    }
}