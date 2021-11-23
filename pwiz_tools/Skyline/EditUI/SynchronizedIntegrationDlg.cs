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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    public partial class SynchronizedIntegrationDlg : Form
    {
        private readonly SkylineWindow _skylineWindow;
        private readonly bool _originalAlignRtPrediction;
        private readonly ChromFileInfoId _originalAlignFile;

        private SrmDocument Document => _skylineWindow.Document;
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

        public SynchronizedIntegrationDlg(SkylineWindow skylineWindow)
        {
            InitializeComponent();

            _skylineWindow = skylineWindow;
            _originalAlignRtPrediction = skylineWindow.AlignToRtPrediction;
            _originalAlignFile = skylineWindow.AlignToFile;

            var groupByReplicates = new GroupByItem(null);
            comboGroupBy.Items.Add(groupByReplicates);
            comboGroupBy.Items.AddRange(ReplicateValue.GetGroupableReplicateValues(Document).Select(v => new GroupByItem(v)).ToArray());

            if (!Document.GetSynchronizeIntegrationChromatogramSets().Any())
            {
                // Synchronized integration is off, select everything
                comboGroupBy.SelectedIndex = 0;
                SetCheckedItems(TargetOptions.ToHashSet());
            }
            else
            {
                var settingsIntegration = Document.Settings.TransitionSettings.Integration;
                comboGroupBy.SelectedIndex = GetItemIndex(settingsIntegration.SynchronizedIntegrationGroupBy, true) ?? 0;
                SetCheckedItems((settingsIntegration.SynchronizedIntegrationAll ? TargetOptions : settingsIntegration.SynchronizedIntegrationTargets).ToHashSet());
            }

            comboAlign.Items.Add(new AlignItem());
            comboAlign.Items.Add(new AlignItem(Document.Settings.PeptideSettings.Prediction));
            comboAlign.Items.AddRange(AlignItem.GetAlignChromFileInfos(Document.Settings).Select(info => new AlignItem(info)).ToArray());
            SelectAlignOption();
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

        private void SelectAlignOption()
        {
            foreach (AlignItem item in comboAlign.Items)
            {
                if (item.IsMatch(_skylineWindow))
                {
                    comboAlign.SelectedItem = item;
                    return;
                }
            }
        }

        private void comboGroupBy_SelectedIndexChanged(object sender, EventArgs e)
        {
            var newItems = SelectedGroupBy.GetItems(Document, new AnnotationCalculator(Document)).ToArray();
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

        private void comboAlign_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (((AlignItem)comboAlign.SelectedItem).Select(_skylineWindow))
                return;

            // RT prediction selected, but document does not have predictor
            if (!_originalAlignRtPrediction)
            {
                SelectAlignOption();
                MessageDlg.Show(this,
                    Resources.SynchronizedIntegrationDlg_comboAlign_SelectedIndexChanged_To_align_to_retention_time_prediction__you_must_first_set_up_a_retention_time_predictor_in_Peptide_Settings___Prediction_);
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            _skylineWindow.AlignToRtPrediction = _originalAlignRtPrediction;
            _skylineWindow.AlignToFile = _originalAlignFile;
        }

        #region Functional test support
        public AlignItem SelectedAlignItem => (AlignItem)comboAlign.SelectedItem;

        public bool SelectNone()
        {
            foreach (AlignItem item in comboAlign.Items)
            {
                if (item.IsNone)
                {
                    comboAlign.SelectedItem = item;
                    return true;
                }
            }
            return false;
        }

        public bool SelectAlignRt()
        {
            foreach (AlignItem item in comboAlign.Items)
            {
                if (item.IsRTRegression)
                {
                    if (item.CalcName == null)
                        return false;
                    comboAlign.SelectedItem = item;
                    return true;
                }
            }
            return false;
        }

        public bool SelectAlignFile(ChromFileInfoId file)
        {
            foreach (AlignItem item in comboAlign.Items)
            {
                if (item.IsFile && ReferenceEquals(item.ChromFileInfoId, file))
                {
                    comboAlign.SelectedItem = item;
                    return true;
                }
            }
            return false;
        }
        #endregion

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

        public class AlignItem
        {
            private readonly PeptidePrediction _prediction;
            private readonly ChromFileInfo _chromFileInfo;

            public bool IsNone => !IsRTRegression && !IsFile;
            public bool IsRTRegression => _prediction != null;
            public bool IsFile => _chromFileInfo != null;

            public string CalcName =>
                IsRTRegression && _prediction.RetentionTime != null && _prediction.RetentionTime.IsAutoCalculated
                    ? _prediction.RetentionTime.Calculator?.Name
                    : null;
            public ChromFileInfoId ChromFileInfoId => _chromFileInfo.FileId;

            public AlignItem()
            {
            }

            public AlignItem(PeptidePrediction prediction)
            {
                _prediction = prediction;
            }

            public AlignItem(ChromFileInfo chromFileInfo)
            {
                _chromFileInfo = chromFileInfo;
            }

            public bool Select(SkylineWindow skylineWindow)
            {
                skylineWindow.AlignToRtPrediction = IsRTRegression;
                skylineWindow.AlignToFile = IsFile ? _chromFileInfo.FileId : null;
                return !(IsRTRegression && CalcName == null);
            }

            public bool IsMatch(SkylineWindow skylineWindow)
            {
                if (skylineWindow.AlignToRtPrediction)
                    return IsRTRegression;
                else if (skylineWindow.AlignToFile != null)
                    return IsFile && ReferenceEquals(skylineWindow.AlignToFile, _chromFileInfo.FileId);
                return IsNone;
            }

            public override string ToString()
            {
                if (IsRTRegression)
                {
                    return CalcName != null
                        ? string.Format(Resources.AlignItem_ToString_Retention_time_calculator___0__, CalcName)
                        : Resources.AlignItem_ToString_Retention_time_calculator___;
                }
                else if (IsFile)
                {
                    return FileDisplayName(_chromFileInfo);
                }
                return Resources.AlignItem_ToString_None;
            }

            public static IEnumerable<ChromFileInfo> GetAlignChromFileInfos(SrmSettings settings)
            {
                if (!settings.HasResults || settings.DocumentRetentionTimes.FileAlignments.IsEmpty)
                    yield break;

                var chromFileInfos = settings.MeasuredResults.Chromatograms.SelectMany(chromSet => chromSet.MSDataFileInfos).ToArray();
                foreach (var name in settings.DocumentRetentionTimes.FileAlignments.Select(alignment => alignment.Key))
                {
                    var chromFileInfo = chromFileInfos.FirstOrDefault(info => name.Equals(FileDisplayName(info)));
                    if (chromFileInfo != null)
                        yield return chromFileInfo;
                }
            }

            private static string FileDisplayName(IPathContainer chromFileInfo)
            {
                return chromFileInfo.FilePath.GetFileNameWithoutExtension();
            }
        }
    }
}
