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
using System.ComponentModel;
using System.Globalization;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    public partial class SpectrumChartPropertyDlg : FormEx
    {
        private bool _clickedOk;

        private readonly MessageBoxHelper _helper;

        public SpectrumChartPropertyDlg()
        {
            InitializeComponent();

            _helper = new MessageBoxHelper(this);

            textLineWidth.Text = Settings.Default.SpectrumLineWidth.ToString(CultureInfo.CurrentCulture);
            textFontSize.Text = Settings.Default.SpectrumFontSize.ToString(CultureInfo.CurrentCulture);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_clickedOk)
            {
                _clickedOk = false; // Reset in case of failure.

                int lineWidth;
                if (!_helper.ValidateNumberTextBox(e, textLineWidth, 1, 5, out lineWidth))
                    return;

                int fontSize;
                if (!_helper.ValidateNumberTextBox(e, textFontSize, 6, 128, out fontSize))
                    return;

                Settings.Default.SpectrumLineWidth = lineWidth;
                Settings.Default.SpectrumFontSize = fontSize;
            }

            base.OnClosing(e);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            _clickedOk = true;
        }
    }
}
