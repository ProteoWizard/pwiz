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
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditDPDlg : Form
    {
        private DeclusteringPotentialRegression _regression;
        private readonly IEnumerable<DeclusteringPotentialRegression> _existing;
        private bool _clickedOk;

        private readonly MessageBoxHelper _helper;

        public EditDPDlg(IEnumerable<DeclusteringPotentialRegression> existing)
        {
            _existing = existing;

            InitializeComponent();

            _helper = new MessageBoxHelper(this);
        }

        public DeclusteringPotentialRegression Regression
        {
            get { return _regression; }
            
            set
            {
                _regression = value;
                if (_regression == null)
                {
                    textName.Text = "";
                }
                else
                {
                    textName.Text = _regression.Name;
                    textSlope.Text = _regression.Slope.ToString();
                    textIntercept.Text = _regression.Intercept.ToString();
                }                
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_clickedOk)
            {
                _clickedOk = false; // Reset in case of failure.

                string name;
                if (!_helper.ValidateNameTextBox(e, textName, out name))
                    return;

                double slope;
                if (!_helper.ValidateDecimalTextBox(e, textSlope, out slope))
                    return;

                double intercept;
                if (!_helper.ValidateDecimalTextBox(e, textIntercept, out intercept))
                    return;

                DeclusteringPotentialRegression regression =
                    new DeclusteringPotentialRegression(name, slope, intercept);

                if (_regression == null && _existing.Contains(regression))
                {
                    _helper.ShowTextBoxError(textName, "The retention time regression '{0}' already exists.", name);
                    e.Cancel = true;
                    return;
                }

                _regression = regression;
            }

            base.OnClosing(e);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            _clickedOk = true;
        }
    }
}
