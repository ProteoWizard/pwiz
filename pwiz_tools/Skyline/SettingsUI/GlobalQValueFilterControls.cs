/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.SettingsUI
{
    /// <summary>
    /// A level dropdown and a cutoff textbox for filtering library entries on a global q-value.
    /// An empty cutoff means no filter.
    /// </summary>
    public class GlobalQValueFilterControls
    {
        /// <summary>
        /// The levels at which any of the libraries report global q-values, in level order.
        /// </summary>
        public static IList<GlobalQValueLevel> GetLevels(IEnumerable<Library> libraries)
        {
            var levels = libraries.Where(library => library != null)
                .SelectMany(library => library.GlobalQValueLevels).ToHashSet();
            return levels.OrderBy(level => level).ToList();
        }

        private readonly ComboBox _comboLevel;
        private readonly TextBox _textCutoff;

        public GlobalQValueFilterControls(ComboBox comboLevel, TextBox textCutoff)
        {
            _comboLevel = comboLevel;
            _textCutoff = textCutoff;
        }

        /// <summary>
        /// The levels offered, empty when there are none to filter on.
        /// </summary>
        public IList<GlobalQValueLevel> Levels
        {
            get { return _comboLevel.Items.Cast<LevelItem>().Select(item => item.Level).ToList(); }
            set
            {
                var selectedLevel = Level;
                _comboLevel.Items.Clear();
                foreach (var level in value)
                    _comboLevel.Items.Add(new LevelItem(level));
                if (value.Count > 0)
                    Level = selectedLevel.HasValue && value.Contains(selectedLevel.Value) ? selectedLevel.Value : value[0];
            }
        }

        public GlobalQValueLevel? Level
        {
            get { return (_comboLevel.SelectedItem as LevelItem)?.Level; }
            set { _comboLevel.SelectedItem = _comboLevel.Items.Cast<LevelItem>().FirstOrDefault(item => item.Level == value); }
        }

        public string CutoffText
        {
            get { return _textCutoff.Text; }
            set { _textCutoff.Text = value; }
        }

        /// <summary>
        /// Reads the filter, null when there is no level or the cutoff is empty. Returns false, after
        /// telling the user, if the cutoff is not a number between 0 and 1.
        /// </summary>
        public bool TryGetFilter(IWin32Window owner, out GlobalQValueFilter filter)
        {
            if (!TryParseFilter(out filter))
            {
                MessageDlg.Show(owner, SettingsUIResources.GlobalQValueFilterControls_TryGetFilter_The_q_value_cutoff_must_be_a_number_between_0_and_1__or_empty_to_add_every_entry);
                _textCutoff.Focus();
                _textCutoff.SelectAll();
                return false;
            }
            return true;
        }

        /// <summary>
        /// The filter for the audit log, null when there is none or the cutoff is not valid.
        /// </summary>
        public GlobalQValueFilter Filter
        {
            get { return TryParseFilter(out var filter) ? filter : null; }
        }

        private bool TryParseFilter(out GlobalQValueFilter filter)
        {
            filter = null;
            var level = Level;
            if (!level.HasValue || string.IsNullOrWhiteSpace(_textCutoff.Text))
                return true;
            if (!double.TryParse(_textCutoff.Text, NumberStyles.Float, LocalizationHelper.CurrentCulture, out var cutoff) ||
                cutoff < 0 || cutoff > 1)
            {
                return false;
            }
            filter = new GlobalQValueFilter(level.Value, cutoff);
            return true;
        }

        private class LevelItem
        {
            public LevelItem(GlobalQValueLevel level)
            {
                Level = level;
            }

            public GlobalQValueLevel Level { get; }

            public override string ToString()
            {
                switch (Level)
                {
                    case GlobalQValueLevel.protein:
                        return SettingsUIResources.GlobalQValueFilterControls_LevelItem_Protein_q_value;
                    case GlobalQValueLevel.peptide:
                        return SettingsUIResources.GlobalQValueFilterControls_LevelItem_Peptide_q_value;
                    default:
                        return SettingsUIResources.GlobalQValueFilterControls_LevelItem_Precursor_q_value;
                }
            }
        }
    }
}
