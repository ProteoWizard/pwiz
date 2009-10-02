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
using System.Windows.Forms;
using pwiz.Skyline.SettingsUI;

namespace pwiz.Skyline.Alerts
{
    /// <summary>
    /// Use for a <see cref="MessageBox"/> substitute that can be
    /// detected and closed by automated functional tests.
    /// </summary>
    public partial class MissingLibraryDlg : Form
    {
        private string _libraryName;

        public MissingLibraryDlg()
        {
            InitializeComponent();
        }

        public string LibraryName
        {
            get { return _libraryName; }
            set
            {
                _libraryName = value;

                labelMessage.Text = string.Format("This document requires the '{0}' spectral library.\n" +
                    "Click the Browse button specify its location on your system.\n" +
                    "Click the Open button to open the document with the library disconnected.", _libraryName);
            }
        }
        public string LibraryFileNameHint { get; set; }

        public string LibraryPath { get; private set; }

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

        private void btnOpen_Click(object sender, System.EventArgs e)
        {
            OkDialog();
        }

        private void btnBrowse_Click(object sender, System.EventArgs e)
        {
            LibraryPath = EditLibraryDlg.GetLibraryPath(this, LibraryFileNameHint);
            if (LibraryPath != null)
                OkDialog();
        }
    }
}
