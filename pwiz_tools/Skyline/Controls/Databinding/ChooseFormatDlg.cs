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
using System.Linq;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Databinding
{
    public partial class ChooseFormatDlg : FormEx
    {
        private bool _inChangeText;
        public ChooseFormatDlg(DataSchemaLocalizer dataSchemaLocalizer)
        {
            InitializeComponent();
            comboFormat.Items.Add(string.Empty);
            comboFormat.Items.AddRange(FormatSuggestion.ListFormatSuggestions(dataSchemaLocalizer.FormatProvider).ToArray());
        }

        public string FormatText
        {
            get { return tbxCustomFormat.Text; }
            set
            {
                tbxCustomFormat.Text = value;
            }
        }

        private void comboFormat_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            if (_inChangeText)
            {
                return;
            }
            var formatSuggestion = comboFormat.SelectedItem as FormatSuggestion;
            if (formatSuggestion == null)
            {
                return;
            }
            tbxCustomFormat.Text = formatSuggestion.FormatString;
        }

        private void tbxCustomFormat_TextChanged(object sender, System.EventArgs e)
        {
            if (_inChangeText)
            {
                return;
            }
            try
            {
                _inChangeText = true;
                string text = tbxCustomFormat.Text;
                int selectedIndex = 0;
                for (int i = 0; i < comboFormat.Items.Count; i++)
                {
                    var formatSuggestion = comboFormat.Items[i] as FormatSuggestion;
                    if (formatSuggestion != null && formatSuggestion.FormatString == text)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
                comboFormat.SelectedIndex = selectedIndex;
            }
            finally
            {
                _inChangeText = false;
            }
        }

        private void lblCustomFormatString_LinkClicked(object sender, System.Windows.Forms.LinkLabelLinkClickedEventArgs e)
        {
            WebHelpers.OpenSkylineShortLink(this, @"helptopic-docgridformats");
        }
    }
}
