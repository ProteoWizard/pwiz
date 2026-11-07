/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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

using System.Windows.Forms;
using pwiz.CarafeSharp.Proteome;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.SettingsUI
{
    /// <summary>
    /// The pair of entrapment percentages a library build takes, shared by Build Library's Predict
    /// For page and the Osprey Search wizard's FASTA page: the targets that get an entrapment
    /// peptide on an entrapment protein of its own (<c>--entrapment</c>), for the true FDR of a
    /// search, and the targets that get one placed on a real target protein
    /// (<c>--entrapment-in-targets</c>), so a protein-aware scoring step is audited too. Together
    /// they are the total entrapment added. Both persist as the user's last values
    /// (<see cref="Settings.LibraryEntrapmentPercent"/>, <see cref="Settings.LibraryEntrapmentInTargetsPercent"/>).
    /// </summary>
    public static class EntrapmentPercentControls
    {
        /// <summary>The smallest total CarafeSharp builds (<see cref="EntrapmentFastaSettings.MIN_ENTRAPMENT_RATIO"/>), as a percentage.</summary>
        public const double MIN_TOTAL_PERCENT = EntrapmentFastaSettings.MIN_ENTRAPMENT_RATIO * 100;
        public const double MAX_PERCENT = 100;

        /// <summary>
        /// Places each box after its label, whatever the label's width in the current language,
        /// and the second pair after the first, so a longer caption never runs under its box.
        /// </summary>
        public static void Layout(Label labelPercent, TextBox textPercent, Label labelInTargets, TextBox textInTargets)
        {
            textPercent.Left = labelPercent.Right + 4;
            labelInTargets.Left = textPercent.Right + 12;
            textInTargets.Left = labelInTargets.Right + 4;
        }

        /// <summary>The captions name the choice; the tooltips say what each percentage does.</summary>
        public static void SetToolTips(ToolTip helpTip, TextBox textPercent, TextBox textInTargets)
        {
            helpTip.SetToolTip(textPercent, SettingsUIResources.EntrapmentPercentControls_SetToolTips_Shuffled_peptides_on_entrapment_proteins_of_their_own__as_a_percentage_of_the_targets__to_measure_the_true_false_discovery_rate_of_a_search_);
            helpTip.SetToolTip(textInTargets, SettingsUIResources.EntrapmentPercentControls_SetToolTips_Shuffled_peptides_placed_on_real_target_proteins__as_a_percentage_of_the_targets__so_protein_aware_scoring_is_audited_too__With_the_entrapment_percentage__the_total_entrapment_added_);
        }

        /// <summary>The boxes start at the user's last values, 10% and 10% to begin with.</summary>
        public static void LoadSettings(TextBox textPercent, TextBox textInTargets)
        {
            SetPercent(textPercent, Settings.Default.LibraryEntrapmentPercent);
            SetPercent(textInTargets, Settings.Default.LibraryEntrapmentInTargetsPercent);
        }

        /// <summary>Keeps validated values as the user's last, for the next build.</summary>
        public static void SaveSettings(double percent, double inTargetsPercent)
        {
            Settings.Default.LibraryEntrapmentPercent = percent;
            Settings.Default.LibraryEntrapmentInTargetsPercent = inTargetsPercent;
        }

        public static double GetPercent(TextBox textBox)
        {
            return double.Parse(textBox.Text, LocalizationHelper.CurrentCulture);
        }

        public static void SetPercent(TextBox textBox, double percent)
        {
            textBox.Text = percent.ToString(LocalizationHelper.CurrentCulture);
        }

        /// <summary>A percentage as the command line and CarafeSharp take it: a ratio of the targets.</summary>
        public static double ToRatio(double percent)
        {
            return percent / 100;
        }

        /// <summary>
        /// Each box must hold a percentage from 0 to 100, and together they must add up to at
        /// least <see cref="MIN_TOTAL_PERCENT"/> (CarafeSharp refuses less) and at most 100; the
        /// refusal names the box and leaves the focus in it.
        /// </summary>
        public static bool Validate(Form parent, TextBox textPercent, TextBox textInTargets, out double percent, out double inTargetsPercent)
        {
            var helper = new MessageBoxHelper(parent);
            inTargetsPercent = 0;
            if (!helper.ValidateDecimalTextBox(textPercent, 0, MAX_PERCENT, out percent))
                return false;
            if (!helper.ValidateDecimalTextBox(textInTargets, 0, MAX_PERCENT, out inTargetsPercent))
                return false;
            double total = percent + inTargetsPercent;
            if (total < MIN_TOTAL_PERCENT || total > MAX_PERCENT)
            {
                helper.ShowTextBoxError(textPercent, string.Format(
                    SettingsUIResources.EntrapmentPercentControls_Validate_The_entrapment_percentages_together_must_be_at_least__0___and_at_most__1___,
                    MIN_TOTAL_PERCENT, MAX_PERCENT));
                return false;
            }
            return true;
        }
    }
}
