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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditFragmentLossDlg : FormEx
    {
        private FragmentLoss _loss;
        private readonly IEnumerable<FragmentLoss> _existing;
        private readonly FormulaBox _formulaBox;

        private readonly int _libraryInclusionIndex;

        public EditFragmentLossDlg(IEnumerable<FragmentLoss> existing)
        {
            InitializeComponent();

            _existing = existing;
            _formulaBox = new FormulaBox(Resources.EditFragmentLossDlg_EditFragmentLossDlg_Neutral_loss__chemical_formula_,Resources.EditFragmentLossDlg_EditFragmentLossDlg__Monoisotopic_loss_,Resources.EditFragmentLossDlg_EditFragmentLossDlg_A_verage_loss_)
            {
                Location = new Point(12,9)
            };
            Controls.Add(_formulaBox);

            comboIncludeLoss.Items.Add(LossInclusion.Never.GetLocalizedString());
            _libraryInclusionIndex = comboIncludeLoss.Items.Count;
            comboIncludeLoss.Items.Add(LossInclusion.Library.GetLocalizedString());
            comboIncludeLoss.Items.Add(LossInclusion.Always.GetLocalizedString());
            comboIncludeLoss.SelectedIndex = _libraryInclusionIndex;
        }

        public FragmentLoss Loss
        {
            get { return _loss; }
            set
            {
                _loss = value;
                if (_loss == null)
                {
                    _formulaBox.Formula = string.Empty;
                    _formulaBox.MonoMass = null;
                    _formulaBox.AverageMass = null;
                    comboIncludeLoss.SelectedIndex = _libraryInclusionIndex;
                }
                else
                {
                    if (!string.IsNullOrEmpty(_loss.Formula))
                    {
                        _formulaBox.Formula = _loss.Formula;
                    }
                    else
                    {
                        _formulaBox.MonoMass = (_loss.MonoisotopicMass != 0 ?
                            _loss.MonoisotopicMass : (double?)null);
                        _formulaBox.AverageMass = (_loss.AverageMass != 0 ?
                            _loss.AverageMass : (double?)null);
                    }
                    Inclusion = _loss.Inclusion;
                }
            }
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            string formulaLoss = _formulaBox.Formula;
            double? monoLoss = null;
            double? avgLoss = null;
            if (!string.IsNullOrEmpty(formulaLoss))
            {
                try
                {
                    double massMono = SequenceMassCalc.ParseModMass(BioMassCalc.MONOISOTOPIC, formulaLoss);
                    double massAverage = SequenceMassCalc.ParseModMass(BioMassCalc.AVERAGE, formulaLoss);
                    if (FragmentLoss.MIN_LOSS_MASS > massMono || FragmentLoss.MIN_LOSS_MASS > massAverage)
                    {
                        _formulaBox.ShowTextBoxErrorFormula(helper, string.Format(Resources.EditFragmentLossDlg_OkDialog_Neutral_loss_masses_must_be_greater_than_or_equal_to__0__,
                                                              FragmentLoss.MIN_LOSS_MASS));
                        return;
                    }
                    if (massMono > FragmentLoss.MAX_LOSS_MASS || massAverage > FragmentLoss.MAX_LOSS_MASS)
                    {
                        _formulaBox.ShowTextBoxErrorFormula(helper, string.Format(Resources.EditFragmentLossDlg_OkDialog_Neutral_loss_masses_must_be_less_than_or_equal_to__0__,
                                                              FragmentLoss.MAX_LOSS_MASS));
                        return;
                    }
                }
                catch (ArgumentException x)
                {
                    _formulaBox.ShowTextBoxErrorFormula(helper, x.Message);
                    return;
                }
            }
            else if (_formulaBox.MonoMass != null ||
                    _formulaBox.AverageMass != null)
            {
                formulaLoss = null;
                double mass;
                if (!_formulaBox.ValidateMonoText(helper, FragmentLoss.MIN_LOSS_MASS, FragmentLoss.MAX_LOSS_MASS, out mass))
                    return;
                monoLoss = mass;
                if (!_formulaBox.ValidateAverageText(helper, FragmentLoss.MIN_LOSS_MASS, FragmentLoss.MAX_LOSS_MASS, out mass))
                    return;
                avgLoss = mass;
            }
            else
            {
                _formulaBox.ShowTextBoxErrorFormula(helper,Resources.EditFragmentLossDlg_OkDialog_Please_specify_a_formula_or_constant_masses);
                return;
            }

            // Make sure the new loss does not already exist.
            var loss = new FragmentLoss(formulaLoss, monoLoss, avgLoss, Inclusion);
            if (_existing.Contains(loss))
            {
                MessageDlg.Show(this, string.Format(Resources.EditFragmentLossDlg_OkDialog_The_loss__0__already_exists, loss));
                return;
            }

            Loss = loss;

            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public LossInclusion Inclusion
        {
            get
            {
                return LossInclusionExtension.GetEnum(comboIncludeLoss.SelectedItem.ToString(), LossInclusion.Library);
            }
            set
            {
                comboIncludeLoss.SelectedItem = value.GetLocalizedString();
            }
        }
    }
}
