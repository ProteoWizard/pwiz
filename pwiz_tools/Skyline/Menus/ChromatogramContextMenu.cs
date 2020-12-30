using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Menus
{
    public partial class ChromatogramContextMenu : SkylineControl
    {
        public ChromatogramContextMenu(SkylineWindow skylineWindow) : base(skylineWindow)
        {
            InitializeComponent();
        }

        private void applyPeakAllMenuItem_Click(object sender, EventArgs e)
        {
            ApplyPeak(false, false);
        }

        private void applyPeakSubsequentMenuItem_Click(object sender, EventArgs e)
        {
            ApplyPeak(true, false);
        }

        private void applyPeakGroupGraphMenuItem_Click(object sender, EventArgs e)
        {
            ApplyPeak(false, true);
        }

        public void ApplyPeak(bool subsequent, bool group)
        {
            SkylineWindow.EditMenu.ApplyPeak(subsequent, group);
        }

        private void removePeakMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            SkylineWindow.CanApplyOrRemovePeak(null, null, out _, out var canRemove);
            if (!canRemove)
                return;

            if (!(sender is ToolStripMenuItem menu) || !menu.DropDownItems.OfType<object>().Any())
                return;

            var nodeGroupTree = SequenceTree.GetNodeOfType<TransitionGroupTreeNode>();
            if (nodeGroupTree != null)
            {
                var nodeGroup = nodeGroupTree.DocNode;
                var pathGroup = nodeGroupTree.Path;
                var nodeTranTree = (TransitionTreeNode) SelectedNode;
                var nodeTran = nodeTranTree.DocNode;

                menu.DropDownItems.Clear();

                if (nodeGroup.TransitionCount > 1)
                {
                    var handler = new RemovePeakHandler(SkylineWindow, pathGroup, nodeGroup, null);
                    var item = new ToolStripMenuItem(
                        Resources.SkylineWindow_removePeaksGraphMenuItem_DropDownOpening_All, null,
                        handler.menuItem_Click);
                    menu.DropDownItems.Insert(0, item);
                }

                var chromInfo = nodeTran.GetChromInfo(SequenceTree.ResultsIndex, GetSelectedChromFileId());
                if (chromInfo != null && !chromInfo.IsEmpty)
                {
                    var handler = new RemovePeakHandler(SkylineWindow, pathGroup, nodeGroup, nodeTran);
                    var item = new ToolStripMenuItem(ChromGraphItem.GetTitle(nodeTran), null, handler.menuItem_Click);
                    menu.DropDownItems.Insert(0, item);
                }

                return;
            }

            var nodePepTree = SequenceTree.GetNodeOfType<PeptideTreeNode>();
            if (nodePepTree != null)
            {
                var placeholder = menu.DropDownItems.OfType<object>().FirstOrDefault() as ToolStripMenuItem;
                if (placeholder == null)
                    return;

                var isotopeLabelType = placeholder.Tag as IsotopeLabelType;
                if (isotopeLabelType == null)
                    return;

                menu.DropDownItems.Clear();

                var transitionGroupDocNode = nodePepTree.DocNode.TransitionGroups.FirstOrDefault(transitionGroup =>
                    Equals(transitionGroup.TransitionGroup.LabelType, isotopeLabelType));
                if (transitionGroupDocNode == null)
                    return;

                var item = new ToolStripMenuItem(Resources.SkylineWindow_removePeaksGraphMenuItem_DropDownOpening_All,
                    null, removePeakMenuItem_Click);
                menu.DropDownItems.Insert(0, item);

                var handler = new RemovePeakHandler(SkylineWindow,
                    new IdentityPath(nodePepTree.Path, transitionGroupDocNode.Id), transitionGroupDocNode, null);
                item = new ToolStripMenuItem(isotopeLabelType.Title, null, handler.menuItem_Click);
                menu.DropDownItems.Insert(0, item);
            }
        }

        private void removePeakMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.RemovePeak(true);
        }

        private class RemovePeakHandler
        {
            private readonly SkylineWindow _skyline;
            private readonly IdentityPath _groupPath;
            private readonly TransitionGroupDocNode _nodeGroup;
            private readonly TransitionDocNode _nodeTran;

            public RemovePeakHandler(SkylineWindow skyline, IdentityPath groupPath,
                TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran)
            {
                _skyline = skyline;
                _groupPath = groupPath;
                _nodeGroup = nodeGroup;
                _nodeTran = nodeTran;
            }

            public void menuItem_Click(object sender, EventArgs e)
            {
                _skyline.RemovePeak(_groupPath, _nodeGroup, _nodeTran);
            }
        }

        private void legendChromContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowChromatogramLegends(legendChromContextMenuItem.Checked);
        }

        private void peakBoundariesContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPeakBoundaries(peakBoundariesContextMenuItem.Checked);
        }

        private void originalPeakContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowOriginalPeak(originalPeakMenuItem.Checked);
        }

        private void massErrorContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowMassErrors(massErrorContextMenuItem.Checked);
        }

        private void retentionTimesContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var showRT = GraphChromatogram.ShowRT;

            allRTContextMenuItem.Checked = (showRT == ShowRTChrom.all);
            bestRTContextMenuItem.Checked = (showRT == ShowRTChrom.best);
            thresholdRTContextMenuItem.Checked = (showRT == ShowRTChrom.threshold);
            noneRTContextMenuItem.Checked = (showRT == ShowRTChrom.none);
        }

        private void allRTContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.SetShowRetentionTimes(ShowRTChrom.all);
        }

        private void bestRTContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.SetShowRetentionTimes(ShowRTChrom.best);
        }
        private void noneRTContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.SetShowRetentionTimes(ShowRTChrom.none);
        }

        private void thresholdRTContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowChromatogramRTThresholdDlg();
        }

        public void ShowChromatogramRTThresholdDlg()
        {
            SkylineWindow.ShowChromatogramRTThresholdDlg();
        }

        private void rawTimesContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ToggleRawTimesMenuItem();
        }
        private void retentionTimePredContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.SetShowRetentionTimePred(retentionTimePredContextMenuItem.Checked);
        }

        private void idTimesNoneContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.HideAllIdTimes();
        }
        private void peptideIDTimesContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPeptideIDTimes(idTimesMatchingContextMenuItem.Checked);
        }
        private void alignedPeptideIDTimesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowAlignedPeptideIDTimes(idTimesAlignedContextMenuItem.Checked);
        }
        private void peptideIDTimesFromOtherRunsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowOtherRunPeptideIDTimes(idTimesOtherContextMenuItem.Checked);
        }
        private void transitionsMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var displayType = GraphChromatogram.DisplayType;

            // If both MS1 and MS/MS ions are not possible, then menu items to differentiate precursors and
            // products are not necessary.
            bool showIonTypeOptions = SkylineWindow.IsMultipleIonSources;
            precursorsTranContextMenuItem.Visible =
                productsTranContextMenuItem.Visible = showIonTypeOptions;

            if (!showIonTypeOptions &&
                (displayType == DisplayTypeChrom.precursors || displayType == DisplayTypeChrom.products))
                displayType = DisplayTypeChrom.all;

            // Only show all ions chromatogram options when at least one chromatogram of this type exists
            bool showAllIonsOptions = DocumentUI.Settings.HasResults &&
                DocumentUI.Settings.MeasuredResults.HasAllIonsChromatograms;

            basePeakContextMenuItem.Visible =
                ticContextMenuItem.Visible =
                    qcContextMenuItem.Visible =
                        toolStripSeparatorTran.Visible = showAllIonsOptions;

            if (!showAllIonsOptions &&
                    (displayType == DisplayTypeChrom.base_peak || displayType == DisplayTypeChrom.tic || displayType == DisplayTypeChrom.qc))
                displayType = DisplayTypeChrom.all;

            if (showAllIonsOptions)
            {
                qcContextMenuItem.DropDownItems.Clear();
                var qcTraceNames = DocumentUI.MeasuredResults.QcTraceNames.ToList();
                if (qcTraceNames.Count > 0)
                {
                    var qcContextTraceItems = new ToolStripItem[qcTraceNames.Count];
                    for (int i = 0; i < qcTraceNames.Count; i++)
                    {
                        qcContextTraceItems[i] = new ToolStripMenuItem(qcTraceNames[i], null, qcMenuItem_Click)
                        {
                            Checked = displayType == DisplayTypeChrom.qc &&
                                      Settings.Default.ShowQcTraceName == qcTraceNames[i]
                        };
                    }

                    qcContextMenuItem.DropDownItems.AddRange(qcContextTraceItems);
                }
                else
                    qcContextMenuItem.Visible = false;
            }

            precursorsTranContextMenuItem.Checked = (displayType == DisplayTypeChrom.precursors);
            productsTranContextMenuItem.Checked = (displayType == DisplayTypeChrom.products);
            singleTranContextMenuItem.Checked = (displayType == DisplayTypeChrom.single);
            allTranContextMenuItem.Checked = (displayType == DisplayTypeChrom.all);
            totalTranContextMenuItem.Checked = (displayType == DisplayTypeChrom.total);
            basePeakContextMenuItem.Checked = (displayType == DisplayTypeChrom.base_peak);
            ticContextMenuItem.Checked = (displayType == DisplayTypeChrom.tic);
            splitGraphContextMenuItem.Checked = Settings.Default.SplitChromatogramGraph;
            onlyQuantitativeContextMenuItem.Checked = Settings.Default.ShowQuantitativeOnly;
        }
        private void qcMenuItem_Click(object sender, EventArgs e)
        {
            var qcTraceItem = sender as ToolStripMenuItem;
            if (qcTraceItem == null)
                throw new InvalidOperationException(@"qcMenuItem_Click must be triggered by a ToolStripMenuItem");
            SkylineWindow.ShowQc(qcTraceItem.Text);
        }
        private void allTranMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowAllTransitions();
        }
        private void precursorsTranMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPrecursorTransitions();
        }
        private void productsTranMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowProductTransitions();
        }

        private void singleTranMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowSingleTransition();
        }
        private void totalTranMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowTotalTransitions();
        }
        private void basePeakMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowBasePeak();
        }
        private void ticMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowTic();
        }
        private void onlyQuantitativeMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowOnlyQuantitative(!Settings.Default.ShowQuantitativeOnly);
        }
        private void splitChromGraphMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowSplitChromatogramGraph(!Settings.Default.SplitChromatogramGraph);
        }
        private void transformChromMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var transform = GraphChromatogram.Transform;

            transformChromNoneContextMenuItem.Checked = (transform == TransformChrom.raw);
            transformChromInterpolatedContextMenuItem.Checked = (transform == TransformChrom.interpolated);
            secondDerivativeContextMenuItem.Checked = (transform == TransformChrom.craw2d);
            firstDerivativeContextMenuItem.Checked = (transform == TransformChrom.craw1d);
            smoothSGChromContextMenuItem.Checked = (transform == TransformChrom.savitzky_golay);
        }
        private void transformChromNoneMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.SetTransformChrom(TransformChrom.raw);
        }


        private void transformInterpolatedMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.SetTransformChrom(TransformChrom.interpolated);
        }


        private void secondDerivativeMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.SetTransformChrom(TransformChrom.craw2d);
        }

        private void firstDerivativeMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.SetTransformChrom(TransformChrom.craw1d);
        }

        private void smoothSGChromMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.SetTransformChrom(TransformChrom.savitzky_golay);
        }

        private void autozoomMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            bool hasRt = (DocumentUI.Settings.PeptideSettings.Prediction.RetentionTime != null);
            autoZoomRTWindowContextMenuItem.Enabled = hasRt;
            autoZoomBothContextMenuItem.Enabled = hasRt;

            var zoom = SkylineWindow.EffectiveAutoZoom;
            autoZoomNoneContextMenuItem.Checked = (zoom == AutoZoomChrom.none);
            autoZoomBestPeakContextMenuItem.Checked = (zoom == AutoZoomChrom.peak);
            autoZoomRTWindowContextMenuItem.Checked = (zoom == AutoZoomChrom.window);
            autoZoomBothContextMenuItem.Checked = (zoom == AutoZoomChrom.both);
        }

        private void autoZoomNoneMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.AutoZoomNone();
        }

        private void autoZoomBestPeakMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.SetAutoZoomChrom(AutoZoomChrom.peak);
        }
        private void lockYChromContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.LockYChrom(lockYChromContextMenuItem.Checked);
        }
        private void autoZoomRTWindowMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.AutoZoomRTWindow();
        }
        private void autoZoomBothMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.AutoZoomBoth();
        }
        private void synchronizeZoomingContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.SynchronizeZooming(synchronizeZoomingContextMenuItem.Checked);
        }
        private void chromPropsContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowChromatogramProperties();
        }

    }
}
