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
using System.Globalization;
using pwiz.Common.SystemUtil;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    public partial class SpectrumChartPropertyDlg : FormEx
    {
        public SpectrumChartPropertyDlg()
        {
            InitializeComponent();

            textLineWidth.Text = Settings.Default.SpectrumLineWidth.ToString(LocalizationHelper.CurrentCulture);
            GraphFontSize.PopulateCombo(textSizeComboBox, Settings.Default.SpectrumFontSize);
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            int lineWidth;
            if (!helper.ValidateNumberTextBox(textLineWidth, 1, 5, out lineWidth))
                return;


            Settings.Default.SpectrumLineWidth = lineWidth;
            Settings.Default.SpectrumFontSize = GraphFontSize.GetFontSize(textSizeComboBox).PointSize;
            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        #region Functional test support

        public int LineWidth
        {
            get { return int.Parse(textLineWidth.Text); }
            set { textLineWidth.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public GraphFontSize FontSize
        {
            get { return textSizeComboBox.SelectedItem as GraphFontSize; }
            set { textSizeComboBox.SelectedItem = value; }
        }

        #endregion
    }
}
