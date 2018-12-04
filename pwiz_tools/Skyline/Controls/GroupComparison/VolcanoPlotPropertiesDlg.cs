/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.GroupComparison
{
    public partial class VolcanoPlotPropertiesDlg : FormEx
    {
        private readonly double _oldLog2FoldChangeCutoff;
        private readonly double _oldPValueCutoff;
        private readonly bool _oldFilterVolcanoPlotPoints;

        private const string FORMAT = "0.######";

        public VolcanoPlotPropertiesDlg()
        {
            InitializeComponent();

            textFoldChange.Text = double.IsNaN(Settings.Default.Log2FoldChangeCutoff) ? string.Empty : Settings.Default.Log2FoldChangeCutoff.ToString(FORMAT, CultureInfo.CurrentCulture);
            textPValue.Text = double.IsNaN(Settings.Default.PValueCutoff) ? string.Empty : Settings.Default.PValueCutoff.ToString(FORMAT, CultureInfo.CurrentCulture);

            _oldLog2FoldChangeCutoff = Settings.Default.Log2FoldChangeCutoff;
            _oldPValueCutoff = Settings.Default.PValueCutoff;
            _oldFilterVolcanoPlotPoints = Settings.Default.FilterVolcanoPlotPoints;

            // This will call Preview and update the graphs in case someone messed with our filters
            checkBoxFilter.Checked = Settings.Default.FilterVolcanoPlotPoints;
           
            // Default of checkBoxLog.Checked is true, setting it to true here would cause problems
            if (!Settings.Default.VolcanoPlotPropertiesLog)
                checkBoxLog.Checked = Settings.Default.VolcanoPlotPropertiesLog;
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            var foldChangeCutoff = double.NaN;
            if (!string.IsNullOrEmpty(textFoldChange.Text) && !helper.ValidateDecimalTextBox(textFoldChange, checkBoxLog.Checked ? (double?)null : 0.0, null, out foldChangeCutoff, false))
                return;

            var pValueCutoff = double.NaN;
            if (!string.IsNullOrEmpty(textPValue.Text) && !helper.ValidateDecimalTextBox(textPValue, 0.0, checkBoxLog.Checked ? (double?)null : 1.0, out pValueCutoff, checkBoxLog.Checked))
                return;

            Settings.Default.Log2FoldChangeCutoff = Math.Abs(checkBoxLog.Checked ? foldChangeCutoff : ConvertBetweenLogs(foldChangeCutoff, true, 2));
            Settings.Default.PValueCutoff = checkBoxLog.Checked ? pValueCutoff : ConvertBetweenLogs(pValueCutoff, true, 10, true);
            Settings.Default.FilterVolcanoPlotPoints = checkBoxFilter.Checked;
            Settings.Default.VolcanoPlotPropertiesLog = checkBoxLog.Checked;

            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void Preview(object sender, EventArgs e)
        {
            var log = checkBoxLog.Checked;
            double foldChangeCutoff;
            if (double.TryParse(textFoldChange.Text, out foldChangeCutoff) && (log || foldChangeCutoff > 0.0))
                Settings.Default.Log2FoldChangeCutoff = Math.Abs(log ? foldChangeCutoff : ConvertBetweenLogs(foldChangeCutoff, true, 2));
            else
                Settings.Default.Log2FoldChangeCutoff = double.NaN;
            
            double pValueCutoff;
            if (double.TryParse(textPValue.Text, out pValueCutoff) && pValueCutoff >= 0.0 && (log || pValueCutoff <= 1.0))
                Settings.Default.PValueCutoff = log ? pValueCutoff : ConvertBetweenLogs(pValueCutoff, true, 10, true);
            else
                Settings.Default.PValueCutoff = double.NaN;

            Settings.Default.FilterVolcanoPlotPoints = checkBoxFilter.Checked;

            FormUtil.OpenForms.OfType<FoldChangeVolcanoPlot>().ForEach(v => v.UpdateGraph(Settings.Default.FilterVolcanoPlotPoints));
        }

        private void checkBoxLog_CheckedChanged(object sender, EventArgs e)
        {
            var log = checkBoxLog.Checked;
            UpdateTextBoxAndLabel(textFoldChange, foldChangeUnitLabel, log, 2);
            UpdateTextBoxAndLabel(textPValue, pValueLowerBoundLabel, log, 10, true);
        }

        private void VolcanoPlotProperties_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (DialogResult != DialogResult.OK)
            {
                if (Settings.Default.Log2FoldChangeCutoff != _oldLog2FoldChangeCutoff ||
                    Settings.Default.PValueCutoff != _oldPValueCutoff ||
                    Settings.Default.FilterVolcanoPlotPoints != _oldFilterVolcanoPlotPoints)
                {
                    Settings.Default.Log2FoldChangeCutoff = _oldLog2FoldChangeCutoff;
                    Settings.Default.PValueCutoff = _oldPValueCutoff;
                    Settings.Default.FilterVolcanoPlotPoints = _oldFilterVolcanoPlotPoints;
                }
            }

            FormUtil.OpenForms.OfType<FoldChangeVolcanoPlot>().ForEach(v => v.UpdateGraph(Settings.Default.FilterVolcanoPlotPoints));
        }

        private double ConvertBetweenLogs(double value, bool log, int logBase, bool negate = false)
        {
            if (log)
            {
                value = Math.Log(value, logBase);
                if (negate)
                    value = -value;
            }
            else
            {
                if (negate)
                    value = -value;
                value = Math.Pow(logBase, value);
            }

            return value;
        }

        private void UpdateTextBoxAndLabel(TextBox textBox, Label label, bool log, int logBase, bool negate = false)
        {
            var text = textBox.Text;
            textBox.Text = string.Empty;

            double value;
            if (double.TryParse(text, out value))
            {
                value = ConvertBetweenLogs(value, log, logBase, negate);
                if (!double.IsNaN(value) && !double.IsInfinity(value))
                    textBox.Text = value.ToString(FORMAT, CultureInfo.CurrentCulture);
            }

            label.Visible = log;
        }

        #region Functional Test Support

        public TextBox TextFoldChangeCutoff { get { return textFoldChange; } }
        public TextBox TextPValueCutoff { get { return textPValue; } }
        public CheckBox CheckBoxLog { get { return checkBoxLog; } }
        public CheckBox CheckBoxFilter { get { return checkBoxFilter; } }

        #endregion
    }
}
