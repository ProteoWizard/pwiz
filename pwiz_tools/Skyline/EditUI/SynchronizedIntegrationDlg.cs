﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
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
            get => listSync.CheckedItems.Cast<string>();
            set => SetCheckedItems(value.ToHashSet());
        }

        public IEnumerable<string> GroupByOptions => comboGroupBy.Items.Cast<GroupByItem>().Select(item => item.ToString());
        public IEnumerable<string> TargetOptions => listSync.Items.Cast<string>();

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

        private void SetCheckedItems(HashSet<string> items)
        {
            for (var i = 0; i < listSync.Items.Count; i++)
                listSync.SetItemChecked(i, items != null && items.Contains(listSync.Items[i]));
        }

        private void comboGroupBy_SelectedIndexChanged(object sender, EventArgs e)
        {
            var newItems = SelectedGroupBy.GetItems(_document, _annotationCalculator);
            if (!ArrayUtil.EqualsDeep(listSync.Items.Cast<string>().ToArray(), newItems))
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

            public string[] GetItems(SrmDocument doc, AnnotationCalculator annotationCalc)
            {
                if (ReplicateValue == null)
                    return doc.Settings.MeasuredResults.Chromatograms.Select(c => c.Name).ToArray();

                var values = new SortedSet<string>();
                foreach (var chromSet in doc.Settings.MeasuredResults.Chromatograms)
                    values.Add(ReplicateValue.GetValue(annotationCalc, chromSet).ToString());
                return values.ToArray();
            }

            public string PersistedString => ReplicateValue != null ? ReplicateValue.ToPersistedString() : null;

            public override string ToString()
            {
                return ReplicateValue != null ? ReplicateValue.Title : Resources.GroupByItem_ToString_Replicates;
            }
        }
    }
}
