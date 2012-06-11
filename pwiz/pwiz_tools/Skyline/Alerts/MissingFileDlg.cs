/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{
    public partial class MissingFileDlg : FormEx
    {
        private string _itemName;
        private string _itemType;
        public MissingFileDlg()
        {
            InitializeComponent();
            Text = Program.Name;
        }

        public const string TEXT = "This document requires the '{0}' {1}.\n" +
                                    "Click the Browse button specify its location on your system.\n" +
                                    "Click the Open button to open the document without the {1}.";

        public string FileDlgInitialPath { get; set; }
        public string FileHint { get; set; }
        //File extension filter
        public string Filter { get; set; }
        //Title bar text
        public string Title { get; set; }
        //Text to put in the initial dialog: something like 'My Background Proteome'
        public string ItemName
        {
            get { return _itemName; }
            set 
            { 
                _itemName = value;
                labelMessage.Text = string.Format(TEXT, _itemName, ItemType);
            }
        }
        //The name of the type of thing that is missing: something like 'background proteome' or 'calculator database'
        public string ItemType
        {
            get { return _itemType; }
            set 
            { 
                _itemType = value;
                labelMessage.Text = string.Format(TEXT, _itemName, _itemType);
            }
        }

        public string FilePath { get; private set; }

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
            var openFileDialog = new OpenFileDialog
            {
                Filter = Filter,
                InitialDirectory = FileDlgInitialPath,
                Title = Title,
                CheckFileExists = true,
                FileName = FileHint
            };
            if (openFileDialog.ShowDialog() == DialogResult.Cancel)
                return;

            FilePath = openFileDialog.FileName;
            OkDialog();
        }
    }
}
