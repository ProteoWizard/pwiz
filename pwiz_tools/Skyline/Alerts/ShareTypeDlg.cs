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
using System.Windows.Forms;
using pwiz.Skyline.Model;

namespace pwiz.Skyline.Alerts
{
    /// <summary>
    /// Use for a <see cref="MessageBox"/> substitute that can be
    /// detected and closed by automated functional tests.
    /// </summary>
    public partial class ShareTypeDlg : Form
    {
        public ShareTypeDlg(SrmDocument document)
        {
            InitializeComponent();

            labelMessage.Text = "The document can be shared either in its complete form, or in a minimal form intended for read-only use with ";
            if (document.Settings.HasLibraries && document.Settings.HasBackgroundProteome)
                labelMessage.Text += "its background proteome disconnected, and all libraries minimized to contain only precursors used in the document.";
            else if (document.Settings.HasBackgroundProteome)
                labelMessage.Text += "its background proteome disconnected.";
            else if (document.Settings.HasLibraries)
                labelMessage.Text += "all libraries minimized to contain only precursors used in the document.";
            else
                throw new InvalidOperationException("Invalide use of " + typeof(ShareTypeDlg).Name + " for document without background proteome or libraries.");
            labelMessage.Text += "\nChoose the appropriate sharing option below.";
        }

        public bool IsCompleteSharing { get; set; }

        protected override void CreateHandle()
        {
            base.CreateHandle();

            Text = Program.Name;
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnComplete_Click(object sender, System.EventArgs e)
        {
            IsCompleteSharing = true;
            OkDialog();
        }
    }
}
