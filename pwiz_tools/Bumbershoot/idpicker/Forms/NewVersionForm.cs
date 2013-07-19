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
// Copyright 2012 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace IDPicker.Forms
{
    public partial class NewVersionForm : Form
    {
        public NewVersionForm (string productName, string currentVersion, string newVersion, string changeLog)
        {
            InitializeComponent();

            nonCollapsedSize = Size;
            showChangeLogCheckbox.Checked = false;

            textTemplate.Text = String.Format(textTemplate.Text, productName, currentVersion, newVersion);
            changelogTextBox.Text = changeLog;
            showChangeLogCheckbox.Visible = !changeLog.IsNullOrEmpty();
        }

        Size nonCollapsedSize;
        private void showChangeLogCheckbox_CheckedChanged (object sender, EventArgs e)
        {
            if (showChangeLogCheckbox.Checked)
            {
                AutoSize = false;
                AutoSizeMode = AutoSizeMode.GrowOnly;
                FormBorderStyle = FormBorderStyle.Sizable;
                MaximizeBox = true;
                splitContainer.Panel2Collapsed = false;
                Size = nonCollapsedSize;
            }
            else
            {
                nonCollapsedSize = Size;
                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowAndShrink;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                splitContainer.Panel2Collapsed = true;
            }
        }
    }
}
