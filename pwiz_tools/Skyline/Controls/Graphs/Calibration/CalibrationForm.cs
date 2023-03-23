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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using ZedGraph;
using SampleType = pwiz.Skyline.Model.DocSettings.AbsoluteQuantification.SampleType;

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
            _originalFormTitle = Text;
            calibrationGraphControl1.ZedGraphControl.ContextMenuBuilder += zedGraphControl_ContextMenuBuilder;
        }

        public static CalibrationCurveOptions Options { get { return Settings.Default.CalibrationCurveOptions; } }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (null != _skylineWindow)
            {
                _skylineWindow.DocumentUIChangedEvent += SkylineWindowOnDocumentUIChangedEvent;
                UpdateUI(false);
            }
        }

        private void SkylineWindowOnDocumentUIChangedEvent(object sender, DocumentChangedEventArgs documentChangedEventArgs)
        {
            UpdateUI(false);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
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
            CalibrationCurveOptions options = Settings.Default.CalibrationCurveOptions;
            var document = _skylineWindow.DocumentUI;

            if (!document.Settings.HasResults)
            {
                DisplayError(QuantificationStrings.CalibrationForm_DisplayCalibrationCurve_No_results_available);
                return;
            }
            PeptideDocNode peptide;
            PeptideGroupDocNode peptideGroup;

            if (!TryGetSelectedPeptide(out peptideGroup,out peptide))
            {
                DisplayError(ModeUIAwareStringFormat(QuantificationStrings
                    .CalibrationForm_DisplayCalibrationCurve_Select_a_peptide_to_see_its_calibration_curve));
                return;
            }
            if (-1 == document.Children.IndexOf(peptideGroup))
            {
                DisplayError(ModeUIAwareStringFormat(QuantificationStrings
                    .CalibrationForm_DisplayCalibrationCurve_The_selected_peptide_is_no_longer_part_of_the_Skyline_document_));
                return;
            }
            PeptideQuantifier peptideQuantifier = PeptideQuantifier.GetPeptideQuantifier(document, peptideGroup,
                peptide);
            CalibrationCurveFitter curveFitter = new CalibrationCurveFitter(peptideQuantifier, document.Settings);
            if (curveFitter.IsEnableSingleBatch && Settings.Default.CalibrationCurveOptions.SingleBatch)
            {
                curveFitter.SingleBatchReplicateIndex = _skylineWindow.SelectedResultsIndex;
            }

            Text = TabText = GetFormTitle(curveFitter);
            if (peptideQuantifier.QuantificationSettings.RegressionFit == RegressionFit.NONE)
            {
                if (!(peptideQuantifier.NormalizationMethod is NormalizationMethod.RatioToLabel))
                {
                    GraphTitle =
                        ModeUIAwareStringFormat(QuantificationStrings.CalibrationForm_DisplayCalibrationCurve_Use_the_Quantification_tab_on_the_Peptide_Settings_dialog_to_control_the_conversion_of_peak_areas_to_concentrations_);
                }
                else
                {
                    if (!peptide.InternalStandardConcentration.HasValue)
                    {
                        GraphTitle =
                            ModeUIAwareStringFormat(QuantificationStrings.CalibrationForm_DisplayCalibrationCurve_To_convert_peak_area_ratios_to_concentrations__specify_the_internal_standard_concentration_for__0__, peptide);
                    }
                    else
                    {
                        GraphTitle = null;
                    }
                }
            }
            else
            {
                if (curveFitter.GetStandardConcentrations().Any())
                {
                    GraphTitle = null;
                }
                else
                {
                    GraphTitle = QuantificationStrings.CalibrationForm_DisplayCalibrationCurve_To_fit_a_calibration_curve__set_the_Sample_Type_of_some_replicates_to_Standard__and_specify_their_concentration_;
                }
            }

            var settings = new CalibrationGraphControl.Settings(document, curveFitter, options)
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

        private bool TryGetSelectedPeptide(out PeptideGroupDocNode peptideGroup, out PeptideDocNode peptide)
        {
            peptide = null;
            peptideGroup = null;
            SequenceTree sequenceTree = _skylineWindow.SequenceTree;
            if (null != sequenceTree)
            {
                PeptideTreeNode peptideTreeNode = sequenceTree.GetNodeOfType<PeptideTreeNode>();
                if (null != peptideTreeNode)
                {
                    peptide = peptideTreeNode.DocNode;
                }
                PeptideGroupTreeNode peptideGroupTreeNode = sequenceTree.GetNodeOfType<PeptideGroupTreeNode>();
                if (null != peptideGroupTreeNode)
                {
                    peptideGroup = peptideGroupTreeNode.DocNode;
                }
            }
            return peptide != null;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Dispose();
        }


        private bool IsEnableIsotopologResponseCurve()
        {
            return TryGetSelectedPeptide(out _, out var peptide) &&
                   peptide.TransitionGroups.Any(tg => tg.PrecursorConcentration.HasValue);
        }

        private void zedGraphControl_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            var calibrationCurveOptions = Settings.Default.CalibrationCurveOptions;
            singleBatchContextMenuItem.Checked = calibrationCurveOptions.SingleBatch;
            if (IsEnableIsotopologResponseCurve())
            {
                singleBatchContextMenuItem.Visible = true;
            }
            else
            {
                singleBatchContextMenuItem.Visible = CalibrationCurveFitter.AnyBatchNames(_skylineWindow.Document.Settings);
            }
            var replicateIndexFromPoint = calibrationGraphControl1.ReplicateIndexFromPoint(mousePt);
            if (replicateIndexFromPoint.HasValue && null == replicateIndexFromPoint.Value.LabelType)
            {
                ToolStripMenuItem excludeStandardMenuItem 
                    = MakeExcludeStandardMenuItem(replicateIndexFromPoint.Value.ReplicateIndex);
                if (excludeStandardMenuItem != null)
                {
                    menuStrip.Items.Clear();
                    menuStrip.Items.Add(excludeStandardMenuItem);
                    return;
                }
            }
            
            showSampleTypesContextMenuItem.DropDownItems.Clear();
            foreach (var sampleType in SampleType.ListSampleTypes())
            {
                showSampleTypesContextMenuItem.DropDownItems.Add(MakeShowSampleTypeMenuItem(sampleType));
            }
            logXContextMenuItem.Checked = Options.LogXAxis;
            logYAxisContextMenuItem.Checked = Options.LogYAxis;
            showLegendContextMenuItem.Checked = Options.ShowLegend;
            showSelectionContextMenuItem.Checked = Options.ShowSelection;
            showFiguresOfMeritContextMenuItem.Checked = Options.ShowFiguresOfMerit;
            ZedGraphHelper.BuildContextMenu(sender, menuStrip, true);
            if (!menuStrip.Items.Contains(logXContextMenuItem))
            {
                int index = 0;
                menuStrip.Items.Insert(index++, logXContextMenuItem);
                menuStrip.Items.Insert(index++, logYAxisContextMenuItem);
                menuStrip.Items.Insert(index++, showSampleTypesContextMenuItem);
                menuStrip.Items.Insert(index++, singleBatchContextMenuItem);
                menuStrip.Items.Insert(index++, showLegendContextMenuItem);
                menuStrip.Items.Insert(index++, showSelectionContextMenuItem);
                menuStrip.Items.Insert(index++, showFiguresOfMeritContextMenuItem);
                menuStrip.Items.Insert(index++, new ToolStripSeparator());
            }
        }

        public ToolStripMenuItem MakeExcludeStandardMenuItem(int replicateIndex)
        {
            var document = DocumentUiContainer.DocumentUI;
            if (!document.Settings.HasResults)
            {
                return null;
            }
            ChromatogramSet chromatogramSet = null;
            if (replicateIndex >= 0 &&
                replicateIndex < document.Settings.MeasuredResults.Chromatograms.Count)
            {
                chromatogramSet = document.Settings.MeasuredResults.Chromatograms[replicateIndex];
            }
            if (chromatogramSet == null)
            {
                return null;
            }
            if (!chromatogramSet.SampleType.AllowExclude)
            {
                return null;
            }
            PeptideDocNode peptideDocNode;
            PeptideGroupDocNode peptideGroupDocNode;
            if (!TryGetSelectedPeptide(out peptideGroupDocNode, out peptideDocNode))
            {
                return null;
            }
            bool isExcluded = peptideDocNode.IsExcludeFromCalibration(replicateIndex);
            var menuItemText = isExcluded ? QuantificationStrings.CalibrationForm_MakeExcludeStandardMenuItem_Include_Standard 
                : QuantificationStrings.CalibrationForm_MakeExcludeStandardMenuItem_Exclude_Standard;
            var peptideIdPath = new IdentityPath(peptideGroupDocNode.Id, peptideDocNode.Id);
            var menuItem = new ToolStripMenuItem(menuItemText, null, (sender, args) =>
            {
                _skylineWindow.ModifyDocument(menuItemText,
                    doc => SetExcludeStandard(doc, peptideIdPath, replicateIndex, !isExcluded), docPair =>
                    {
                        var msgType = isExcluded
                            ? MessageType.set_included_standard
                            : MessageType.set_excluded_standard;
                        return AuditLogEntry.CreateSingleMessageEntry(new MessageInfo(msgType, docPair.NewDocumentType, PeptideTreeNode.GetLabel(peptideDocNode, string.Empty), chromatogramSet.Name));
                    });
            });
            return menuItem;
        }

        private ToolStripMenuItem MakeShowSampleTypeMenuItem(SampleType sampleType)
        {
            ToolStripMenuItem menuItem = new ToolStripMenuItem(sampleType.ToString())
            {
                Checked = Options.DisplaySampleTypes.Contains(sampleType.Name)
            };
            menuItem.Click += (sender, args) =>
            {
                if (menuItem.Checked)
                {
                    Options.DisplaySampleTypes = Options.DisplaySampleTypes.Except(new[] {sampleType.Name}).ToArray();
                }
                else
                {
                    Options.DisplaySampleTypes =
                        Options.DisplaySampleTypes.Concat(new[] {sampleType.Name}).Distinct().ToArray();
                }
                UpdateUI(false);
            };
            return menuItem;
        }


        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            Dispose();
        }

        private void logXAxisContextMenuItem_Click(object sender, EventArgs e)
        {
            Options.LogXAxis = !Options.LogXAxis;
            UpdateUI(false);
        }

        private void logYAxisContextMenuItem_Click(object sender, EventArgs e)
        {
            Options.LogYAxis = !Options.LogYAxis;
            UpdateUI(false);
        }


        private void showLegendContextMenuItem_Click(object sender, EventArgs e)
        {
            Options.ShowLegend = !Options.ShowLegend;
            UpdateUI(false);
        }

        private void showSelectionContextMenuItem_Click(object sender, EventArgs e)
        {
            Options.ShowSelection = !Options.ShowSelection;
            UpdateUI(false);
        }


        private void showFiguresOfMeritContextMenuItem_Click(object sender, EventArgs e)
        {
            Options.ShowFiguresOfMerit = !Options.ShowFiguresOfMerit;
            UpdateUI(false);
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

        private SrmDocument SetExcludeStandard(SrmDocument document, IdentityPath peptideIdPath, int resultsIndex, bool exclude)
        {
            if (!document.Settings.HasResults)
            {
                return document;
            }
            var peptideDocNode = (PeptideDocNode) document.FindNode(peptideIdPath);
            if (peptideDocNode == null)
            {
                return document;
            }
            if (resultsIndex < 0 || resultsIndex >= document.Settings.MeasuredResults.Chromatograms.Count)
            {
                return document;
            }
            bool wasExcluded = peptideDocNode.IsExcludeFromCalibration(resultsIndex);
            return (SrmDocument) document.ReplaceChild(peptideIdPath.Parent,
                peptideDocNode.ChangeExcludeFromCalibration(resultsIndex, !wasExcluded));
        }

        private void singleBatchContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.CalibrationCurveOptions.SingleBatch =
                !Settings.Default.CalibrationCurveOptions.SingleBatch;
            UpdateUI(false);
        }

        public void DisplayError(string message)
        {
            calibrationGraphControl1.DisplayError(message);
        }

        public string GraphTitle
        {
            get
            {
                return calibrationGraphControl1.GraphTitle;
            }
            set
            {
                calibrationGraphControl1.GraphTitle = value;
            }
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
