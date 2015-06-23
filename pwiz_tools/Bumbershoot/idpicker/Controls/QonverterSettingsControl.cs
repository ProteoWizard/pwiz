//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using IDPicker.DataModel;

namespace IDPicker.Controls
{
    /// <summary>
    /// Allows viewing and manipulation of a QonverterSettings instance.
    /// </summary>
    public partial class QonverterSettingsControl : UserControl
    {
        public QonverterSettingsControl ()
        {
            InitializeComponent();

            scoreNameColumn.ValueType = typeof(string);
            scoreWeightColumn.ValueType = typeof(double);
            scoreOrderColumn.Items.AddRange(Enum.GetNames(typeof(Qonverter.Settings.Order)));
            scoreNormalizationColumn.Items.AddRange(Enum.GetNames(typeof(Qonverter.Settings.NormalizationMethod)));
        }

        private QonverterSettings qonverterSettings = null;
        public QonverterSettings QonverterSettings
        {
            set
            {
                qonverterSettings = value;
                if(qonverterSettings == null)
                    return;

                qonvertMethodComboBox.SelectedIndex = (int) value.QonverterMethod;
                chargeStateHandlingComboBox.SelectedIndex = Math.Min(3, (int) value.ChargeStateHandling)-1;
                terminalSpecificityHandlingComboBox.SelectedIndex = Math.Min(3, (int) value.TerminalSpecificityHandling)-1;
                massErrorHandlingComboBox.SelectedIndex = (int) value.MassErrorHandling;
                missedCleavagesComboBox.SelectedIndex = (int) value.MissedCleavagesHandling;
                kernelComboBox.SelectedIndex = (int) value.Kernel;
                rerankingCheckbox.Checked = value.RerankMatches;
                optimizeAtFdrTextBox.Text = (value.MaxFDR * 100).ToString("f4", CultureInfo.CurrentCulture);

                scoreGridView.Rows.Clear();
                foreach (var kvp in value.ScoreInfoByName)
                    scoreGridView.Rows.Add(kvp.Key,
                                           kvp.Value.Weight,
                                           Enum.GetName(typeof(Qonverter.Settings.Order), kvp.Value.Order),
                                           Enum.GetName(typeof(Qonverter.Settings.NormalizationMethod), kvp.Value.NormalizationMethod));
            }

            get { return qonverterSettings; }
        }

        public QonverterSettings EditedQonverterSettings
        {
            get
            {
                var qonverterSettings = new QonverterSettings()
                {
                    QonverterMethod = (Qonverter.QonverterMethod) qonvertMethodComboBox.SelectedIndex,
                    ChargeStateHandling = (Qonverter.ChargeStateHandling) (chargeStateHandlingComboBox.SelectedIndex > 1 ? 2 : chargeStateHandlingComboBox.SelectedIndex+1),
                    TerminalSpecificityHandling = (Qonverter.TerminalSpecificityHandling) (terminalSpecificityHandlingComboBox.SelectedIndex > 1 ? 2 : terminalSpecificityHandlingComboBox.SelectedIndex+1),
                    MassErrorHandling = (Qonverter.MassErrorHandling) massErrorHandlingComboBox.SelectedIndex,
                    MissedCleavagesHandling = (Qonverter.MissedCleavagesHandling) missedCleavagesComboBox.SelectedIndex,
                    Kernel = (Qonverter.Kernel) kernelComboBox.SelectedIndex,
                    RerankMatches = rerankingCheckbox.Checked,
                    MaxFDR = Convert.ToDouble(optimizeAtFdrTextBox.Text, CultureInfo.CurrentCulture) / 100,
                    ScoreInfoByName = new Dictionary<string, Qonverter.Settings.ScoreInfo>()
                };

                foreach (DataGridViewRow row in scoreGridView.Rows)
                {
                    if (row.IsNewRow)
                        continue;

                    var scoreInfo = new Qonverter.Settings.ScoreInfo()
                    {
                        Weight = (double) row.Cells[1].Value,
                        Order = (Qonverter.Settings.Order) scoreOrderColumn.Items.IndexOf((string) row.Cells[2].Value),
                        NormalizationMethod = (Qonverter.Settings.NormalizationMethod) scoreNormalizationColumn.Items.IndexOf((string) row.Cells[3].Value)
                    };
                    qonverterSettings.ScoreInfoByName[(string) row.Cells[0].Value] = scoreInfo;
                }
                return qonverterSettings;
            }
        }

