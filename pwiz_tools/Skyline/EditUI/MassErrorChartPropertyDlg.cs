/*
 * Original author: Alex MacLean <alexmaclean2000 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    public partial class MassErrorChartPropertyDlg : FormEx
    {
        public MassErrorChartPropertyDlg()
        {
            InitializeComponent();

            if (Settings.Default.MinMassError != 0)
                textMin.Text = Settings.Default.MinMassError.ToString(LocalizationHelper.CurrentCulture);
            if (Settings.Default.MaxMassError != 0)
                textMax.Text = Settings.Default.MaxMassError.ToString(LocalizationHelper.CurrentCulture);
            GraphFontSize.PopulateCombo(textSizeComboBox, Settings.Default.AreaFontSize);
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            double min = 0;
            if (!string.IsNullOrEmpty(textMin.Text))
            {
                if (!helper.ValidateDecimalTextBox(textMin, Double.MinValue, 0, out min))
                    return;
            }


            double max = 0;
            if (!string.IsNullOrEmpty(textMax.Text))
            {
                if (!helper.ValidateDecimalTextBox(textMax, 0, Double.MaxValue, out max))
                    return;
            }


            Settings.Default.MaxMassError = max;
            Settings.Default.MinMassError = min;
            Settings.Default.AreaFontSize = GraphFontSize.GetFontSize(textSizeComboBox).PointSize;

            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }
    }
}
