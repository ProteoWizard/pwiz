//
// $Id: LayoutNameBox.cs 239 2010-11-15 17:20:21Z holmanjd $
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
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace BumberDash.Forms
{
    public sealed partial class TextPromptBox : Form
    {
        private readonly Regex _inputFormat;

        public TextPromptBox()
        {
            InitializeComponent();
            _inputFormat = new Regex(@"^(?:\w| |/)+$");
            inputTextBox.Select();
        }

        public TextPromptBox(string title, string defaultName)
        {
            InitializeComponent();
            _inputFormat = new Regex(@"^(?:\w| |/)+$");
            Text = title;
            inputLabel.Text = string.Format("New {0}:", title);
            inputTextBox.Text = defaultName;
            inputTextBox.Select();
        }

        private void inputTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!_inputFormat.IsMatch(e.KeyChar.ToString()) && !Char.IsControl(e.KeyChar))
                e.Handled = true;
        }

        internal string GetText()
        {
            return string.IsNullOrEmpty(inputTextBox.Text) 
                ? "(Default)" 
                : inputTextBox.Text;
        }
    }
}
