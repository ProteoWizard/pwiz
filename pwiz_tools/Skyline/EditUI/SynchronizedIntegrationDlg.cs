/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    public partial class SynchronizedIntegrationDlg : Form
    {
        private readonly SrmDocument _document;
        private readonly AnnotationCalculator _annotationCalculator;
        private GroupByItem SelectedGroupBy => (GroupByItem) comboGroupBy.SelectedItem;

        public string GroupByPersistedString => SelectedGroupBy.PersistedString;

        public string GroupBy
        {
            get => SelectedGroupBy.ToString();
            set
            {
                var idx = GetItemIndex(value, false);
                if (idx.HasValue)
                    comboGroupBy.SelectedIndex = idx.Value;
            }
        }

        public bool IsAll => listSync.Items.Count > 0 && listSync.CheckedItems.Count == listSync.Items.Count;

        public IEnumerable<string> Targets
        {
            get => listSync.CheckedItems.Cast<object>().Select(o => o.ToString());
            set => SetCheckedItems(value.ToHashSet());
        }
        public IEnumerable<string> TargetsInvariant => listSync.CheckedItems.Cast<object>().Select(o => Convert.ToString(o, CultureInfo.InvariantCulture));

        public IEnumerable<string> GroupByOptions => comboGroupBy.Items.Cast<GroupByItem>().Select(item => item.ToString());
        public IEnumerable<string> TargetOptions => listSync.Items.Cast<object>().Select(o => o.ToString());

        public SynchronizedIntegrationDlg(SrmDocument document)
        {
            InitializeComponent();

            _document = document;
            _annotationCalculator = new AnnotationCalculator(_document);

            var groupByReplicates = new GroupByItem(null);
            comboGroupBy.Items.Add(groupByReplicates);
            comboGroupBy.Items.AddRange(ReplicateValue.GetGroupableReplicateValues(_document).Select(v => new GroupByItem(v)).ToArray());

            if (!_document.GetSynchronizeIntegrationChromatogramSets().Any())
            {
                // Synchronized integration is off, select everything
                comboGroupBy.SelectedIndex = 0;
                SetCheckedItems(TargetOptions.ToHashSet());
            }
            else
            {
                var settingsIntegration = _document.Settings.TransitionSettings.Integration;
                comboGroupBy.SelectedIndex = GetItemIndex(settingsIntegration.SynchronizedIntegrationGroupBy, true) ?? 0;
                SetCheckedItems((settingsIntegration.SynchronizedIntegrationAll ? TargetOptions : settingsIntegration.SynchronizedIntegrationTargets).ToHashSet());
            }
        }

        private int? GetItemIndex(string s, bool persistedString)
        {
            if (string.IsNullOrEmpty(s))
                return null;

            for (var i = 0; i < comboGroupBy.Items.Count; i++)
            {
                var item = (GroupByItem)comboGroupBy.Items[i];
                if (persistedString && Equals(s, item.ReplicateValue?.ToPersistedString()) ||
                    !persistedString && Equals(s, item.ToString()))
                {
                    return i;
                }
            }
            return null;
        }

        private void SetCheckedItems(ICollection<string> items)
        {
            for (var i = 0; i < listSync.Items.Count; i++)
                listSync.SetItemChecked(i, items != null && items.Contains(listSync.Items[i].ToString()));
        }

        private void comboGroupBy_SelectedIndexChanged(object sender, EventArgs e)
        {
            var newItems = SelectedGroupBy.GetItems(_document, _annotationCalculator).ToArray();
            if (!ArrayUtil.EqualsDeep(listSync.Items.Cast<object>().ToArray(), newItems))
            {
                var allChecked = IsAll;
                listSync.Items.Clear();
                listSync.Items.AddRange(newItems);
                cbSelectAll.Checked = false;
                for (var i = 0; i < listSync.Items.Count; i++)
                    listSync.SetItemChecked(i, allChecked);
            }
        }

        private void cbSelectAll_CheckedChanged(object sender, EventArgs e)
        {
            listSync.ItemCheck -= listSync_ItemCheck;
            for (var i = 0; i < listSync.Items.Count; i++)
                listSync.SetItemChecked(i, cbSelectAll.Checked);
            listSync.ItemCheck += listSync_ItemCheck;
        }

        private void listSync_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            cbSelectAll.CheckedChanged -= cbSelectAll_CheckedChanged;
            var anyChecked = listSync.CheckedItems.Count + (e.NewValue == CheckState.Checked ? 1 : -1) > 0;
            if (!cbSelectAll.Checked && anyChecked)
                cbSelectAll.Checked = true;
            else if (cbSelectAll.Checked && !anyChecked)
                cbSelectAll.Checked = false;
            cbSelectAll.CheckedChanged += cbSelectAll_CheckedChanged;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        private class GroupByItem
        {
            public ReplicateValue ReplicateValue { get; }

            public GroupByItem(ReplicateValue replicateValue)
            {
                ReplicateValue = replicateValue;
            }

            public IEnumerable<object> GetItems(SrmDocument doc, AnnotationCalculator annotationCalc)
            {
                return ReplicateValue == null
                    ? doc.Settings.MeasuredResults.Chromatograms.Select(c => c.Name)
                    : doc.Settings.MeasuredResults.Chromatograms
                        .Select(chromSet => ReplicateValue.GetValue(annotationCalc, chromSet))
                        .Distinct()
                        .OrderBy(o => o, CollectionUtil.ColumnValueComparer)
                        .Select(o => o ?? string.Empty); // replace nulls with empty strings so they can go into the listbox
            }

            public string PersistedString => ReplicateValue?.ToPersistedString();

            public override string ToString()
            {
                return ReplicateValue != null ? ReplicateValue.Title : Resources.GroupByItem_ToString_Replicates;
            }
        }
    }
}