        public void CommitChanges ()
        {
            qonverterSettings = EditedQonverterSettings;
        }

        public bool IsDirty
        {
            get
            {
                if (qonverterSettings == null)
                    return false;

                var editedQonverterSettings = EditedQonverterSettings;
                bool isDirty = qonverterSettings.QonverterMethod != editedQonverterSettings.QonverterMethod ||
                               qonverterSettings.ChargeStateHandling != editedQonverterSettings.ChargeStateHandling ||
                               qonverterSettings.TerminalSpecificityHandling != editedQonverterSettings.TerminalSpecificityHandling ||
                               qonverterSettings.MassErrorHandling != editedQonverterSettings.MassErrorHandling ||
                               qonverterSettings.MissedCleavagesHandling != editedQonverterSettings.MissedCleavagesHandling ||
                               qonverterSettings.Kernel != editedQonverterSettings.Kernel ||
                               qonverterSettings.RerankMatches != editedQonverterSettings.RerankMatches ||
                               qonverterSettings.MaxFDR != editedQonverterSettings.MaxFDR ||
                               qonverterSettings.ScoreInfoByName.Count != editedQonverterSettings.ScoreInfoByName.Count;

                if (isDirty)
                    return true;

                foreach (var kvp in qonverterSettings.ScoreInfoByName)
                {
                    if (editedQonverterSettings.ScoreInfoByName.ContainsKey(kvp.Key))
                    {
                        var scoreInfo = editedQonverterSettings.ScoreInfoByName[kvp.Key];
                        if (scoreInfo.Weight != kvp.Value.Weight ||
                            scoreInfo.Order != kvp.Value.Order ||
                            scoreInfo.NormalizationMethod != kvp.Value.NormalizationMethod)
                            return true;
                    }
                    else
                        return true;
                }

                return false;
            }
        }

        private void doubleTextBox_KeyPress (object sender, KeyPressEventArgs e)
        {
            var textBox = sender as TextBoxBase;
            if (textBox == null)
                return;

            if (e.KeyChar.ToString() == CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator)
            {
                if (textBox.Text.Length == 0)
                {
                    textBox.Text = "0" + CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
                    e.Handled = true;
                    textBox.SelectionStart = textBox.TextLength;
                }
                else if (textBox.Text.Contains(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator))
                    e.Handled = true;
            }
            else if (e.KeyChar != '\b' && (e.KeyChar < '0' || e.KeyChar > '9'))
                e.Handled = true;
        }

        private void flowLayoutPanel_Resize (object sender, EventArgs e)
        {
            scoreGridViewPanel.Width = flowLayoutPanel.Width;
        }

        private void qonvertMethodComboBox_SelectedIndexChanged (object sender, EventArgs e)
        {
            // hide step optimization FDR options for non-step methods
            bool showStepOptimizerOptions = qonvertMethodComboBox.SelectedIndex == (int) Qonverter.QonverterMethod.MonteCarlo;
            stepOptimizerPanel.Visible = showStepOptimizerOptions;

            // hide kernel and non-score-feature options for non-SVM methods
            bool showNonScoreOptions = qonvertMethodComboBox.SelectedIndex == (int) Qonverter.QonverterMethod.PartitionedSVM ||
                                       qonvertMethodComboBox.SelectedIndex == (int) Qonverter.QonverterMethod.SingleSVM;
            svmPanel.Visible = showNonScoreOptions;

            if (showNonScoreOptions)
            {
                massErrorHandlingComboBox.SelectedIndex = (int) Qonverter.MassErrorHandling.Ignore;
                missedCleavagesComboBox.SelectedIndex = (int) Qonverter.MissedCleavagesHandling.Feature;
            }
            else
            {
                massErrorHandlingComboBox.SelectedIndex = (int) Qonverter.MassErrorHandling.Ignore;
                missedCleavagesComboBox.SelectedIndex = (int) Qonverter.MissedCleavagesHandling.Ignore;
            }
        }
    }
}