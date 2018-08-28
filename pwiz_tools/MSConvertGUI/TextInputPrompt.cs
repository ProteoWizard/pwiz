//
// $Id: LayoutNameBox.cs 239 2010-11-15 17:20:21Z holmanjd $
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
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

namespace MSConvertGUI
{
    public sealed partial class TextInputPrompt : Form
    {
        public Regex InputFormat { get; set; }

        public TextInputPrompt(string title, bool checkboxShown, string initialText)
        {
            InitializeComponent();
            Text = title;
            inputLabel.Text = string.Format("New {0}:", title);
            if (!checkboxShown)
                inputCheckBox.Visible = false;
            inputTextBox.Text = initialText;

            InputFormat = new Regex(@"[a-zA-Z0-9 `~!@#$%&_=\-\+\.\^\*\(\)\[\]\{\}\|<>,;':/\\]");
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            inputTextBox.Focus();
        }

        private void inputTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (InputFormat != null && !InputFormat.IsMatch(e.KeyChar.ToString()) && !Char.IsControl(e.KeyChar))
                e.Handled = true;
        }

        private void TextInputBox_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (String.IsNullOrEmpty(inputTextBox.Text) && okButton.Focused)
                e.Cancel = true;
        }

        public string GetText()
        {
            return inputTextBox.Text;
        }

        public bool GetCheckState()
        {
            return inputCheckBox.Checked;
        }

        private void inputTextBox_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }
    }
}
