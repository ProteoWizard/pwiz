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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    /// <summary>
    /// Additional Settings... of an Osprey run: the settings the wizard's pages do not show, for
    /// the run's scopes, in a property grid grouped by help section (or sorted by name, a choice
    /// kept in <see cref="Settings.OspreyAdditionalSettingsSorted"/>), each under its label with
    /// its usage text in the help box when it is selected; a value that is not Osprey's default
    /// is bold. The grid edits the <see cref="OspreyAdditionalSettings"/>' own Setting objects;
    /// Cancel puts back the values the dialog opened with.
    /// </summary>
    public sealed partial class OspreySettingsGridDlg : FormEx
    {
        private readonly OspreySettingsObject _settingsObject;
        private readonly IDictionary<AbstractDdaSearchEngine.Setting, object> _openingValues;

        public OspreySettingsGridDlg(OspreyAdditionalSettings settings, ICollection<OspreyPropertyScope> scopes)
        {
            InitializeComponent();

            Text = PeptideSearchResources.SearchSettingsControl_Additional_Settings;
            _settingsObject = new OspreySettingsObject(settings, scopes);
            _openingValues = _settingsObject.Descriptors.ToDictionary(d => d.Setting, d => d.Setting.Value);
            propertyGrid.PropertySort = Settings.Default.OspreyAdditionalSettingsSorted
                ? PropertySort.Alphabetical
                : PropertySort.Categorized;
            propertyGrid.SelectedObject = _settingsObject;
            propertyGrid.PropertySortChanged += propertyGrid_PropertySortChanged;
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (!e.Cancel && DialogResult != DialogResult.OK)
                RestoreOpeningValues();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void propertyGrid_PropertySortChanged(object sender, EventArgs e)
        {
            Settings.Default.OspreyAdditionalSettingsSorted = propertyGrid.PropertySort == PropertySort.Alphabetical;
        }

        /// <summary>Puts back the values the dialog opened with, for Cancel: the settings are the caller's own objects.</summary>
        private void RestoreOpeningValues()
        {
            foreach (var pair in _openingValues)
                pair.Key.Value = pair.Value;
        }

        #region Test support

        /// <summary>The labels of the settings shown, in help order.</summary>
        public IEnumerable<string> Labels => _settingsObject.Descriptors.Select(d => d.DisplayName);

        /// <summary>The section titles shown, each once, in help order, as the properties name them (the grid draws a doubled ampersand as one).</summary>
        public IEnumerable<string> Categories => _settingsObject.Descriptors.Select(d => d.Category.Replace(@"&&", @"&")).Distinct();

        public PropertySort PropertySort
        {
            get => propertyGrid.PropertySort;
            set => propertyGrid.PropertySort = value;
        }

        /// <summary>The value of the setting shown under <paramref name="label"/>: a flag's bool, other settings' text.</summary>
        public object GetValue(string label)
        {
            return FindDescriptor(label).GetValue(_settingsObject);
        }

        /// <summary>
        /// Sets the setting shown under <paramref name="label"/> from <paramref name="text"/> as the
        /// grid would, throwing what the grid shows for a value the argument does not take.
        /// </summary>
        public void SetValue(string label, string text)
        {
            FindDescriptor(label).SetValue(_settingsObject, text);
            propertyGrid.Refresh();
        }

        private OspreySettingDescriptor FindDescriptor(string label)
        {
            var descriptor = _settingsObject.FindByLabel(label);
            if (descriptor == null)
                throw new ArgumentException($@"The grid shows no setting labeled '{label}'");
            return descriptor;
        }

        #endregion
    }
}
