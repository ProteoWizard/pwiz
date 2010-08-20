/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditLabelTypeListDlg : Form
    {
        public EditLabelTypeListDlg()
        {
            InitializeComponent();
        }

        public IEnumerable<IsotopeLabelType> LabelTypes
        {
            get
            {
                using (var reader = new StringReader(textLabelTypes.Text))
                {
                    int typeOrder = IsotopeLabelType.light.SortOrder + 1;

                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (!string.IsNullOrEmpty(line))
                            yield return new IsotopeLabelType(line, typeOrder++);
                    }
                }
            }

            set
            {
                var sb = new StringBuilder();
                foreach (var labelType in value)
                    sb.AppendLine(labelType.Name);
                textLabelTypes.Text = sb.ToString();
            }
        }

        public string LabelTypeText
        {
            get { return textLabelTypes.Text; }
            set { textLabelTypes.Text = value; }
        }

        private void btnOk_Click(object sender, System.EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            var setLabelTypeNames = new HashSet<string>();
            foreach (var labelType in LabelTypes)
            {
                if (Equals(labelType.Name, IsotopeLabelType.light.Name))
                {
                    MessageDlg.Show(this, string.Format("The name '{0}' is not allowed for an isotope label type.", IsotopeLabelType.light));
                    return;
                }
                string labelTypeNameLower = labelType.Name.ToLower();
                if (setLabelTypeNames.Contains(labelTypeNameLower))
                {
                    MessageDlg.Show(this, string.Format("The label name '{0}' may not be used more than once.", labelType.Name));
                    return;
                }
                setLabelTypeNames.Add(labelTypeNameLower);
            }
            // If everything was deleted, force at least the default heavy label name
            if (setLabelTypeNames.Count == 0)
                textLabelTypes.Text = IsotopeLabelType.heavy.Name;

            DialogResult = DialogResult.OK;
        }
    }
}
