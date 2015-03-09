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
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI
{
    public partial class RenameResultDlg : FormEx
    {
        public RenameResultDlg(string name, IEnumerable<string> existing)
        {
            InitializeComponent();

            textName.Text = name;

            ExistingNames = existing;
        }

        private IEnumerable<string> ExistingNames { get; set; }

        public string ReplicateName
        {
            get { return textName.Text; }
            set
            {
                if (ExistingNames.Contains(value))
                    throw new InvalidOperationException(string.Format(Resources.RenameResultDlg_ReplicateName_The_name__0__is_already_in_use, value));
                textName.Text = value;
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            if (ExistingNames.Contains(textName.Text))
            {
                var message = TextUtil.LineSeparate(
                    string.Format(Resources.RenameResultDlg_ReplicateName_The_name__0__is_already_in_use, textName.Text),
                    Resources.RenameResultDlg_OkDialog_Please_use_a_different_name);
                MessageDlg.Show(this, message);
                return;
            }
            DialogResult = DialogResult.OK;
        }
    }
}
