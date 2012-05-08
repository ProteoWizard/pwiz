/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.SettingsUI
{
    public partial class CalculateIsolationSchemeDlg : Form
    {
        public IList<EditIsolationWindow> IsolationWindows { get { return CreateIsolationWindows(); } }

        private const double OPTIMIZED_WINDOW_WIDTH_MULTIPLE = 1.00045475;
        private const double OPTIMIZED_WINDOW_OFFSET = 0.25;
        private const int MAX_GENERATED_WINDOWS = 1000;

        public static class WindowMargin
        {
            public const string NONE = "None";
            public const string SYMMETRIC = "Symmetric";
            public const string ASYMMETRIC = "Asymmetric";
        };

        public CalculateIsolationSchemeDlg()
        {
            InitializeComponent();

            // Initialize margins combo box.
            comboMargins.Items.AddRange(
                new object[]
                    {
                        WindowMargin.NONE,
                        WindowMargin.SYMMETRIC,
                        WindowMargin.ASYMMETRIC
                    });
            comboMargins.SelectedItem = WindowMargin.NONE;
        }

        private IList<EditIsolationWindow> CreateIsolationWindows()
        {
            var isolationWindows = new List<EditIsolationWindow>();
            double start;
            double end;
            double windowWidth;
            double overlap;
            double marginLeft = 0;
            double marginRight = 0;

            double.TryParse(textOverlap.Text, out overlap);
            switch (comboMargins.SelectedItem.ToString())
            {
                case WindowMargin.SYMMETRIC:
                    double.TryParse(textMarginLeft.Text, out marginLeft);
                    marginRight = marginLeft;
                    break;

                case WindowMargin.ASYMMETRIC:
                    double.TryParse(textMarginLeft.Text, out marginLeft);
                    double.TryParse(textMarginRight.Text, out marginRight);
                    break;
            }

            // No isolation windows if we don't have enough information or it is nonsense.
            if (!double.TryParse(textStart.Text, out start) ||
                !double.TryParse(textEnd.Text, out end) ||
                !double.TryParse(textWidth.Text, out windowWidth) ||
                start >= end ||
                windowWidth <= 0 ||
                overlap >= 100)
            {
                return isolationWindows;
            }

            // For optimized window placement, we try to align windows with the low spots
            // in the chromatogram.  This requires us to adjust both the window width
            // and the starting offset of the windows.
            if (cbOptimizeWindowPlacement.Checked)
            {
                if (overlap > 0)
                {
                    return isolationWindows;
                }
                windowWidth = Math.Ceiling(windowWidth) * OPTIMIZED_WINDOW_WIDTH_MULTIPLE;
                start = Math.Floor(start / windowWidth) * windowWidth + OPTIMIZED_WINDOW_OFFSET;
            }

            // Generate window list.
            bool generateTarget = cbGenerateMethodTarget.Checked;
            bool generateStartMargin = (comboMargins.SelectedItem.ToString() != WindowMargin.NONE);
            bool generateEndMargin = (comboMargins.SelectedItem.ToString() == WindowMargin.ASYMMETRIC);
            double windowStep = windowWidth * (100 - overlap) / 100;
            for (;
                start < end && isolationWindows.Count <= MAX_GENERATED_WINDOWS;
                start += windowStep)
            {
                // Apply instrument limits to method start and end.
                var methodStart = Math.Max(start - marginLeft, TransitionFullScan.MIN_RES_MZ);
                var methodEnd = Math.Min(start + windowWidth + marginRight, TransitionFullScan.MAX_RES_MZ);

                // Skip this isolation window if it is empty due to instrument limits.
                if (methodStart + marginLeft >= methodEnd - marginRight)
                    continue;
                
                var window = new EditIsolationWindow
                {
                    Start = methodStart + marginLeft,
                    End = methodEnd - marginRight,
                    Target = generateTarget ? (double?)((methodStart + methodEnd) / 2) : null,
                    StartMargin = generateStartMargin ? (double?)marginLeft : null,
                    EndMargin = generateEndMargin ? (double?)marginRight : null
                };
                isolationWindows.Add(window);
            }

            return isolationWindows;
        }

        private void UpdateWindowCount()
        {
            var windowCount = CreateIsolationWindows().Count;
            if (windowCount == 0)
                labelWindowCount.Text = string.Empty;
            else if (windowCount > MAX_GENERATED_WINDOWS)
                labelWindowCount.Text = string.Format(">{0}", MAX_GENERATED_WINDOWS);
            else
                labelWindowCount.Text = string.Format("{0}", windowCount);
        }

        public void OkDialog()
        {
            // TODO: Remove this
            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(this);

            double start;
            double end;
            double windowWidth;
            double overlap;
            double marginLeft;
            double marginRight;
            if (!helper.ValidateDecimalTextBox(e, textStart, TransitionFullScan.MIN_RES_MZ, TransitionFullScan.MAX_RES_MZ, out start) ||
                !helper.ValidateDecimalTextBox(e, textEnd, TransitionFullScan.MIN_RES_MZ, TransitionFullScan.MAX_RES_MZ, out end) ||
                !helper.ValidateDecimalTextBox(e, textWidth, TransitionInstrument.MIN_MZ_MATCH_TOLERANCE, 
                    TransitionFullScan.MAX_RES_MZ - TransitionFullScan.MIN_RES_MZ, out windowWidth) ||
                (textOverlap.Enabled && textOverlap.Text.Trim().Length > 0 && !helper.ValidateDecimalTextBox(e, textOverlap, 0, 99, out overlap)) ||
                (!Equals(comboMargins.SelectedItem.ToString(), WindowMargin.NONE) &&
                    !helper.ValidateDecimalTextBox(e, textMarginLeft, TransitionInstrument.MIN_MZ_MATCH_TOLERANCE, 
                        TransitionFullScan.MAX_RES_MZ - TransitionFullScan.MIN_RES_MZ, out marginLeft)) ||
                (Equals(comboMargins.SelectedItem.ToString(), WindowMargin.ASYMMETRIC) &&
                    !helper.ValidateDecimalTextBox(e, textMarginRight, TransitionInstrument.MIN_MZ_MATCH_TOLERANCE, 
                        TransitionFullScan.MAX_RES_MZ - TransitionFullScan.MIN_RES_MZ, out marginRight)))
            {
                return;
            }

            if (start >= end)
            {
                MessageDlg.Show(this, "Start value must be less than End value.");
                return;
            }

            DialogResult = DialogResult.OK;
        }

        private void comboMargins_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (comboMargins.SelectedItem.ToString())
            {
                case WindowMargin.NONE:
                    textMarginLeft.Enabled = false;
                    textMarginRight.Visible = false;
                    break;

                case WindowMargin.SYMMETRIC:
                    textMarginLeft.Enabled = true;
                    textMarginRight.Visible = false;
                    break;

                case WindowMargin.ASYMMETRIC:
                    textMarginLeft.Enabled = true;
                    textMarginRight.Visible = true;
                    break;
            }
        }

        private void cbOptimizeWindowPlacement_CheckedChanged(object sender, EventArgs e)
        {
            double overlap;
            double.TryParse(textOverlap.Text, out overlap);
            if (cbOptimizeWindowPlacement.Checked && overlap != 0)
            {
                using (var dlg = new MultiButtonMsgDlg("Window optimization cannot be applied to overlapping isolation windows. " +
                    "Click OK to remove overlap, or Cancel to cancel optimization.", "OK"))
                {
                    if (dlg.ShowDialog() == DialogResult.Cancel)
                        cbOptimizeWindowPlacement.Checked = false;
                    else
                        textOverlap.Text = string.Empty;
                }
            }

            textOverlap.Enabled = !cbOptimizeWindowPlacement.Checked;

            UpdateWindowCount();
        }

        private void textOverlap_TextChanged(object sender, EventArgs e)
        {
            UpdateWindowCount();
        }

        private void textStart_TextChanged(object sender, EventArgs e)
        {
            UpdateWindowCount();
        }

        private void textEnd_TextChanged(object sender, EventArgs e)
        {
            UpdateWindowCount();
        }

        private void textWidth_TextChanged(object sender, EventArgs e)
        {
            UpdateWindowCount();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }
    }
}
