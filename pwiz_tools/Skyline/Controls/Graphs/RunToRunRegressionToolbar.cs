/*
 * Original author: Max Horowitz-Gelb <maxhg .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class RunToRunRegressionToolbar : GraphSummaryToolbar //UserControl// for editing in the designer
    {
        public RunToRunRegressionToolbar(GraphSummary graphSummary) :
            base(graphSummary)
        {
            InitializeComponent();

            toolStrip1_Resize(null, null);
        }

        public override bool Visible
        {
            get { return true; }
        }

        public override void OnDocumentChanged(SrmDocument oldDocument, SrmDocument newDocument)
        {
            // Need at least 2 replicates to do run to run regression.
            bool visibleOld = oldDocument.MeasuredResults != null &&
                              oldDocument.MeasuredResults.Chromatograms.Count > 1;
            bool visibleNew = newDocument.MeasuredResults != null &&
                              newDocument.MeasuredResults.Chromatograms.Count > 1;
            if (visibleNew != visibleOld && visibleNew)
            {
                // Use first two replicates to avoid comparing the first replicate to itself
                _graphSummary.SetResultIndexes(0, 1, false);
            }
        }

        public override void UpdateUI()
        {
            var results = _graphSummary.DocumentUIContainer.DocumentUI.MeasuredResults;

            if (results == null)
                return;

            // Check to see if the list of files has changed.
            var listNames = new List<string>();
            foreach (var chromSet in results.Chromatograms)
                listNames.Add(chromSet.Name);

            ResetResultsCombo(listNames, toolStripComboBoxTargetReplicates);
            var origIndex = ResetResultsCombo(listNames, toolStripComboOriginalReplicates);
            var targetIndex = _graphSummary.StateProvider.SelectedResultsIndex;
            if (origIndex < 0)
                origIndex = (targetIndex + 1) % results.Chromatograms.Count;
            _graphSummary.SetResultIndexes(targetIndex, origIndex, false);
            _dontUpdateForTargetSelectedIndex = true;
            toolStripComboBoxTargetReplicates.SelectedIndex = targetIndex;
            _dontUpdateOriginalSelectedIndex = true;
            toolStripComboOriginalReplicates.SelectedIndex = origIndex;
        }

        private int ResetResultsCombo(List<string> listNames, ToolStripComboBox combo)
        {
            object selected = combo.SelectedItem;
            combo.Items.Clear();
            foreach (string name in listNames)
                combo.Items.Add(name);
            ComboHelper.AutoSizeDropDown(combo);
            return selected != null ? combo.Items.IndexOf(selected) : -1;
        }

        private bool _dontUpdateForTargetSelectedIndex;
        private void toolStripComboBoxTargetReplicates_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_dontUpdateForTargetSelectedIndex)
                _dontUpdateForTargetSelectedIndex = false;
            else if (_graphSummary.StateProvider.SelectedResultsIndex != toolStripComboBoxTargetReplicates.SelectedIndex)
                _graphSummary.StateProvider.SelectedResultsIndex = toolStripComboBoxTargetReplicates.SelectedIndex;
        }

        private bool _dontUpdateOriginalSelectedIndex;
        private void toolStripComboOriginalReplicates_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_dontUpdateOriginalSelectedIndex)
                _dontUpdateOriginalSelectedIndex = false;
            else
                _graphSummary.SetResultIndexes(_graphSummary.TargetResultsIndex, toolStripComboOriginalReplicates.SelectedIndex);
        }

        private void toolStrip1_Resize(object sender, EventArgs e)
        {
            toolStripComboOriginalReplicates.Width = toolStripComboBoxTargetReplicates.Width = (toolStrip1.Width - toolStripLabel1.Width - 24) / 2;
        }

        #region Functional Test Support

        public ToolStripComboBox RunToRunTargetReplicate { get { return toolStripComboBoxTargetReplicates; } }

        public ToolStripComboBox RunToRunOriginalReplicate { get { return toolStripComboOriginalReplicates; } }

        #endregion
    }
}
