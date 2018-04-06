//
// $Id$
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
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace IDPicker
{
    /// <summary>
    /// A utility class for easily making an OK/Cancel dialog out of a single control (usually a UserControl);
    /// provides functionality similar to <see cref="MessageBox">MessageBox</see>.
    /// </summary>
    public partial class UserDialog : Form
    {
        public UserDialog (Control content) : this(null, content, MessageBoxButtons.OKCancel) { }

        public UserDialog (IWin32Window owner, Control content, MessageBoxButtons buttons)
        {
            InitializeComponent();

            ClientSize = new System.Drawing.Size(content.Width, content.Height + buttonPanel.Height);

            contentPanel.Controls.Add(content);
            content.Dock = DockStyle.Fill;

            StartPosition = owner == null ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent;

            if (buttons == MessageBoxButtons.OK)
            {
                okButton.Visible = true;
                cancelButton.Visible = false;
            }
            else if (buttons == MessageBoxButtons.OKCancel)
            {
                okButton.Visible = true;
                cancelButton.Visible = true;
            }
            else
            {
                throw new NotImplementedException("UserDialog currently only supports OK and OKCancel values for MessageBoxButtons");
            }
        }

        private void cancelButton_Click (object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void okButton_Click (object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        public static DialogResult Show (string caption, Control content, MessageBoxButtons buttons = MessageBoxButtons.OKCancel)
        {
            return new UserDialog(content) { Text = caption }.ShowDialog();
        }

        public static DialogResult Show (IWin32Window owner, string caption, Control content, MessageBoxButtons buttons = MessageBoxButtons.OKCancel)
        {
            return new UserDialog(owner, content, buttons) { Text = caption }.ShowDialog(owner);
        }

        public static DialogResult Show (IWin32Window owner, string caption, Control content, FormBorderStyle borderStyle, MessageBoxButtons buttons = MessageBoxButtons.OKCancel)
        {
            return new UserDialog(owner, content, buttons) { Text = caption, FormBorderStyle = borderStyle }.ShowDialog(owner);
        }
    }
}
