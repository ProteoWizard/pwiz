/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Menus
{
    public partial class ChromatogramContextMenu : SkylineControl
    {
        public ChromatogramContextMenu(SkylineWindow skylineWindow) : base(skylineWindow)
        {
            InitializeComponent();
        }

        #region Build Menu
        public void BuildChromatogramMenu(ZedGraphControl zedGraphControl, PaneKey paneKey, ContextMenuStrip menuStrip, ChromFileInfoId chromFileInfoId)
        {
            // Store original menu items in an array, and insert a separator
            ToolStripItem[] items = new ToolStripItem[menuStrip.Items.Count];
            int iUnzoom = -1;
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = menuStrip.Items[i];
                string tag = (string)items[i].Tag;
                if (tag == @"unzoom")
                    iUnzoom = i;
            }

            if (iUnzoom != -1)
                menuStrip.Items.Insert(iUnzoom, toolStripSeparator26);

            // Insert skyline specific menus
            var set = Settings.Default;
            int iInsert = 0;

            var settings = DocumentUI.Settings;
            bool retentionPredict = (settings.PeptideSettings.Prediction.RetentionTime != null);
            bool peptideIdTimes = (settings.PeptideSettings.Libraries.HasLibraries &&
                                   (settings.TransitionSettings.FullScan.IsEnabled || settings.PeptideSettings.Libraries.HasMidasLibrary));
            AddApplyRemovePeak(menuStrip, paneKey.IsotopeLabelType, 1, ref iInsert);

            synchronizeIntegrationContextMenuItem.Checked = DocumentUI.GetSynchronizeIntegrationChromatogramSets().Any();
            menuStrip.Items.Insert(iInsert++, synchronizeIntegrationContextMenuItem);
            menuStrip.Items.Insert(iInsert++, toolStripSeparator33);

            legendChromContextMenuItem.Checked = set.ShowChromatogramLegend;
            menuStrip.Items.Insert(iInsert++, legendChromContextMenuItem);
            var fullScan = Document.Settings.TransitionSettings.FullScan;
            if (ChromatogramCache.FORMAT_VERSION_CACHE > ChromatogramCache.FORMAT_VERSION_CACHE_4
                && fullScan.IsEnabled
                && (fullScan.IsHighResPrecursor || fullScan.IsHighResProduct))
            {
                massErrorContextMenuItem.Checked = set.ShowMassError;
                menuStrip.Items.Insert(iInsert++, massErrorContextMenuItem);
            }
            iInsert = InsertIonMobilityMenuItems(menuStrip.Items, chromFileInfoId, iInsert);

            peakBoundariesContextMenuItem.Checked = set.ShowPeakBoundaries;
            menuStrip.Items.Insert(iInsert++, peakBoundariesContextMenuItem);

            originalPeakMenuItem.Checked = set.ShowOriginalPeak;
            menuStrip.Items.Insert(iInsert++, originalPeakMenuItem);

            menuStrip.Items.Insert(iInsert++, retentionTimesContextMenuItem);
            if (retentionTimesContextMenuItem.DropDownItems.Count == 0)
            {
                retentionTimesContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                {
                    allRTContextMenuItem,
                    bestRTContextMenuItem,
                    thresholdRTContextMenuItem,
                    noneRTContextMenuItem,
                    rawTimesMenuItemSplitter,
                    rawTimesContextMenuItem
                });
            }
            if (retentionPredict)
            {
                retentionTimePredContextMenuItem.Checked = set.ShowRetentionTimePred;
                menuStrip.Items.Insert(iInsert++, retentionTimePredContextMenuItem);
            }
            rawTimesContextMenuItem.Checked = set.ChromShowRawTimes;
            bool alignedTimes = settings.HasAlignedTimes();
            bool unalignedTimes = settings.HasUnalignedTimes();
            if (peptideIdTimes || alignedTimes || unalignedTimes)
            {
                menuStrip.Items.Insert(iInsert++, peptideIDTimesContextMenuItem);
                peptideIDTimesContextMenuItem.DropDownItems.Clear();
                idTimesNoneContextMenuItem.Checked = false;
                peptideIDTimesContextMenuItem.DropDownItems.Add(idTimesNoneContextMenuItem);
                if (peptideIdTimes)
                {
                    idTimesMatchingContextMenuItem.Checked = set.ShowPeptideIdTimes;
                    peptideIDTimesContextMenuItem.DropDownItems.Add(idTimesMatchingContextMenuItem);
                }
                if (settings.HasAlignedTimes())
                {
                    idTimesAlignedContextMenuItem.Checked = set.ShowAlignedPeptideIdTimes;
                    peptideIDTimesContextMenuItem.DropDownItems.Add(idTimesAlignedContextMenuItem);
                }
                if (settings.HasUnalignedTimes())
                {

                    idTimesOtherContextMenuItem.Checked = set.ShowUnalignedPeptideIdTimes;
                    peptideIDTimesContextMenuItem.DropDownItems.Add(idTimesOtherContextMenuItem);
                }

                idTimesNoneContextMenuItem.Checked = !peptideIDTimesContextMenuItem.DropDownItems
                    .Cast<ToolStripMenuItem>()
                    .Any(idItem => idItem.Checked);
            }
            menuStrip.Items.Insert(iInsert++, toolStripSeparator16);
            AddTransitionContextMenu(menuStrip, iInsert++);
            menuStrip.Items.Insert(iInsert++, transformChromContextMenuItem);
            // Sometimes child menuitems are stripped from the parent
            if (transformChromContextMenuItem.DropDownItems.Count == 0)
            {
                transformChromContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                {
                    transformChromNoneContextMenuItem,
                    transformChromInterpolatedContextMenuItem,
                    secondDerivativeContextMenuItem,
                    smoothSGChromContextMenuItem
                });
            }
            menuStrip.Items.Insert(iInsert++, toolStripSeparator17);
            menuStrip.Items.Insert(iInsert++, autoZoomContextMenuItem);
            // Sometimes child menuitems are stripped from the parent
            if (autoZoomContextMenuItem.DropDownItems.Count == 0)
            {
                autoZoomContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                {
                    autoZoomNoneContextMenuItem,
                    autoZoomBestPeakContextMenuItem,
                    autoZoomRTWindowContextMenuItem,
                    autoZoomBothContextMenuItem
                });
            }
            lockYChromContextMenuItem.Checked = set.LockYChrom;
            menuStrip.Items.Insert(iInsert++, lockYChromContextMenuItem);
            synchronizeZoomingContextMenuItem.Checked = set.AutoZoomAllChromatograms;
            menuStrip.Items.Insert(iInsert++, synchronizeZoomingContextMenuItem);
            iInsert = InsertAlignmentMenuItems(menuStrip.Items, chromFileInfoId, iInsert);
            menuStrip.Items.Insert(iInsert++, toolStripSeparator18);
            menuStrip.Items.Insert(iInsert++, chromPropsContextMenuItem);
            menuStrip.Items.Insert(iInsert, toolStripSeparator19);

            // Remove some ZedGraph menu items not of interest
            foreach (var item in items)
            {
                string tag = (string)item.Tag;
                if (tag == @"set_default" || tag == @"show_val")
                    menuStrip.Items.Remove(item);
            }
            ZedGraphClipboard.AddToContextMenu(zedGraphControl, menuStrip);
        }

        /// <summary>
        /// Insert ion mobility-related menu items as appropriate
        /// </summary>
        private int InsertIonMobilityMenuItems(ToolStripItemCollection items, ChromFileInfoId chromFileInfoId, int iInsert)
        {
            var chromFileInfo = DocumentUI.Settings.MeasuredResults?.Chromatograms
                .Select(chromatogramSet => chromatogramSet.GetFileInfo(chromFileInfoId))
                .FirstOrDefault(fileInfo => null != fileInfo);
            if (null != chromFileInfo && chromFileInfo.IonMobilityUnits != eIonMobilityUnits.none && chromFileInfo.IonMobilityUnits != eIonMobilityUnits.waters_sonar)
            {
                var asSubMenu = true;

                var ccsMenuItemText = Resources.ChromatogramContextMenu_Collision_Cross_Section;
                var ccsItem = new ToolStripMenuItem(ccsMenuItemText);
                ccsItem.Click += (sender, eventArgs) => SkylineWindow.ShowCollisionCrossSection = !SkylineWindow.ShowCollisionCrossSection;
                ccsItem.Checked = SkylineWindow.ShowCollisionCrossSection;

                string imMenuItemText;
                switch (chromFileInfo.IonMobilityUnits)
                {
                    case eIonMobilityUnits.drift_time_msec:
                        imMenuItemText = Resources.ChromatogramContextMenu_InsertIonMobilityMenuItems_Drift_Time;
                        break;
                    case eIonMobilityUnits.inverse_K0_Vsec_per_cm2:
                        imMenuItemText = Resources.ChromatogramContextMenu_InsertIonMobilityMenuItems_Inverse_Ion_Mobility;
                        break;
                    case eIonMobilityUnits.compensation_V:
                        imMenuItemText = Resources.ChromatogramContextMenu_InsertIonMobilityMenuItems_Compensation_Voltage;
                        asSubMenu = false; // No CCS value, no need to submenu
                        break;
                    default:
                        Assume.Fail(@"unknown ion mobility type");
                        imMenuItemText = string.Empty;
                        break;
                }
                var ionMobilityItem = new ToolStripMenuItem(imMenuItemText);
                ionMobilityItem.Click += (sender, eventArgs) => SkylineWindow.ShowIonMobility = !SkylineWindow.ShowIonMobility;
                ionMobilityItem.Checked = SkylineWindow.ShowIonMobility;

                if (asSubMenu)
                {
                    var imSubMenu = new ToolStripMenuItem(Resources.ChromatogramContextMenu_InsertIonMobilityMenuItems_Ion_Mobility);
                    imSubMenu.DropDownItems.Add(ccsItem);
                    imSubMenu.DropDownItems.Add(ionMobilityItem);
                    items.Insert(iInsert++, imSubMenu);
                }
                else
                {
                    items.Insert(iInsert++, ionMobilityItem);
                }

            }
            return iInsert;
        }

        /// <summary>
        /// If the predicted retention time is auto calculated, add a "Show {Prediction} score" menu item.
        /// If there are retention time alignments available for the specified chromFileInfoId, then adds 
        /// a "Align Times To {Specified File}" menu item to a context menu.
        /// </summary>
        private int InsertAlignmentMenuItems(ToolStripItemCollection items, ChromFileInfoId chromFileInfoId, int iInsert)
        {
            var predictRT = Document.Settings.PeptideSettings.Prediction.RetentionTime;
            if (predictRT != null && predictRT.IsAutoCalculated)
            {
                var menuItem = new ToolStripMenuItem(string.Format(Resources.SkylineWindow_ShowCalculatorScoreFormat, predictRT.Calculator.Name), null,
                    (sender, eventArgs) => SkylineWindow.AlignToRtPrediction = !SkylineWindow.AlignToRtPrediction)
                {
                    Checked = SkylineWindow.AlignToRtPrediction,
                };
                items.Insert(iInsert++, menuItem);
            }
            if (null != chromFileInfoId && DocumentUI.Settings.HasResults &&
                !DocumentUI.Settings.DocumentRetentionTimes.FileAlignments.IsEmpty)
            {
                foreach (var chromatogramSet in DocumentUI.Settings.MeasuredResults.Chromatograms)
                {
                    var chromFileInfo = chromatogramSet.GetFileInfo(chromFileInfoId);
                    if (null == chromFileInfo)
                    {
                        continue;
                    }
                    string fileItemName = Path.GetFileNameWithoutExtension(SampleHelp.GetFileName(chromFileInfo.FilePath));
                    var menuItemText = string.Format(Resources.SkylineWindow_AlignTimesToFileFormat, fileItemName);
                    var alignToFileItem = new ToolStripMenuItem(menuItemText);
                    if (ReferenceEquals(chromFileInfoId, SkylineWindow.AlignToFile))
                    {
                        alignToFileItem.Click += (sender, eventArgs) => SkylineWindow.AlignToFile = null;
                        alignToFileItem.Checked = true;
                    }
                    else
                    {
                        alignToFileItem.Click += (sender, eventArgs) => SkylineWindow.AlignToFile = chromFileInfoId;
                        alignToFileItem.Checked = false;
                    }
                    items.Insert(iInsert++, alignToFileItem);
                }
            }
            return iInsert;
        }
        public void AddApplyRemovePeak(ToolStrip menuStrip, IsotopeLabelType labelType, int separator, ref int iInsert)
        {
            var removePeakItems = removePeakGraphMenuItem.DropDownItems;
            var document = DocumentUI;
            SkylineWindow.EditMenu.CanApplyOrRemovePeak(removePeakItems, labelType, out var canApply, out var canRemove);
            if (canApply || canRemove)
            {
                if (separator < 0)
                    menuStrip.Items.Insert(iInsert++, toolStripSeparator1);
                if (canApply)
                {
                    menuStrip.Items.Insert(iInsert++, applyPeakAllGraphMenuItem);
                    if (!document.GetSynchronizeIntegrationChromatogramSets().Any())
                    {
                        menuStrip.Items.Insert(iInsert++, applyPeakSubsequentGraphMenuItem);
                        if (ReplicateValue.GetGroupableReplicateValues(document).Any())
                        {
                            var groupBy = SkylineWindow.EditMenu.GetGroupApplyToDescription();
                            if (groupBy != null)
                            {
                                applyPeakGroupGraphMenuItem.Text = Resources.SkylineWindow_BuildChromatogramMenu_Apply_Peak_to_ + groupBy;
                                menuStrip.Items.Insert(iInsert++, applyPeakGroupGraphMenuItem);
                            }

                            SkylineWindow.EditMenu.AddGroupByMenuItems(menuStrip, groupApplyToByGraphMenuItem,
                                SkylineWindow.SetGroupApplyToBy, false, Settings.Default.GroupApplyToBy, ref iInsert);
                        }
                    }
                }
                if (canRemove)
                    menuStrip.Items.Insert(iInsert++, removePeakGraphMenuItem);
                if (separator > 0)
                    menuStrip.Items.Insert(iInsert++, toolStripSeparator1);
            }
        }


        public void AddTransitionContextMenu(ToolStrip menuStrip, int iInsert)
        {
            menuStrip.Items.Insert(iInsert, transitionsContextMenuItem);
            // Sometimes child menuitems are stripped from the parent
            if (transitionsContextMenuItem.DropDownItems.Count == 0)
            {
                transitionsContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                {
                    allTranContextMenuItem,
                    precursorsTranContextMenuItem,
                    productsTranContextMenuItem,
                    singleTranContextMenuItem,
                    totalTranContextMenuItem,
                    toolStripSeparatorTran,
                    basePeakContextMenuItem,
                    ticContextMenuItem,
                    qcContextMenuItem,
                    toolStripSeparatorOnlyQuantitative,
                    onlyQuantitativeContextMenuItem,
                    toolStripSeparatorSplitGraph,
                    splitGraphContextMenuItem,
                });
            }
        }
        #endregion

        #region Peaks

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
            SkylineWindow.EditMenu.CanApplyOrRemovePeak(null, null, out _, out var canRemove);
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
            SkylineWindow.EditMenu.RemovePeak(false);
        }

        private void synchronizeIntegrationContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.EditMenu.ShowSynchronizedIntegrationDialog();
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
        #endregion

        #region Hide And Show things
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
        #endregion

        #region Retention Times

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
        #endregion
        #region Transitions
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
                (displayType == DisplayTypeChrom.base_peak || displayType == DisplayTypeChrom.tic ||
                 displayType == DisplayTypeChrom.qc))
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
        #endregion
        #region Transform
        private void transformChromMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var transform = GraphChromatogram.Transform;

            transformChromNoneContextMenuItem.Checked = (transform == TransformChrom.raw);
            transformChromInterpolatedContextMenuItem.Checked = (transform == TransformChrom.interpolated);
            secondDerivativeContextMenuItem.Checked = (transform == TransformChrom.craw2d);
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

        private void smoothSGChromMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.SetTransformChrom(TransformChrom.savitzky_golay);
        }
        #endregion
        #region Auto Zoom
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
        #endregion
        private void chromPropsContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowChromatogramProperties();
        }
    }
}
