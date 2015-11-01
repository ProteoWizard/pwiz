/*
 * Original author: Brian Pratt <bspratt .at. proteinmet>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.FileUI
{
    public partial class ImportResultsLockMassDlg : FormEx
    {
        /// <summary>
        /// Check to see if any of these are Waters files in potential need of lockmass correction
        /// </summary>
        /// <param name="parent">Parent window to make owner of <see cref="ImportResultsLockMassDlg"/></param>
        /// <param name="doc">we only do lockmass correction if doc has full scan settings</param>
        /// <param name="namedResults">list of files to be checked</param>
        /// <returns>false iff cancelled by user</returns>
        public static bool CheckWatersLockmassCorrection(Control parent, SrmDocument doc, KeyValuePair<string, MsDataFileUri[]>[] namedResults)
        {
            if (doc.Settings.TransitionSettings.FullScan.IsEnabled &&
                namedResults.Any(n => n.Value.Any(m => m.IsWatersLockmassCorrectionCandidate())))
            {
                // We have at least one Waters file possbily needing lockspray correction
                using (var dlgLockMass = new ImportResultsLockMassDlg())
                {
                    var result = dlgLockMass.ShowDialog(parent); // Will modify Settings.Default with user's input
                    if (result != DialogResult.OK)
                    {
                        return false; // Cancelled
                    }
                }
            }
            return true; // Success
        }

        public ImportResultsLockMassDlg()
        {
            InitializeComponent();

            if (Settings.Default.LockMassPositive > 0)
                LockmassPositive = Settings.Default.LockMassPositive;
            else
                LockmassPositive = null;
            if (Settings.Default.LockMassNegative > 0)
                LockmassNegative = Settings.Default.LockMassNegative;
            else
                LockmassNegative = null;
            if (Settings.Default.LockMassTolerance > 0)
                LockmassTolerance = Settings.Default.LockMassTolerance;
            else
                LockmassTolerance = null;


        }

        public double? LockmassPositive
        {
            get
            {
                if (string.IsNullOrEmpty(textLockmassPositive.Text))
                    return null;
                return double.Parse(textLockmassPositive.Text);
            }
            set
            {
                textLockmassPositive.Text = value.HasValue ? value.Value.ToString(CultureInfo.CurrentCulture) : string.Empty;
                if (value.HasValue && string.IsNullOrEmpty(textLockmassTolerance.Text))
                    LockmassTolerance = LockMassParameters.LOCKMASS_TOLERANCE_DEFAULT;
            }
        }

        public double? LockmassNegative
        {
            get
            {
                if (string.IsNullOrEmpty(textLockmassNegative.Text))
                    return null;
                return double.Parse(textLockmassNegative.Text);
            }
            set
            {
                textLockmassNegative.Text = value.HasValue ? value.Value.ToString(CultureInfo.CurrentCulture) : string.Empty;
                if (value.HasValue && string.IsNullOrEmpty(textLockmassTolerance.Text))
                    LockmassTolerance = LockMassParameters.LOCKMASS_TOLERANCE_DEFAULT;
            }
        }

        public double? LockmassTolerance
        {
            get
            {
                if (string.IsNullOrEmpty(textLockmassTolerance.Text))
                    return null;
                return double.Parse(textLockmassTolerance.Text);
            }
            set { textLockmassTolerance.Text = value.HasValue ? value.Value.ToString(CultureInfo.CurrentCulture) : string.Empty; }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);
            double lockmassPositive;
            if (string.IsNullOrEmpty(textLockmassPositive.Text))
                lockmassPositive = 0;
            else if (!helper.ValidateDecimalTextBox(textLockmassPositive, 0, null, out lockmassPositive))
                return;
            double lockmassNegative;
            if (string.IsNullOrEmpty(textLockmassNegative.Text))
                lockmassNegative = 0;
            else if (!helper.ValidateDecimalTextBox(textLockmassNegative, 0, null, out lockmassNegative))
                return;
            double lockmassTolerance;
            if (string.IsNullOrEmpty(textLockmassTolerance.Text))
                lockmassTolerance = 0;
            else if (!helper.ValidateDecimalTextBox(textLockmassTolerance, LockMassParameters.LOCKMASS_TOLERANCE_MIN, LockMassParameters.LOCKMASS_TOLERANCE_MAX, out lockmassTolerance))
                return;

            Settings.Default.LockmassParameters = new LockMassParameters(lockmassPositive, lockmassNegative, lockmassTolerance);
            DialogResult = DialogResult.OK;
        }

    }
}
