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
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    public sealed partial class AreaCVToolbar : GraphSummaryToolbar //UserControl // for designer
    {
        private readonly Timer _timer;
        private int _standardTypeCount;

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

        private void toolStripComboGroup_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetGroupIndex(toolStripComboGroup.SelectedIndex);
        }

        public void SetGroupIndex(int index)
        {
            if (index == 0)
            {
                Program.MainWindow.SetAreaCVAnnotation(null, false);
                var results = _graphSummary.DocumentUIContainer.DocumentUI.MeasuredResults;
                toolStripNumericDetections.NumericUpDownControl.Maximum =
                    results != null ? results.Chromatograms.Count : 0;
            }
            else
            {
                Program.MainWindow.SetAreaCVAnnotation(toolStripComboGroup.Items[index], false);
                var document = _graphSummary.DocumentUIContainer.DocumentUI;
                var groupByGroup =
                    ReplicateValue.FromPersistedString(document.Settings, AreaGraphController.GroupByGroup);
                toolStripNumericDetections.NumericUpDownControl.Maximum = AnnotationHelper
                    .GetReplicateIndices(document, groupByGroup, AreaGraphController.GroupByAnnotation).Length;
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
            // TODO(Tobi): Fix this. It causes an error loading a saved layout with an Area CV graph and normalization meth
            if (Program.MainWindow == null)
                return;

            if (index < _standardTypeCount)
            {
                Program.MainWindow.SetNormalizationMethod(AreaCVNormalizationMethod.ratio, index, false);
            }
            else
            {
                index -= _standardTypeCount;
                if (!_graphSummary.DocumentUIContainer.DocumentUI.Settings.HasGlobalStandardArea)
                    ++index;

                var normalizationMethod = AreaCVNormalizationMethod.none;
                switch (index)
                {
                    case 0:
                        normalizationMethod =
                            _graphSummary.DocumentUIContainer.Document.Settings.HasGlobalStandardArea
                                ? AreaCVNormalizationMethod.global_standards
                                : AreaCVNormalizationMethod.medians;
                        break;
                    case 1:
                        normalizationMethod = AreaCVNormalizationMethod.medians;
                        break;
                    case 2:
                        normalizationMethod = AreaCVNormalizationMethod.none;
                        break;
                }

                Program.MainWindow.SetNormalizationMethod(normalizationMethod, -1, false);
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
            SetMinimumDetections((int)toolStripNumericDetections.NumericUpDownControl.Value);
        }

        public void SetMinimumDetections(int min)
        {
            AreaGraphController.MinimumDetections = min;

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
            var info = _graphSummary.GraphPanes.FirstOrDefault() as IAreaCVHistogramInfo;
            if (info == null)
                return false;

            var document = _graphSummary.DocumentUIContainer.DocumentUI;
            return info.Cache.IsValidFor(document, new AreaCVGraphData.AreaCVGraphSettings(document.Settings, _graphSummary.Type)) &&
                info.Cache.Get(ReplicateValue.FromPersistedString(document.Settings, AreaGraphController.GroupByGroup),
                    AreaGraphController.GroupByAnnotation,
                    AreaGraphController.MinimumDetections,
                    AreaGraphController.NormalizationMethod,
                    AreaGraphController.AreaCVRatioIndex) != null;
        }

        private void toolStripProperties_Click(object sender, EventArgs e)
        {
            using (var dlgProperties = new AreaCVToolbarProperties(_graphSummary))
            {
                if (dlgProperties.ShowDialog(FormEx.GetParentForm(this)) == DialogResult.OK)
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

            if (!document.Settings.HasResults)
                return;

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
                    toolStripNumericDetections.NumericUpDownControl.Maximum = AnnotationHelper.GetReplicateIndices(document, ReplicateValue.FromPersistedString(document.Settings, AreaGraphController.GroupByGroup), AreaGraphController.GroupByAnnotation).Length;

                if (!detectionsVisiblePrev)
                    toolStripNumericDetections.NumericUpDownControl.Value = 2;
            }

            if (groupsVisible)
            {
                var annotations = new[] {Resources.GraphSummary_UpdateToolbar_All}.Concat(
                    AnnotationHelper.GetPossibleAnnotations(document,
                            ReplicateValue.FromPersistedString(document.Settings, AreaGraphController.GroupByGroup))
                        .Except(new object[] {null})).ToArray();

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
            _standardTypeCount = 0;

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

        #region Functional Test Support
        public int MinDetections { get { return (int) toolStripNumericDetections.NumericUpDownControl.Minimum; } }

        public int Detections { get { return (int) toolStripNumericDetections.NumericUpDownControl.Value; } }

        public int MaxDetections { get { return (int) toolStripNumericDetections.NumericUpDownControl.Maximum; } }

        public bool DetectionsVisible { get {  return toolStripLabel2.Visible && toolStripNumericDetections.Visible && toolStripLabel3.Visible; } }

        public bool GroupsVisible { get { return toolStripLabel1.Visible && toolStripComboGroup.Visible; } }

        public IEnumerable<object> Annotations
        {
            get { return toolStripComboGroup.Items.Cast<object>(); }
            set
            {
                toolStripComboGroup.Items.Clear();
                // ReSharper disable once CoVariantArrayConversion
                toolStripComboGroup.Items.AddRange(value.ToArray());
            }
        }

        public IEnumerable<string> NormalizationMethods
        {
            get { return toolStripComboNormalizedTo.Items.Cast<string>(); }
            set
            {
                toolStripComboNormalizedTo.Items.Clear();
                // ReSharper disable once CoVariantArrayConversion
                toolStripComboNormalizedTo.Items.AddRange(value.ToArray());
            }
        }
        #endregion
    }
}