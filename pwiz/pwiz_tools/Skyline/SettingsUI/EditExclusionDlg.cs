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
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditExclusionDlg : Form
    {
        private PeptideExcludeRegex _exclusion;
        private readonly IEnumerable<PeptideExcludeRegex> _existing;
        private bool _clickedOk;

        private readonly MessageBoxHelper _helper;

        public EditExclusionDlg(IEnumerable<PeptideExcludeRegex> existing)
        {
            _existing = existing;

            InitializeComponent();

            _helper = new MessageBoxHelper(this);
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
                    textName.Text = "";
                    textExclusionRegex.Text = "";
                }
                else
                {
                    textName.Text = _exclusion.Name;
                    textExclusionRegex.Text = _exclusion.Regex;
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // If the user is accepting these settings, then validate
            // and hold them in dialog member variables.
            if (_clickedOk)
            {
                _clickedOk = false; // Reset in case of failure

                string name;
                if (!_helper.ValidateNameTextBox(e, textName, out name))
                    return;

                string exRegex = textExclusionRegex.Text.Trim();

                PeptideExcludeRegex exclusion = new PeptideExcludeRegex(name, exRegex);
                if (_exclusion == null && _existing.Contains(exclusion))
                {
                    _helper.ShowTextBoxError(textName, "The peptide exclusion '{0}' already exists.", name);
                    e.Cancel = true;
                    return;
                }

                _exclusion = exclusion;
            }

            base.OnClosing(e);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            _clickedOk = true;
        }
    }
}