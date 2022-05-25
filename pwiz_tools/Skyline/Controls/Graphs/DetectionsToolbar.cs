/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using Settings = pwiz.Skyline.Controls.Graphs.DetectionsGraphController.Settings;
using IntLabeledValue = pwiz.Skyline.Controls.Graphs.DetectionsGraphController.IntLabeledValue;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class DetectionsToolbar : GraphSummaryToolbar
    {
        private Timer _timer;

        private Dictionary<ToolStripDropDown, ToolStripItem> _selectedItems =
            new Dictionary<ToolStripDropDown, ToolStripItem>(4);

        public override bool Visible => true;
        public ToolStripComboBox CbLevel => cbLevel;

        public DetectionsToolbar(GraphSummary graphSummary) : base(graphSummary)
        {
            InitializeComponent();
            _timer = new Timer { Interval = 100 };
            _timer.Tick += Timer_OnTick;
        }

        private void Timer_OnTick(object sender, EventArgs e)
        {
            _graphSummary.UpdateUIWithoutToolbar();
            _timer.Stop();
        }

        public override void OnDocumentChanged(SrmDocument oldDocument, SrmDocument newDocument)
        {
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _timer.Stop();
            _timer.Tick -= Timer_OnTick;

            base.OnHandleDestroyed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
                _timer.Dispose();
            }
            base.Dispose(disposing);
        }

        public override void UpdateUI()
        {
            IntLabeledValue.PopulateCombo(cbLevel, Settings.TargetType);
            if (!_graphSummary.TryGetGraphPane(out DetectionsPlotPane pane)) return;
            if (pane.CurrentData.IsValid &&
                DetectionPlotData.GetDataCache().Status != DetectionPlotData.DetectionDataCache.CacheStatus.error)
            {
                EnableControls(true);
                IntLabeledValue.PopulateCombo(cbLevel, Settings.TargetType);
                toolStripAtLeastN.NumericUpDownControl.Minimum = 0;
                toolStripAtLeastN.NumericUpDownControl.Maximum =
                    _graphSummary.DocumentUIContainer.DocumentUI.MeasuredResults.Chromatograms.Count;
                toolStripAtLeastN.NumericUpDownControl.Value = Settings.RepCount;
                toolStripAtLeastN.NumericUpDownControl.ValueChanged += toolStripAtLeastN_ValueChanged;
            }
            else
            {
                toolStripAtLeastN.NumericUpDownControl.Value = 0;
                EnableControls(false);
            }
        }

        public void EnableControls(bool enable)
        {
            toolStripAtLeastN.Enabled = enable;
            cbLevel.Enabled = enable;
            pbProperties.Enabled = enable;
        }
        //marked public for testing purposes
        public void pbProperties_Click(object sender, EventArgs e)
        {
            using (var dlgProperties = new DetectionToolbarProperties(_graphSummary))
            {
                _timer.Stop();
                if (dlgProperties.ShowDialog(FormEx.GetParentForm(this)) == DialogResult.OK)
                {
                    UpdateUI();
                    _timer.Start();
                }
            }
        }

        public void cbLevel_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbLevel.Items.Count == 2)
            {
                Settings.TargetType = IntLabeledValue.GetValue(cbLevel, Settings.TargetType);
                _timer.Stop();
                _timer.Start();
            }
        }

        public void toolStripAtLeastN_ValueChanged(object sender, EventArgs e)
        {
            Settings.RepCount = (int)toolStripAtLeastN.NumericUpDownControl.Value;
            _timer.Stop();
            _timer.Start();
        }
    }
}
