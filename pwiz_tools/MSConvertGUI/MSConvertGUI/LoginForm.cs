//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
//
// Copyright 2018 Matt Chambers - Nashville, TN 37221
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MSConvertGUI
{
    public partial class LoginForm : Form
    {
        public enum ApiVersion
        {
            Version3 = 3,
            Version4
        }

        public LoginForm(ApiVersion apiVersion)
        {
            InitializeComponent();
            base.AcceptButton = okButton;
            base.CancelButton = cancelButton;
            advancedOptionLabelPanel.Visible = advancedOptionPanel.Visible = false;

            switch(apiVersion)
            {
                case ApiVersion.Version3:
                    identityServerTextBox.Text = "<HostURL>:50333";
                    clientScopeTextBox.Text = "unifi";
                    break;
                case ApiVersion.Version4:
                    identityServerTextBox.Text = "<HostURL>:48333";
                    clientScopeTextBox.Text = "webapi";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(apiVersion), apiVersion, null);
            }
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void advancedCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            advancedOptionLabelPanel.Visible = advancedOptionPanel.Visible = advancedCheckbox.Checked;
        }
    }
}
