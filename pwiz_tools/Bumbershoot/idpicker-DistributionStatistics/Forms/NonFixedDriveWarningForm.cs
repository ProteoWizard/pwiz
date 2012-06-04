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
    public partial class NonFixedDriveWarningForm : Form
    {
        public NonFixedDriveWarningForm ()
        {
            InitializeComponent();
        }

        protected override void OnFormClosing (FormClosingEventArgs e)
        {
            Properties.GUI.Settings.Default.WarnAboutNonFixedDrive = !doNotShowCheckBox.Checked;
            Properties.GUI.Settings.Default.Save();

            base.OnFormClosing(e);
        }
    }
}
