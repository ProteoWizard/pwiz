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
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.EditUI
{
    public partial class GenerateDecoysDlg : Form
    {
        private readonly SrmDocument _document;
        private readonly SrmSettings _settings;

        // Number of precursor (TransitionGroup) decoys
        private int _numDecoys;
        private IsotopeLabelType _refineLabelType;

        public int NumDecoys
        {
            get { return _numDecoys; }
            set
            {
                _numDecoys = value;
                textNumberOfDecoys.Text = _numDecoys.ToString(CultureInfo.CurrentCulture);
            }
        }

        public IsotopeLabelType DecoysLabelType
        {
            get { return _refineLabelType; }
            set { _refineLabelType = value; }
        }

        public string DecoysMethod
        {
            get { return comboDecoysGenerationMethod.SelectedItem.ToString(); }
            set { comboDecoysGenerationMethod.SelectedItem = value; }
        }

        public GenerateDecoysDlg(SrmDocument document)
        {
            _document = document;
            _settings = document.Settings;

            InitializeComponent();

            Icon = Resources.Skyline;

            // Set initial decoys number
            textNumberOfDecoys.Text = (document.TransitionGroupCount/2).ToString(CultureInfo.CurrentCulture);

            // Fill label type combo box
            comboDecoysLabelType.Items.Add("All");
            comboDecoysLabelType.Items.Add(IsotopeLabelType.LIGHT_NAME);
            foreach (var typedMods in _settings.PeptideSettings.Modifications.GetHeavyModifications())
                comboDecoysLabelType.Items.Add(typedMods.LabelType.Name);
            comboDecoysLabelType.SelectedIndex = 1;

            // Fill method type combo box
            comboDecoysGenerationMethod.Items.AddRange(DecoyGeneration.Methods.Cast<object>().ToArray());
            comboDecoysGenerationMethod.SelectedIndex = 0;
        }

        public void OkDialog()
        {
            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(this);
            if (!helper.ValidateNumberTextBox(e, textNumberOfDecoys, 0, _document.TransitionGroupCount, out _numDecoys))
                return;

            _refineLabelType = null;
            string refineTypeName = comboDecoysLabelType.SelectedItem.ToString();
            if (refineTypeName != IsotopeLabelType.LIGHT_NAME)
            {
                var typedMods = _settings.PeptideSettings.Modifications.GetModificationsByName(refineTypeName);
                if (typedMods != null)
                    _refineLabelType = typedMods.LabelType;
            }
            else
                _refineLabelType = IsotopeLabelType.light;

            DialogResult = DialogResult.OK;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }
    }
}
