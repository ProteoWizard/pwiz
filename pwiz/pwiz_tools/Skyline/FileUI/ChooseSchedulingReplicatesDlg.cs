/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI
{
    /// <summary>
    /// Allows the user to specify which replicates should be used for determining the retention time
    /// window for extracted chromatograms.  Sets the <see cref="ChromatogramSet.UseForRetentionTimeFilter"/>a
    /// property.
    /// </summary>
    public partial class ChooseSchedulingReplicatesDlg : FormEx
    {
        public ChooseSchedulingReplicatesDlg(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            Icon = Resources.Skyline;
            SkylineWindow = skylineWindow;
            double windowLength = SkylineWindow.Document.Settings.TransitionSettings.FullScan.RetentionTimeFilterLength;
            labelInstructions.Text = string.Format(labelInstructions.Text, windowLength);
        }

        public SkylineWindow SkylineWindow { get; private set; }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (null != SkylineWindow)
            {
                SkylineWindow.DocumentUIChangedEvent += SkylineWindowOnDocumentUIChangedEvent;
                UpdateUi();
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (null != SkylineWindow)
            {
                SkylineWindow.DocumentUIChangedEvent -= SkylineWindowOnDocumentUIChangedEvent;
            }
            base.OnHandleDestroyed(e);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            var checkedReplicates = checkedListBoxResults.CheckedItems
                .OfType<ListBoxItem>().Select(item => item.ChromatogramSet).ToList();
            if (checkedReplicates.Count == 0)
            {
                var prediction = SkylineWindow.DocumentUI.Settings.PeptideSettings.Prediction;
                if (prediction.RetentionTime == null || !prediction.RetentionTime.IsUsable)
                {
                    MessageDlg.Show(this, Resources.ChooseSchedulingReplicatesDlg_btnOk_Click_You_must_choose_at_least_one_replicate);
                    return;
                }
            }
            SkylineWindow.ModifyDocument(Resources.ChooseSchedulingReplicatesDlg_btnOk_Click_Choose_retention_time_filter_replicates, document =>
            {
                var loadedReplicates = checkedListBoxResults.Items
                    .Cast<ListBoxItem>().Select(item => item.ChromatogramSet);
                foreach (var replicate in loadedReplicates)
                {
                    if (document.Settings.MeasuredResults == null ||
                        !document.Settings.MeasuredResults.Chromatograms.Any(
                            chromSetCompare => ReferenceEquals(chromSetCompare, replicate)))
                    {
                        throw new InvalidDataException(Resources.ChooseSchedulingReplicatesDlg_btnOk_Click_The_set_of_replicates_in_this_document_has_changed___Please_choose_again_which_replicates_to_use_for_the_retention_time_filter_);
                    }
                }
                var measuredResults = document.Settings.MeasuredResults;
                if (null != measuredResults)
                {
                    var chromatogramSets = measuredResults.Chromatograms.ToArray();
                    for (int replicateIndex = 0; replicateIndex < chromatogramSets.Length; replicateIndex++)
                    {
                        var chromatogramSet = chromatogramSets[replicateIndex];
                        bool useForFilter = checkedReplicates.Any(chromSetCompare
                            => ReferenceEquals(chromSetCompare, chromatogramSet));
                        if (useForFilter != chromatogramSet.UseForRetentionTimeFilter)
                        {
                            chromatogramSets[replicateIndex] =
                                chromatogramSet.ChangeUseForRetentionTimeFilter(useForFilter);
                        }
                    }
                    document = document.ChangeMeasuredResults(measuredResults.ChangeChromatograms(chromatogramSets));
                }
                return document;
            });
            DialogResult = DialogResult.OK;
        }
        private void SkylineWindowOnDocumentUIChangedEvent(object sender, DocumentChangedEventArgs documentChangedEventArgs)
        {
            BeginInvoke(new Action(UpdateUi));
        }

        public void UpdateUi()
        {
            checkedListBoxResults.Items.Clear();
            var document = SkylineWindow.DocumentUI;
            var measuredResults = document.Settings.MeasuredResults;
            if (null != measuredResults)
            {
                foreach (var chromatogramSet in measuredResults.Chromatograms)
                {
                    checkedListBoxResults.Items.Add(new ListBoxItem(chromatogramSet));
                    checkedListBoxResults.SetItemChecked(checkedListBoxResults.Items.Count - 1, chromatogramSet.UseForRetentionTimeFilter);
                }
            }
        }

        class ListBoxItem
        {
            public ListBoxItem(ChromatogramSet chromatogramSet)
            {
                ChromatogramSet = chromatogramSet;
            }

            public ChromatogramSet ChromatogramSet
            {
                get; private set;
            }

            public override string ToString()
            {
                return ChromatogramSet.Name;
            }
        }

        private void checkboxSelectAll_CheckedChanged(object sender, EventArgs e)
        {
            if (checkboxSelectAll.CheckState == CheckState.Indeterminate)
            {
                return;
            }
            SelectOrDeselectAll(checkboxSelectAll.CheckState == CheckState.Checked);
        }

        public void SelectOrDeselectAll(bool select)
        {
            for (int i = 0; i < checkedListBoxResults.Items.Count; i++)
            {
                checkedListBoxResults.SetItemChecked(i, select);
            }
        }

        private void UpdateSelectAll()
        {
            int checkCount = checkedListBoxResults.CheckedIndices.Count;
            if (checkCount == checkedListBoxResults.Items.Count)
            {
                checkboxSelectAll.CheckState = checkCount == 0 ? CheckState.Indeterminate : CheckState.Checked;
            }
            else
            {
                checkboxSelectAll.CheckState = checkCount == 0 ? CheckState.Unchecked : CheckState.Indeterminate;
            }
        }

        private void checkedListBoxResults_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            BeginInvoke(new Action(UpdateSelectAll));
        }

        #region Testing methods

        public bool TrySetReplicateChecked(ChromatogramSet chromatogramSet, bool isChecked)
        {
            for (int index = 0; index < checkedListBoxResults.Items.Count; index ++)
            {
                ListBoxItem item = (ListBoxItem) checkedListBoxResults.Items[index];
                if (ReferenceEquals(chromatogramSet, item.ChromatogramSet))
                {
                    checkedListBoxResults.SetItemChecked(index, isChecked);
                    return true;
                }
            }
            return false;
        }

        public void OkDialog()
        {
            btnOk_Click(btnOk, new EventArgs());
        }
        #endregion
    }
}
