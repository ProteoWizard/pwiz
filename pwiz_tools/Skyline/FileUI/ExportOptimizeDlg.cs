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
using System.ComponentModel;
using System.Globalization;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI
{
    public partial class ExportOptimizeDlg : FormEx
    {
        private double _stepSize;
        private int _stepCount;

        private readonly MessageBoxHelper _helper;

        public ExportOptimizeDlg()
        {
            InitializeComponent();

            _helper = new MessageBoxHelper(this);
        }

        public IEnumerable<string> OptimizeParameterValues
        {
            set
            {
                comboOptimize.Items.Clear();
                foreach (var param in value)
                    comboOptimize.Items.Add(param);
                comboOptimize.SelectedIndex = 0;
            }
        }
        public string OptimizeParameter
        {
            get { return comboOptimize.SelectedItem.ToString(); }
            set { comboOptimize.SelectedItem = value; }
        }

        public double StepSize
        {
            get { return _stepSize; }
            set
            {
                _stepSize = value;
                textStepSize.Text = _stepSize.ToString(CultureInfo.CurrentCulture);
            }
        }

        public int StepCount
        {
            get { return _stepCount; }
            set
            {
                _stepCount = value;
                textStepCount.Text = _stepCount.ToString(CultureInfo.CurrentCulture);
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OnOk();
        }

        public void OnOk()
        {
            // TODO: Remove this
            var e = new CancelEventArgs();
            
            if (!_helper.ValidateDecimalTextBox(e, textStepSize, 0.0001, 100, out _stepSize))
                return;

            if (!_helper.ValidateNumberTextBox(e, textStepCount, 1, 10, out _stepCount))
                return;

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
