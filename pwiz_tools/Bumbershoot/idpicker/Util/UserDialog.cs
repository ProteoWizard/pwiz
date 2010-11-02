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
        public UserDialog (Control content) : this(null, content) { }

        public UserDialog (IWin32Window owner, Control content)
        {
            InitializeComponent();

            Width = content.Width;
            Height = content.Height + buttonPanel.Height;

            contentPanel.Controls.Add(content);
            content.Dock = DockStyle.Fill;

            StartPosition = owner == null ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent;
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

        public static DialogResult Show (string caption, Control content)
        {
            return new UserDialog(content) { Text = caption }.ShowDialog();
        }

        public static DialogResult Show (IWin32Window owner, string caption, Control content)
        {
            return new UserDialog(owner, content) { Text = caption }.ShowDialog(owner);
        }
    }
}
