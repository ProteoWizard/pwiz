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
using System.IO;
using System.Windows.Forms;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI
{
    public partial class ImportResultsSamplesDlg : FormEx
    {
        private readonly List<int> _sampleIndices = new List<int>();

        public ImportResultsSamplesDlg(string filePath, IEnumerable<string> sampleNames)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            labelFile.Text = Path.GetFileName(filePath);
            foreach (var sampleName in sampleNames)
            {
                int index = listSamples.Items.Add(sampleName);
                listSamples.SetItemChecked(index, true);
            }
            cbSelectAll.Checked = true;
        }

        public List<int> SampleIndices { get { return _sampleIndices; } }

        public void IncludeSample(int index)
        {
            listSamples.SetItemChecked(index, true);
        }

        public void ExcludeSample(int index)
        {
            listSamples.SetItemChecked(index, false);
        }

        private void cbSelectAll_CheckedChanged(object sender, EventArgs e)
        {
            CheckAll(cbSelectAll.Checked);
        }

        public void CheckAll(bool checkAll)
        {
            
            for (int i = 0; i < listSamples.Items.Count; i++)
                listSamples.SetItemChecked(i, checkAll);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            foreach (int index in listSamples.CheckedIndices)
                _sampleIndices.Add(index);
            DialogResult = DialogResult.OK;
        }
    }
}
