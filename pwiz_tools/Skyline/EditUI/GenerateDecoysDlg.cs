/*
 * Original author: Lucia Espona <espona .at. imsb.biol.ethz.ch>,
 *                  IMSB, ETHZ
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    public partial class GenerateDecoysDlg : FormEx
    {
        private readonly SrmDocument _document;

        // Number of precursor (TransitionGroup) decoys
        private int _numDecoys;

        public int NumDecoys
        {
            get { return _numDecoys; }
            set
            {
                _numDecoys = value;
                textNumberOfDecoys.Text = _numDecoys.ToString(LocalizationHelper.CurrentCulture);
            }
        }

        public string DecoysMethod
        {
            get { return comboDecoysGenerationMethod.SelectedItem.ToString(); }
            set { comboDecoysGenerationMethod.SelectedItem = value; }
        }

        public GenerateDecoysDlg(SrmDocument document)
        {
            _document = document;

            InitializeComponent();

            Icon = Resources.Skyline;

            // Set initial decoys number
            textNumberOfDecoys.Text = RefinementSettings.SuggestDecoyCount(document).ToString(LocalizationHelper.CurrentCulture);

            // Fill method type combo box
            comboDecoysGenerationMethod.Items.AddRange(DecoyGeneration.Methods.Cast<object>().ToArray());
            comboDecoysGenerationMethod.SelectedIndex = 0;
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            int numDecoys;
            if (!helper.ValidateNumberTextBox(textNumberOfDecoys, 0, null, out numDecoys))
                return;

            int numComparableGroups = _document.Peptides.SelectMany(PeakFeatureEnumerator.ComparableGroups).Count();
            if (numComparableGroups == 0)
            {
                helper.ShowTextBoxError(textNumberOfDecoys, Resources.GenerateDecoysDlg_OkDialog_No_precursor_models_for_decoys_were_found_, null);
                return;
            }
            if (!Equals(DecoysMethod, DecoyGeneration.SHUFFLE_SEQUENCE) && numComparableGroups < numDecoys)
            {
                helper.ShowTextBoxError(textNumberOfDecoys,
                                        Resources.GenerateDecoysDlg_OkDialog__0__must_be_less_than_the_number_of_precursor_models_for_decoys__or_use_the___1___decoy_generation_method_,
                                        null, DecoyGeneration.SHUFFLE_SEQUENCE);
                return;
            }

            _numDecoys = numDecoys;
            DialogResult = DialogResult.OK;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }
    }
}
