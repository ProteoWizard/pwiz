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
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditLabelTypeListDlg : FormEx
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
            // All names must produce unique (case insensitive) IDs, since IDs are used
            // for the names in reports, and the SQL column names used in reports are case insensitive.
            var dictIdsToNames = new Dictionary<string, string>();
            foreach (var labelType in LabelTypes)
            {
                string lowerCaseId = Helpers.MakeId(labelType.Name).ToLower();
                if (Equals(lowerCaseId, IsotopeLabelType.light.Name))
                {
                    MessageDlg.Show(this, string.Format("The name '{0}' conflicts with the default light isotope label type.", labelType.Name));
                    textLabelTypes.Focus();
                    return;
                }
                string nameExisting;
                if (dictIdsToNames.TryGetValue(lowerCaseId, out nameExisting))
                {
                    if (Equals(nameExisting, labelType.Name))
                        MessageDlg.Show(this, string.Format("The label name '{0}' may not be used more than once.", labelType.Name));
                    else
                        MessageDlg.Show(this, string.Format("The label names '{0}' and '{1}' conflict.  Use more unique names.", nameExisting, labelType.Name));
                    textLabelTypes.Focus();
                    return;
                }
                dictIdsToNames.Add(lowerCaseId, labelType.Name);
            }
            // If everything was deleted, force at least the default heavy label name
            if (dictIdsToNames.Count == 0)
                textLabelTypes.Text = IsotopeLabelType.heavy.Name;

            DialogResult = DialogResult.OK;
        }
    }
}
