/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Common.Properties;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Controls
{
    public partial class NameLayoutForm : CommonFormEx
    {
        private HashSet<string> _existingNames;
        public NameLayoutForm(IViewContext viewContext, HashSet<string> existing)
        {
            InitializeComponent();
            ViewContext = viewContext;
            _existingNames = existing;
        }

        public IViewContext ViewContext { get; private set; }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            if (string.IsNullOrEmpty(LayoutName))
            {
                ViewContext.ShowMessageBox(this, Resources.NameLayoutForm_OkDialog_Name_cannot_be_empty, MessageBoxButtons.OK);
                return;
            }
            if (_existingNames.Contains(LayoutName))
            {
                if (DialogResult.Yes !=ViewContext.ShowMessageBox(this, string.Format(Resources.NameLayoutForm_OkDialog_You_already_have_a_layout_named___0____Do_you_want_to_replace_it_, LayoutName), MessageBoxButtons.YesNo))
                {
                    return;
                }
            }
            DialogResult = DialogResult.OK;
        }

        public string LayoutName
        {
            get { return tbxLayoutName.Text; }
            set { tbxLayoutName.Text = value; }
        }

        public bool MakeDefault
        {
            get { return cbxMakeDefault.Checked; }
            set { cbxMakeDefault.Checked = value; }
        }
    }
}
