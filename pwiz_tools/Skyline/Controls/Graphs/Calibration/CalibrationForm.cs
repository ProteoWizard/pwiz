/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs.Calibration
{
    public partial class CalibrationForm : DockableFormEx, IUpdatable
    {
        private readonly SkylineWindow _skylineWindow;
        private string _originalFormTitle;
        public CalibrationForm(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            _skylineWindow = skylineWindow;
            calibrationGraphControl1.SkylineWindow = skylineWindow;
            _originalFormTitle = Text;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (null != _skylineWindow)
            {
                _skylineWindow.DocumentUIChangedEvent += SkylineWindowOnDocumentUIChangedEvent;
                Settings.Default.PropertyChanged += Settings_OnPropertyChanged;
                UpdateUI(false);
            }
        }

        private void Settings_OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.CalibrationCurveOptions))
            {
                UpdateUI(false);
            }
        }

        private void SkylineWindowOnDocumentUIChangedEvent(object sender, DocumentChangedEventArgs documentChangedEventArgs)
        {
            UpdateUI(false);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            Settings.Default.PropertyChanged -= Settings_OnPropertyChanged;
            if (null != _skylineWindow)
            {
                _skylineWindow.DocumentUIChangedEvent -= SkylineWindowOnDocumentUIChangedEvent;
            }
            base.OnHandleDestroyed(e);
        }

        public IDocumentUIContainer DocumentUiContainer
        {
            get { return _skylineWindow; }
        }

        public void UpdateUI(bool selectionChanged)
        {
            try
            {
                if (IsDisposed)
                {
                    return;
                }
                DisplayCalibrationCurve();
            }
            catch (Exception e)
            {
                Program.ReportException(e);
            }
        }

        public CalibrationGraphControl CalibrationGraphControl
        {
            get
            {
                return calibrationGraphControl1;
            }
        }

        public CalibrationCurve CalibrationCurve
        {
            get { return calibrationGraphControl1.CalibrationCurve; }
        }
        public CalibrationCurveMetrics CalibrationCurveMetrics
        {
            get { return calibrationGraphControl1.CalibrationCurveMetrics; }
        }
        private void DisplayCalibrationCurve()
        {
            Text = TabText = _originalFormTitle;
            var document = _skylineWindow.DocumentUI;

            if (!document.Settings.HasResults)
            {
                DisplayError(QuantificationStrings.CalibrationForm_DisplayCalibrationCurve_No_results_available);
                return;
            }

            IdPeptideDocNode idPeptideDocNode = GetSelectedPeptide();
            if (idPeptideDocNode == null)
            {
                DisplayError(ModeUIAwareStringFormat(QuantificationStrings
                    .CalibrationForm_DisplayCalibrationCurve_Select_a_peptide_to_see_its_calibration_curve));
                return;
            }
            if (document.FindNodeIndex(idPeptideDocNode.PeptideGroup) < 0)
            {
                DisplayError(ModeUIAwareStringFormat(QuantificationStrings
                    .CalibrationForm_DisplayCalibrationCurve_The_selected_peptide_is_no_longer_part_of_the_Skyline_document_));
                return;
            }
            CalibrationCurveFitter curveFitter = CalibrationCurveFitter.GetCalibrationCurveFitter(document, idPeptideDocNode);
            var mainPeptideQuantifier = curveFitter.PeptideQuantifier;
            if (curveFitter.IsEnableSingleBatch && Settings.Default.CalibrationCurveOptions.SingleBatch)
            {
                curveFitter.SingleBatchReplicateIndex = _skylineWindow.SelectedResultsIndex;
            }

            Text = TabText = GetFormTitle(curveFitter);
            var settings = new CalibrationGraphControl.Settings(document, curveFitter)
                .ChangeSelectedResultsIndex(_skylineWindow.SelectedResultsIndex);
            if (curveFitter.IsotopologResponseCurve)
            {
                var labelType = (_skylineWindow.SequenceTree.SelectedNode as SrmTreeNode)
                    ?.GetNodeOfType<TransitionGroupTreeNode>()?.DocNode.LabelType;
                settings = settings.ChangeSelectedLabelType(labelType);
            }
            calibrationGraphControl1.Update(settings);
        }

        private string GetFormTitle(CalibrationCurveFitter curveFitter)
        {
            string title = TextUtil.SpaceSeparate(_originalFormTitle + ':', curveFitter.PeptideQuantifier.PeptideDocNode.ModifiedSequenceDisplay);
            if (curveFitter.SingleBatchReplicateIndex.HasValue)
            {
                var chromatogramSet = _skylineWindow.Document.Settings.MeasuredResults.Chromatograms[
                    curveFitter.SingleBatchReplicateIndex.Value];
                if (string.IsNullOrEmpty(chromatogramSet.BatchName))
                {
                    title = TextUtil.SpaceSeparate(title, string.Format(QuantificationStrings.CalibrationForm_GetFormTitle__Replicate___0__, chromatogramSet.Name));
                }
                else
                {
                    title = TextUtil.SpaceSeparate(title, string.Format(QuantificationStrings.CalibrationForm_GetFormTitle__Batch___0__, chromatogramSet.BatchName));
                }
            }
            else
            {
                if (_skylineWindow.Document.Settings.HasResults && _skylineWindow.Document.Settings.MeasuredResults
                    .Chromatograms.Select(c => c.BatchName).Distinct().Count() > 1)
                {
                    title = TextUtil.SpaceSeparate(title, QuantificationStrings.CalibrationForm_GetFormTitle__All_Replicates_);
                }
            }
            return title;
        }

        private IdPeptideDocNode GetSelectedPeptide()
        {
            SequenceTree sequenceTree = _skylineWindow.SequenceTree;
            PeptideTreeNode peptideTreeNode = sequenceTree?.GetNodeOfType<PeptideTreeNode>();
            PeptideGroupTreeNode peptideGroupTreeNode = sequenceTree?.GetNodeOfType<PeptideGroupTreeNode>();
            if (peptideGroupTreeNode != null && peptideTreeNode != null)
            {
                return new IdPeptideDocNode(peptideGroupTreeNode.DocNode.PeptideGroup, peptideTreeNode.DocNode);
            }
            return null;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Dispose();
        }


        public ToolStripMenuItem MakeExcludeStandardMenuItem(int replicateIndex)
        {
            return calibrationGraphControl1.MakeExcludeStandardMenuItem(replicateIndex);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            Dispose();
        }

        private void CalibrationForm_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    _skylineWindow.FocusDocument();
                    e.Handled = true;
                    break;
            }
        }

        public static bool IsNumber(double? value)
        {
            if (!value.HasValue)
            {
                return false;
            }
            if (double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            {
                return false;
            }
            return true;
        }

        #region Test Methods
        public ZedGraphControl ZedGraphControl { get { return calibrationGraphControl1.ZedGraphControl; } }
        #endregion

        public void DisplayError(string message)
        {
            calibrationGraphControl1.DisplayError(message);
        }

        private void calibrationGraphControl1_PointClicked(CalibrationPoint calibrationPoint)
        {
            _skylineWindow.SelectedResultsIndex = calibrationPoint.ReplicateIndex;
            if (null != calibrationPoint.LabelType)
            {
                var selectedTransitionGroup = (_skylineWindow.SequenceTree.SelectedNode as SrmTreeNode)
                    ?.GetNodeOfType<TransitionGroupTreeNode>();
                if (selectedTransitionGroup == null || !Equals(selectedTransitionGroup.DocNode.LabelType,
                        calibrationPoint.LabelType))
                {
                    var selectedPeptide = (_skylineWindow.SequenceTree.SelectedNode as SrmTreeNode)
                        ?.GetNodeOfType<PeptideTreeNode>();
                    if (selectedPeptide != null)
                    {
                        var transitionGroupToSelect = selectedPeptide.Nodes.OfType<TransitionGroupTreeNode>()
                            .FirstOrDefault(node => Equals(calibrationPoint.LabelType, node.DocNode.LabelType));
                        if (transitionGroupToSelect != null)
                        {
                            _skylineWindow.SequenceTree.SelectedPath = transitionGroupToSelect.Path;
                        }
                    }
                }
            }
        }
    }
}
