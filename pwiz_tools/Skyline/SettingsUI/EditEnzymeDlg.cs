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
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditEnzymeDlg : FormEx
    {
        private Enzyme _enzyme;
        private readonly IEnumerable<Enzyme> _existing;

        private readonly string _cleavageCLabelText;
        private readonly string _restrictCLabelText;
        private readonly string _cleavageNLabelText;
        private readonly string _restrictNLabelText;

        public EditEnzymeDlg(IEnumerable<Enzyme> existing)
        {
            _existing = existing;

            InitializeComponent();

            _cleavageCLabelText = labelCleavage.Text;
            _restrictCLabelText = labelRestrict.Text;
            _cleavageNLabelText = labelCleavageN.Text;
            _restrictNLabelText = labelRestrictN.Text;

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
                    textName.Text = string.Empty;
                    textCleavage.Text = string.Empty;
                    textRestrict.Text = string.Empty;
                    comboDirection.SelectedIndex = 0;
                }
                else
                {
                    textName.Text = _enzyme.Name;
                    if (_enzyme.IsBothTerm)
                    {
                        comboDirection.SelectedIndex = 2;
                        textCleavage.Text = _enzyme.CleavageC;
                        textRestrict.Text = _enzyme.RestrictC;
                        textCleavageN.Text = _enzyme.CleavageN;
                        textRestrictN.Text = _enzyme.RestrictN;
                    }
                    else if (_enzyme.IsNTerm)
                    {
                        comboDirection.SelectedIndex = 1;
                        textCleavage.Text = _enzyme.CleavageN;
                        textRestrict.Text = _enzyme.RestrictN;
                    }
                    else
                    {
                        comboDirection.SelectedIndex = 0;
                        textCleavage.Text = _enzyme.CleavageC;
                        textRestrict.Text = _enzyme.RestrictC;
                    }
                }
            }
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            string name;
            if (!helper.ValidateNameTextBox(textName, out name))
                return;

            string cleavageC;
            if (!helper.ValidateAATextBox(textCleavage, false, out cleavageC))
                return;
            string restrictC;
            if (!helper.ValidateAATextBox(textRestrict, true, out restrictC))
                return;

            string cleavageN;
            string restrictN;
            if (comboDirection.SelectedIndex == 2)
            {
                if (!helper.ValidateAATextBox(textCleavageN, false, out cleavageN))
                    return;
                if (!helper.ValidateAATextBox(textRestrictN, true, out restrictN))
                    return;
            }
            else if (comboDirection.SelectedIndex == 1)
            {
                cleavageN = cleavageC;
                cleavageC = null;
                restrictN = restrictC;
                restrictC = null;
            }
            else
            {
                cleavageN = null;
                restrictN = null;
            }

            Enzyme enzyme = new Enzyme(name, cleavageC, restrictC, cleavageN, restrictN);
            if (_enzyme == null && _existing.Contains(enzyme))
            {
                helper.ShowTextBoxError(textName, Resources.EditEnzymeDlg_OnClosing_The_enzyme__0__already_exists, name);
                return;
            }

            _enzyme = enzyme;
            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void comboDirection_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboDirection.SelectedIndex == 1)
            {
                labelCleavage.Text = _cleavageNLabelText;
                labelRestrict.Text = _restrictNLabelText;
            }
            else
            {
                labelCleavage.Text = _cleavageCLabelText;
                labelRestrict.Text = _restrictCLabelText;
            }
            if (comboDirection.SelectedIndex == 2)
            {
                labelCleavageN.Visible =
                    textCleavageN.Visible =
                    labelRestrictN.Visible =
                    textRestrictN.Visible = true;
                if (textRestrictN.Bottom > ClientRectangle.Bottom)
                {
                    Height += textRestrictN.Bottom - textRestrict.Bottom;
                }
            }
            else
            {
                labelCleavageN.Visible =
                    textCleavageN.Visible =
                    labelRestrictN.Visible =
                    textRestrictN.Visible = false;
                if (textRestrictN.Bottom < ClientRectangle.Bottom)
                {
                    Height -= textRestrictN.Bottom - textRestrict.Bottom;
                }
            }
        }

        #region Functional test support
        
        public string EnzymeName
        {
            get { return textName.Text; }
            set { textName.Text = value; }
        }

        public string Cleavage
        {
            get { return textCleavage.Text; }
            set { textCleavage.Text = value; }
        }

        public string Restrict
        {
            get { return textRestrict.Text; }
            set { textRestrict.Text = value;}
        }

        public string CleavageN
        {
            get { return textCleavageN.Text; }
            set { textCleavageN.Text = value; }
        }

        public string RestrictN
        {
            get { return textRestrictN.Text; }
            set { textRestrictN.Text = value; }
        }

        public SequenceTerminus? Type
        {
            get
            {
                switch (comboDirection.SelectedIndex)
                {
                    case 0:
                        return SequenceTerminus.C;
                    case 1:
                        return SequenceTerminus.N;
                    default:
                        return null;
                }
            }
            set
            {
                if (!value.HasValue)
                    comboDirection.SelectedIndex = 2;
                else if (value.Value == SequenceTerminus.N)
                    comboDirection.SelectedIndex = 1;
                else
                    comboDirection.SelectedIndex = 0;

            }
        }

        #endregion
    }

    internal static class EnzymeMessageBoxHelper
    {
        public static bool ValidateAATextBox(this MessageBoxHelper helper, TextBox control, bool allowEmpty, out string aaText)
        {
            aaText = control.Text.Trim().ToUpperInvariant();
            if (aaText.Length == 0)
            {
                if (!allowEmpty)
                {
                    helper.ShowTextBoxError(control, Resources.EditEnzymeDlg_ValidateAATextBox__0__must_contain_at_least_one_amino_acid);
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
                        helper.ShowTextBoxError(control, Resources.EditEnzymeDlg_ValidateAATextBox_The_character__0__is_not_a_valid_amino_acid, c);
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
    }
}