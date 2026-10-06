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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    /// <summary>
    /// Asks which replicate value to group the chromatogram graphs by, with replicates themselves
    /// as the first choice.
    /// </summary>
    public partial class ArrangeGraphsTabbedByGroupDlg : FormEx
    {
        public ArrangeGraphsTabbedByGroupDlg(SrmDocument document)
        {
            InitializeComponent();

            comboGroupBy.Items.Add(new GroupByItem(null));
            comboGroupBy.Items.AddRange(ReplicateValue.GetGroupableReplicateValues(document)
                .Select(replicateValue => new GroupByItem(replicateValue)).ToArray());
            comboGroupBy.SelectedIndex = 0;
            if (document.HasSynchronizedIntegration)
            {
                ReplicateValue = ReplicateValue.FromPersistedString(document.Settings,
                    document.Settings.TransitionSettings.Integration.SynchronizedIntegrationGroupBy);
            }
        }

        /// <summary>
        /// The replicate value to group by, or null to group by replicate.
        /// </summary>
        public ReplicateValue ReplicateValue
        {
            get { return ((GroupByItem) comboGroupBy.SelectedItem).ReplicateValue; }
            set
            {
                var item = comboGroupBy.Items.Cast<GroupByItem>()
                    .FirstOrDefault(groupByItem => Equals(groupByItem.ReplicateValue, value));
                if (item != null)
                    comboGroupBy.SelectedItem = item;
            }
        }

        public IEnumerable<string> GroupByOptions
        {
            get { return comboGroupBy.Items.Cast<GroupByItem>().Select(item => item.ToString()); }
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
            public GroupByItem(ReplicateValue replicateValue)
            {
                ReplicateValue = replicateValue;
            }

            public ReplicateValue ReplicateValue { get; }

            public override string ToString()
            {
                return ReplicateValue != null ? ReplicateValue.Title : Resources.GroupByItem_ToString_Replicates;
            }
        }
    }
}
