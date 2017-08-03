/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls.Graphs
{
    public sealed partial class AreaCVToolbar : GraphSummaryToolbar //UserControl // for designer
    {
        public AreaCVToolbar(GraphSummary graphSummary) :
            base(graphSummary)
        {
            InitializeComponent();

            toolStripNumericDetections.NumericUpDownControl.ValueChanged += NumericUpDownControl_ValueChanged;
            toolStripComboGroup.SelectedIndexChanged += toolStripComboGroup_SelectedIndexChanged;

            _timer = new Timer
            {
                Interval = 100
            };
            _timer.Tick += timer_Tick;
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _timer.Stop();
            _timer.Tick -= timer_Tick;

            base.OnHandleDestroyed(e);
        }

        void timer_Tick(object sender, EventArgs e)
        {
            _timer.Stop();
            _graphSummary.UpdateUIWithoutToolbar();
        }

        public IEnumerable<string> Annotations
        {
            get { return toolStripComboGroup.Items.Cast<string>(); }
            set
            {
                toolStripComboGroup.Items.Clear();
                // ReSharper disable once CoVariantArrayConversion
                toolStripComboGroup.Items.AddRange(value.ToArray());
            }
        }

        private void toolStripComboGroup_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetGroupIndex(toolStripComboGroup.SelectedIndex);
        }

        public void SetGroupIndex(int index)
        {
            if (index == 0)
            {
                AreaGraphController.GroupByAnnotation = null;
                toolStripNumericDetections.NumericUpDownControl.Maximum = _graphSummary.DocumentUIContainer.DocumentUI
                    .MeasuredResults.Chromatograms.Count;
            }
            else
            {
                AreaGraphController.GroupByAnnotation = toolStripComboGroup.Items[index].ToString();
                toolStripNumericDetections.NumericUpDownControl.Maximum = AnnotationHelper
                    .GetReplicateIndices(_graphSummary.DocumentUIContainer.DocumentUI.Settings,
                        AreaGraphController.GroupByGroup, AreaGraphController.GroupByAnnotation).Length;
            }

            if (IsCurrentDataCached())
            {
                _graphSummary.UpdateUIWithoutToolbar();
                return;
            }

            _timer.Stop();
            _timer.Start();
        }

        private void toolStripComboNormalizedTo_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetNormalizationIndex(toolStripComboNormalizedTo.SelectedIndex);
        }

        public void SetNormalizationIndex(int index)
        {
            if (index < _standardTypeCount)
            {
                AreaGraphController.NormalizationMethod = AreaCVNormalizationMethod.ratio;
                AreaGraphController.AreaCVRatioIndex = index;
            }
            else
            {
                index -= _standardTypeCount;
                if (!_graphSummary.DocumentUIContainer.DocumentUI.Settings.HasGlobalStandardArea)
                    ++index;

                switch (index)
                {
                    case 0:
                        AreaGraphController.NormalizationMethod =
                            _graphSummary.DocumentUIContainer.Document.Settings.HasGlobalStandardArea
                                ? AreaCVNormalizationMethod.global_standards
                                : AreaCVNormalizationMethod.medians;
                        break;
                    case 1:
                        AreaGraphController.NormalizationMethod = AreaCVNormalizationMethod.medians;
                        break;
                    case 2:
                        AreaGraphController.NormalizationMethod = AreaCVNormalizationMethod.none;
                        break;
                }

                AreaGraphController.AreaCVRatioIndex = -1;
            }

            if (IsCurrentDataCached())
            {
                _graphSummary.UpdateUIWithoutToolbar();
                return;
            }

            _timer.Stop();
            _timer.Start();
        }

        private void NumericUpDownControl_ValueChanged(object sender, EventArgs e)
        {
            AreaGraphController.MinimumDetections = (int)toolStripNumericDetections.NumericUpDownControl.Value;

            if (IsCurrentDataCached())
            {
                _graphSummary.UpdateUIWithoutToolbar();
                return;
            }

            _timer.Stop();
            _timer.Start();
        }

        private bool IsCurrentDataCached()
        {
            AreaCVGraphData.AreaCVGraphDataCache cache;
            var pane = _graphSummary.GraphPanes.FirstOrDefault();
            if (pane == null)
                return false;
            else if (pane is AreaCVHistogramGraphPane)
            {
                cache = ((AreaCVHistogramGraphPane) pane).Cache;
            }
            else if (pane is AreaCVHistogram2DGraphPane)
            {
                cache = ((AreaCVHistogram2DGraphPane) pane).Cache;
            }
            else
                return false;

            return cache.IsValidFor(new AreaCVGraphData.AreaCVGraphSettings()) &&
                cache.Get(AreaGraphController.GroupByGroup,
                    AreaGraphController.GroupByAnnotation,
                    AreaGraphController.MinimumDetections,
                    AreaGraphController.NormalizationMethod,
                    AreaGraphController.AreaCVRatioIndex) != null;
        }

        private void toolStripProperties_Click(object sender, EventArgs e)
        {
            using (var dlgProperties = new AreaCVToolbarProperties(_graphSummary.DocumentUIContainer.DocumentUI))
            {
                if (dlgProperties.ShowDialog() == DialogResult.OK)
                    _graphSummary.UpdateUI();
            }
        }

        public override bool Visible
        {
            get { return true; }
        }

        public override void OnDocumentChanged(SrmDocument oldDocument, SrmDocument newDocument)
        {
        }

        public override void UpdateUI()
        {
            var document = _graphSummary.DocumentUIContainer.DocumentUI;

            var groupsVisible = AreaGraphController.GroupByGroup != null;
            toolStripLabel1.Visible = toolStripComboGroup.Visible = groupsVisible;

            var detectionsVisiblePrev = toolStripLabel2.Visible && toolStripNumericDetections.Visible && toolStripLabel3.Visible;
            var detectionsVisible = AreaGraphController.ShouldUseQValues(document);
            toolStripLabel2.Visible = toolStripNumericDetections.Visible = toolStripLabel3.Visible = detectionsVisible;

            if (detectionsVisible)
            {
                toolStripNumericDetections.NumericUpDownControl.Minimum = 2;

                if (AreaGraphController.GroupByGroup == null || AreaGraphController.GroupByAnnotation == null)
                    toolStripNumericDetections.NumericUpDownControl.Maximum = document.MeasuredResults.Chromatograms.Count;
                else
                    toolStripNumericDetections.NumericUpDownControl.Maximum = AnnotationHelper.GetReplicateIndices(document.Settings, AreaGraphController.GroupByGroup, AreaGraphController.GroupByAnnotation).Length;

                if (!detectionsVisiblePrev)
                    toolStripNumericDetections.NumericUpDownControl.Value = 2;
            }

            if (groupsVisible)
            {
                var annotations = new[] { Resources.GraphSummary_UpdateToolbar_All }.Concat(AnnotationHelper.GetPossibleAnnotations(document.Settings, AreaGraphController.GroupByGroup, AnnotationDef.AnnotationTarget.replicate)).ToArray();

                toolStripComboGroup.Items.Clear();
                // ReSharper disable once CoVariantArrayConversion
                toolStripComboGroup.Items.AddRange(annotations);

                if (AreaGraphController.GroupByAnnotation != null)
                    toolStripComboGroup.SelectedItem = AreaGraphController.GroupByAnnotation;
                else
                    toolStripComboGroup.SelectedIndex = 0;
            }

            var mods = _graphSummary.DocumentUIContainer.DocumentUI.Settings.PeptideSettings.Modifications;
            var standardTypes = mods.RatioInternalStandardTypes;

            toolStripComboNormalizedTo.Items.Clear();

            if (mods.HasHeavyModifications)
            {
                // ReSharper disable once CoVariantArrayConversion
                toolStripComboNormalizedTo.Items.AddRange(standardTypes.Select(s => s.Title).ToArray());
                _standardTypeCount = standardTypes.Count;
            }

            var hasGlobalStandard = _graphSummary.DocumentUIContainer.DocumentUI.Settings.HasGlobalStandardArea;
            if (hasGlobalStandard)
                toolStripComboNormalizedTo.Items.Add(Resources.AreaCVToolbar_UpdateUI_Global_standards);
            toolStripComboNormalizedTo.Items.Add(Resources.AreaCVToolbar_UpdateUI_Medians);
            toolStripComboNormalizedTo.Items.Add(Resources.AreaCVToolbar_UpdateUI_None);

            if (AreaGraphController.NormalizationMethod == AreaCVNormalizationMethod.ratio)
                toolStripComboNormalizedTo.SelectedIndex = AreaGraphController.AreaCVRatioIndex;
            else
            {
                var index = _standardTypeCount + (int) AreaGraphController.NormalizationMethod;
                if (!hasGlobalStandard)
                    --index;
                toolStripComboNormalizedTo.SelectedIndex = index;
            }
                
        }

        private readonly Timer _timer;
        private int _standardTypeCount;

        #region Functional Test Support
        public int MinDetections { get { return (int) toolStripNumericDetections.NumericUpDownControl.Minimum; } }

        public int Detections { get { return (int) toolStripNumericDetections.NumericUpDownControl.Value; } }

        public int MaxDetections { get { return (int) toolStripNumericDetections.NumericUpDownControl.Maximum; } }

        public bool DetectionsVisible { get {  return toolStripLabel2.Visible && toolStripNumericDetections.Visible && toolStripLabel3.Visible; } }

        public bool GroupsVisible { get { return toolStripLabel1.Visible && toolStripComboGroup.Visible; } }
        #endregion
    }
}