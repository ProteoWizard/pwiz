//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Jay Holman.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace IDPicker.Forms
{
    public partial class LayoutNameBox : Form
    {
        private readonly Regex _inputFormat;

        public LayoutNameBox()
        {
            InitializeComponent();
            _inputFormat = new Regex(@"\w| ");
        }

        public LayoutNameBox(string regEx)
        {
            InitializeComponent();
            try
            {
                _inputFormat = new Regex(regEx);
            }
            catch (Exception)
            {
                _inputFormat = new Regex(@"\w| ");
            }
        }

        private void inputTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!_inputFormat.IsMatch(e.KeyChar.ToString()) &&  !Char.IsControl(e.KeyChar))
                e.Handled = true;
        }

        private void TextInputBox_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (string.IsNullOrEmpty(inputTextBox.Text) && okButton.Focused)
                e.Cancel = true;
        }
    }
}
