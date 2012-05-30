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
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditEnzymeDlg : FormEx
    {
        private Enzyme _enzyme;
        private readonly IEnumerable<Enzyme> _existing;
        private bool _clickedOk;

        private readonly MessageBoxHelper _helper;

        public EditEnzymeDlg(IEnumerable<Enzyme> existing)
        {
            _existing = existing;

            InitializeComponent();

            _helper = new MessageBoxHelper(this);

            // Seems like there should be a way to set this in the properties.
            comboDirection.SelectedIndex = 0;
        }

        public Enzyme Enzyme
        {
            get { return _enzyme; }

            set
            {
                _enzyme = value;
                if (_enzyme == null)
                {
                    textName.Text = "";
                    textCleavage.Text = "";
                    textRestrict.Text = "";
                    comboDirection.SelectedIndex = 0;
                }
                else
                {
                    textName.Text = _enzyme.Name;
                    textCleavage.Text = _enzyme.Cleavage;
                    textRestrict.Text = _enzyme.Restrict;
                    comboDirection.SelectedIndex = (_enzyme.Type == SequenceTerminus.C ? 0 : 1);
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_clickedOk)
            {
                _clickedOk = false; // Reset in case of failure.

                string name;
                if (!_helper.ValidateNameTextBox(e, textName, out name))
                    return;

                string cleavage;
                if (!ValidateAATextBox(e, textCleavage, false, out cleavage))
                    return;
                string restrict;
                if (!ValidateAATextBox(e, textRestrict, true, out restrict))
                    return;

                SequenceTerminus direction = (comboDirection.SelectedIndex == 0 ?
                        SequenceTerminus.C : SequenceTerminus.N);

                Enzyme enzyme = new Enzyme(name, cleavage, restrict, direction);
                if (_enzyme == null && _existing.Contains(enzyme))
                {
                    _helper.ShowTextBoxError(textName, "The enzyme '{0}' already exists.", name);
                    e.Cancel = true;
                    return;
                }

                _enzyme = enzyme;
            }

            base.OnClosing(e);
        }

        private bool ValidateAATextBox(CancelEventArgs e, TextBox control, bool allowEmpty, out string aaText)
        {
            aaText = control.Text.Trim().ToUpper();
            if (aaText.Length == 0)
            {
                if (!allowEmpty)
                {
                    _helper.ShowTextBoxError(control, "{0} must contain at least one amino acid.");
                    e.Cancel = true;
                    return false;
                }
            }
            else
            {
                StringBuilder aaBuilder = new StringBuilder();
                HashSet<char> setAA = new HashSet<char>();
                foreach (char c in aaText)
                {
                    if (!AminoAcid.IsAA(c))
                    {
                        _helper.ShowTextBoxError(control, "The character '{0}' is not a valid amino acid.", c);
                        e.Cancel = true;
                        return false;
                    }
                    // Silently strip duplicates.
                    if (!setAA.Contains(c))
                    {
                        aaBuilder.Append(c);
                        setAA.Add(c);
                    }
                }
                aaText = aaBuilder.ToString();
            }
            return true;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            _clickedOk = true;
        }
    }
}