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
using System.Collections.Generic;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.FileUI
{
    public partial class RemoveResultsDlg : Form
    {
        private readonly MeasuredResults _results;
        private readonly List<string> _removeNames = new List<string>();

        private bool _clickedOk;

        public RemoveResultsDlg(SrmDocument document)
        {
            _results = document.Settings.MeasuredResults;

            InitializeComponent();

            UpdateResultsList();
        }

        public List<string> RemoveNames
        {
            get { return _removeNames; }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_clickedOk)
            {
                _clickedOk = false; // Reset in case of failure.

                foreach (var item in listResults.CheckedItems)
                    _removeNames.Add(item.ToString());
            }

            base.OnClosing(e);
        }

        private void cbSelectAll_CheckedChanged(object sender, System.EventArgs e)
        {
            bool checkAll = cbSelectAll.Checked;
            for (int i = 0; i < listResults.Items.Count; i++)
                listResults.SetItemChecked(i, checkAll);
            // Make sure changing the list checkboxes does not update cbSelectAll
            cbSelectAll.Checked = checkAll;
        }

        private void listResults_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (listResults.Items.Count == listResults.CheckedItems.Count + 1 && e.NewValue == CheckState.Checked)
                cbSelectAll.Checked = true;
            else if (listResults.CheckedItems.Count == 1 && e.NewValue == CheckState.Unchecked)
                cbSelectAll.Checked = false;
        }

        private void UpdateResultsList()
        {
            if (_results == null)
                return;

            foreach (var set in _results.Chromatograms)
                listResults.Items.Add(set.Name);
        }

        private void btnOk_Click(object sender, System.EventArgs e)
        {
            _clickedOk = true;
        }
    }
}
