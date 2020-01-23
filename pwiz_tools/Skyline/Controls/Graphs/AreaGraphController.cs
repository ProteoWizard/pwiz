/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    public enum AreaNormalizeToView{ area_percent_view, area_maximum_view, area_ratio_view, area_global_standard_view, none }

    public enum AreaScope{ document, protein }

    public enum PointsTypePeakArea { targets, decoys }

    public enum AreaCVNormalizationMethod { global_standards, medians, none, ratio }

    public enum AreaCVTransitions { all, best, count }

    public enum AreaCVMsLevel { precursors, products }

    public static class AreCVMsLevelExtension
    {
        private static string[] LOCALIZED_VALUES
        {
            get
            {
                return new[]
                {
                    Resources.RefineDlg_RefineDlg_Precursors,
                    Resources.RefineDlg_RefineDlg_Products
                };
            }
        }
        public static string GetLocalizedString(this AreaCVMsLevel val)
        {
            return LOCALIZED_VALUES[(int)val];
        }

        public static AreaCVMsLevel GetEnum(string enumValue)
        {
            return Helpers.EnumFromLocalizedString<AreaCVMsLevel>(enumValue, LOCALIZED_VALUES);
        }

    }

    public enum AreaGraphDisplayType { bars, lines }

    public sealed class AreaGraphController : GraphSummary.IControllerSplit
    {
        public static GraphTypeSummary GraphType
        {
            get { return Helpers.ParseEnum(Settings.Default.AreaGraphType, GraphTypeSummary.replicate); }
            set { Settings.Default.AreaGraphType = value.ToString(); }
        }

        public static AreaGraphDisplayType GraphDisplayType
        {
            get { return Helpers.ParseEnum(Settings.Default.AreaGraphDisplayType, AreaGraphDisplayType.bars); }
            set { Settings.Default.AreaGraphDisplayType = value.ToString(); }
        }

        public static AreaNormalizeToView AreaView
        {
            get { return Helpers.ParseEnum(Settings.Default.AreaNormalizeToView, AreaNormalizeToView.none); }
            set { Settings.Default.AreaNormalizeToView = value.ToString(); }
        }

        public static AreaScope AreaScope
        {
            get { return Helpers.ParseEnum(Settings.Default.PeakAreaScope, AreaScope.document); }
            set { Settings.Default.PeakAreaScope = value.ToString(); }
        }

        public static PointsTypePeakArea PointsType
        {
            get { return Helpers.ParseEnum(Settings.Default.AreaCVPointsType, PointsTypePeakArea.targets); }
            set { Settings.Default.AreaCVPointsType = value.ToString(); }
        }

        public static AreaCVNormalizationMethod NormalizationMethod
        {
            get { return Helpers.ParseEnum(Settings.Default.AreaCVNormalizationMethod, AreaCVNormalizationMethod.none); }
            set { Settings.Default.AreaCVNormalizationMethod = value.ToString(); }
        }

        public static AreaCVTransitions AreaCVTransitions
        {
            get { return Helpers.ParseEnum(Settings.Default.AreaCVTransitions, AreaCVTransitions.all); }
            set { Settings.Default.AreaCVTransitions = value.ToString(); }
        }

        public static AreaCVMsLevel AreaCVMsLevel
        {
            get { return Helpers.ParseEnum(Settings.Default.AreaCVMsLevel, AreaCVMsLevel.products); }
            set { Settings.Default.AreaCVMsLevel = value.ToString(); }
        }

        public static int AreaCVRatioIndex
        {
            get { return Settings.Default.AreaCVRatioIndex; }
            set { Settings.Default.AreaCVRatioIndex = value; }
        }

        public static string GroupByGroup { get; set; }
        public static string GroupByAnnotation { get; set; }

        public static int MinimumDetections = 2;

        public static int AreaCVTransitionsCount { get; set; }

        public GraphSummary GraphSummary { get; set; }

        UniqueList<GraphTypeSummary> GraphSummary.IController.GraphTypes
        {
            get { return Settings.Default.AreaGraphTypes; }
            set { Settings.Default.AreaGraphTypes = value; }
        }

        public IFormView FormView { get { return new GraphSummary.AreaGraphView(); } }

        public static double GetAreaCVFactorToDecimal()
        {
            return Settings.Default.AreaCVShowDecimals ? 1.0 : 100.0;
        }

        public static double GetAreaCVFactorToPercentage()
        {
            return Settings.Default.AreaCVShowDecimals ? 100.0 : 1.0;
        }

        public static bool ShouldUseQValues(SrmDocument document)
        {
            return PointsType == PointsTypePeakArea.targets &&
                document.Settings.PeptideSettings.Integration.PeakScoringModel.IsTrained &&
                !double.IsNaN(Settings.Default.AreaCVQValueCutoff) &&
                Settings.Default.AreaCVQValueCutoff < 1.0;
        }

        public void OnDocumentChanged(SrmDocument oldDocument, SrmDocument newDocument)
        {
            var settingsNew = newDocument.Settings;
            var settingsOld = oldDocument.Settings;

            if (GraphSummary.Type == GraphTypeSummary.histogram || GraphSummary.Type == GraphTypeSummary.histogram2d)
            {
                if (GroupByGroup != null && !ReferenceEquals(settingsNew.DataSettings.AnnotationDefs, settingsOld.DataSettings.AnnotationDefs))
                {
                    var groups = AnnotationHelper.FindGroupsByTarget(settingsNew, AnnotationDef.AnnotationTarget.replicate);
                    // The group we were grouping by has been removed
                    if (!groups.Contains(GroupByGroup))
                    {
                        GroupByGroup = GroupByAnnotation = null;
                    }
                }

                if (GroupByAnnotation != null && settingsNew.HasResults && settingsOld.HasResults &&
                    !ReferenceEquals(settingsNew.MeasuredResults.Chromatograms, settingsOld.MeasuredResults.Chromatograms))
                {
                    var annotations = AnnotationHelper.GetPossibleAnnotations(settingsNew, GroupByGroup, AnnotationDef.AnnotationTarget.replicate);

                    // The annotation we were grouping by has been removed
                    if (!annotations.Contains(GroupByAnnotation))
                        GroupByAnnotation = null;

                    var paneInfo = GraphSummary.GraphPanes.FirstOrDefault() as IAreaCVHistogramInfo;
                    if(paneInfo != null)
                        paneInfo.Cache.Cancel();
                }
            }
        }

        public void OnActiveLibraryChanged()
        {
            if (GraphSummary.GraphPanes.OfType<AreaReplicateGraphPane>().Any())
                GraphSummary.UpdateUI();
        }

        public void OnResultsIndexChanged()
        {
            if (GraphSummary.GraphPanes.OfType<AreaReplicateGraphPane>().Any() /* || !Settings.Default.AreaAverageReplicates */ ||
                    RTLinearRegressionGraphPane.ShowReplicate == ReplicateDisplay.single)
                GraphSummary.UpdateUI();
        }

        public void OnRatioIndexChanged()
        {
            if (GraphSummary.GraphPanes.OfType<AreaReplicateGraphPane>().Any() /* || !Settings.Default.AreaAverageReplicates */)
                GraphSummary.UpdateUI();
        }

        public void OnUpdateGraph()
        {
            // CONSIDER: Need a better guarantee that this ratio index matches the
            //           one in the sequence tree, but at least this will keep the UI
            //           from crashing with IndexOutOfBoundsException.
            var settings = GraphSummary.DocumentUIContainer.DocumentUI.Settings;
            var mods = settings.PeptideSettings.Modifications;
            GraphSummary.RatioIndex = Math.Min(GraphSummary.RatioIndex, mods.RatioInternalStandardTypes.Count - 1);

            // Only show ratios if document changes to have valid ratios
            if (AreaView == AreaNormalizeToView.area_ratio_view && !mods.HasHeavyModifications)
                AreaView = AreaNormalizeToView.none;

            // Only show ratios if type and info match
            if (NormalizationMethod == AreaCVNormalizationMethod.ratio && !mods.HasHeavyModifications ||
                NormalizationMethod == AreaCVNormalizationMethod.global_standards && !settings.HasGlobalStandardArea)
            {
                NormalizationMethod = AreaCVNormalizationMethod.none;
            }

            AreaCVRatioIndex = Math.Min(AreaCVRatioIndex, mods.RatioInternalStandardTypes.Count - 1);

            var globalStandards = NormalizationMethod == AreaCVNormalizationMethod.global_standards;
            if (globalStandards && !GraphSummary.DocumentUIContainer.DocumentUI.Settings.HasGlobalStandardArea)
                NormalizationMethod = AreaCVNormalizationMethod.none;

            var pane = GraphSummary.GraphPanes.FirstOrDefault();

            switch (GraphSummary.Type)
            {
                case GraphTypeSummary.replicate:
                case GraphTypeSummary.peptide:
                    GraphSummary.DoUpdateGraph(this, GraphSummary.Type);
                    break;
                case GraphTypeSummary.histogram:
                    if (!(pane is AreaCVHistogramGraphPane))
                        GraphSummary.GraphPanes = new[] { new AreaCVHistogramGraphPane(GraphSummary) };
                    break;
                case GraphTypeSummary.histogram2d:
                    if (!(pane is AreaCVHistogram2DGraphPane))
                        GraphSummary.GraphPanes = new[] { new AreaCVHistogram2DGraphPane(GraphSummary)  };
                    break;
            }

            if (!ReferenceEquals(GraphSummary.GraphPanes.FirstOrDefault(), pane))
            {
                var disposable = pane as IDisposable;
                if (disposable != null)
                    disposable.Dispose();
            }
        }

        public bool IsReplicatePane(SummaryGraphPane pane)
        {
            return pane is AreaReplicateGraphPane;
        }

        public bool IsPeptidePane(SummaryGraphPane pane)
        {
            return pane is AreaPeptideGraphPane;
        }

        public SummaryGraphPane CreateReplicatePane(PaneKey key)
        {
            return new AreaReplicateGraphPane(GraphSummary, key);
        }

        public SummaryGraphPane CreatePeptidePane(PaneKey key)
        {
            return new AreaPeptideGraphPane(GraphSummary, key);
        }

        public bool HandleKeyDownEvent(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
//                case Keys.D3:
//                    if (e.Alt)
//                        GraphSummary.Hide();
//                    break;
                case Keys.F7:
                    if (!e.Alt && !(e.Shift && e.Control))
                    {
                        var type = e.Control
                            ? GraphTypeSummary.peptide
                            : GraphTypeSummary.replicate;
                        Settings.Default.AreaGraphTypes.Insert(0, type);

                        Program.MainWindow.ShowGraphPeakArea(true, type);
                        return true;
                    }
                    break;
            }
            return false;
        }

        public string Text
        {
            get { return Resources.SkylineWindow_CreateGraphPeakArea_Peak_Areas; }
        }
    }
}

