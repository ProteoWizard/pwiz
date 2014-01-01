/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Topograph.MsData;
using pwiz.Topograph.ui.Properties;

namespace pwiz.Topograph.ui.Controls
{
    public partial class HalfLifeSettingsControl : UserControl
    {
        private bool _isExpanded = true;
        private HalfLifeSettings _halfLifeSettings;
        public HalfLifeSettingsControl()
        {
            InitializeComponent();
            AutoSize = true;
            foreach (var value in Enum.GetValues(typeof(EvviesFilterEnum)))
            {
                comboEvviesFilter.Items.Add(value);
            }
        }

        private bool _inChangeSettings;
        public HalfLifeSettings HalfLifeSettings
        {
            get { return _halfLifeSettings; }
            set 
            { 
                if (_inChangeSettings)
                {
                    return;
                }
                try
                {
                    _inChangeSettings = true;
                    bool changed = !Equals(_halfLifeSettings, value);
                    _halfLifeSettings = value;
                    switch (value.NewlySynthesizedTracerQuantity)
                    {
                        case TracerQuantity.LabeledAminoAcid:
                            radioLabeledAminoAcid.Checked = true;
                            break;
                        case TracerQuantity.PartialLabelDistribution:
                            radioLabelDistribution.Checked = true;
                            break;
                        case TracerQuantity.UnlabeledPeptide:
                            radioUnlabeledPeptide.Checked = true;
                            break;
                    }
                    tbxInitialPrecursorPool.Text = value.InitialPrecursorPool.ToString(CultureInfo.CurrentCulture);
                    if (value.PrecursorPoolCalculation == PrecursorPoolCalculation.Fixed)
                    {
                        radioFixedPrecursorPool.Checked = true;
                        tbxCurrentPrecursorPool.Enabled = true;
                    }
                    else
                    {
                        tbxCurrentPrecursorPool.Enabled = false;
                        if (value.PrecursorPoolCalculation == PrecursorPoolCalculation.MedianPerSample)
                        {
                            radioUseMedianPrecursorPool.Checked = true;
                        }
                        else if (value.PrecursorPoolCalculation == PrecursorPoolCalculation.Individual)
                        {
                            radioIndividualPrecursorPool.Checked = true;
                        }
                    }
                    tbxCurrentPrecursorPool.Text = value.CurrentPrecursorPool.ToString(CultureInfo.CurrentCulture);
                    tbxMinAuc.Text = value.MinimumAuc.ToString(CultureInfo.CurrentCulture);
                    tbxMinimumDeconvolutionScore.Text = value.MinimumDeconvolutionScore.ToString(CultureInfo.CurrentCulture);
                    tbxMinTurnoverScore.Text = value.MinimumTurnoverScore.ToString(CultureInfo.CurrentCulture);
                    comboEvviesFilter.SelectedIndex = (int)value.EvviesFilter;

                    cbxForceThroughOrigin.Checked = value.ForceThroughOrigin;
                    cbxSimpleLinearRegression.Checked = value.SimpleLinearRegression;

                    if (changed)
                    {
                        var handler = SettingsChange;
                        if (handler != null)
                        {
                            handler.Invoke(this, new EventArgs());
                        }
                    }
                }
                finally
                {
                    _inChangeSettings = false;
                }
            }
        }

        private HalfLifeSettings GetHalfLifeSettingsFromUi(HalfLifeSettings value)
        {
            value.NewlySynthesizedTracerQuantity
                = radioLabeledAminoAcid.Checked
                      ? TracerQuantity.LabeledAminoAcid
                      : radioLabelDistribution.Checked
                            ? TracerQuantity.PartialLabelDistribution
                            : radioUnlabeledPeptide.Checked
                                  ? TracerQuantity.UnlabeledPeptide : 0;
            value.InitialPrecursorPool = HalfLifeSettings.TryParseDouble(tbxInitialPrecursorPool.Text, 0);
            value.CurrentPrecursorPool = HalfLifeSettings.TryParseDouble(tbxCurrentPrecursorPool.Text, 0);
            value.PrecursorPoolCalculation = radioFixedPrecursorPool.Checked
                                                 ? PrecursorPoolCalculation.Fixed
                                                 : radioUseMedianPrecursorPool.Checked
                                                       ? PrecursorPoolCalculation.MedianPerSample
                                                       : radioIndividualPrecursorPool.Checked
                                                             ? PrecursorPoolCalculation.Individual
                                                             : 0;
            value.MinimumAuc = HalfLifeSettings.TryParseDouble(tbxMinAuc.Text, 0);
            value.MinimumDeconvolutionScore = HalfLifeSettings.TryParseDouble(tbxMinimumDeconvolutionScore.Text, 0);
            value.MinimumTurnoverScore = HalfLifeSettings.TryParseDouble(tbxMinTurnoverScore.Text, 0);
            value.EvviesFilter = (EvviesFilterEnum)comboEvviesFilter.SelectedIndex;
            
            value.ForceThroughOrigin = cbxForceThroughOrigin.Checked;
            value.SimpleLinearRegression = cbxSimpleLinearRegression.Checked;
            return value;
        }

        public event EventHandler SettingsChange;

        private void UpdateSettings(object sender, EventArgs e)
        {
            HalfLifeSettings = GetHalfLifeSettingsFromUi(HalfLifeSettings);
        }

        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                _isExpanded = value;
                if (IsExpanded)
                {
                    panelContent.AutoSize = true;
                    imgExpandCollapse.Image = Resources.Collapse;
                }
                else
                {
                    panelContent.AutoSize = false;
                    panelContent.Height = 0;
                    imgExpandCollapse.Image = Resources.Expand;
                }
            }
        }

        private void ImgExpandCollapseOnClick(object sender, EventArgs e)
        {
            IsExpanded = !IsExpanded;
        }
    }
}
