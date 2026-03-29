/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Drawing;
using System.Linq;
using pwiz.Skyline.Model;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Properties;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Box plot pane showing protein/peptide abundance distributions per replicate.
    /// Provides a distributional overview of the same data points shown in the
    /// Relative Abundance dot-plot.
    /// </summary>
    internal class AreaAbundanceComparisonGraphPane : SummaryGraphPane
    {
        private static readonly Color DEFAULT_BAR_COLOR = Color.LightGreen;

        public AreaAbundanceComparisonGraphPane(GraphSummary graphSummary) : base(graphSummary)
        {
            XAxis.Title.Text = GraphsResources.AreaAbundanceComparisonGraphPane_XAxis_Replicate;
            XAxis.Type = AxisType.Text;
            XAxis.Scale.FontSpec = GraphSummary.CreateFontSpec(Color.Black);
            XAxis.Scale.FontSpec.Angle = 90;
            XAxis.MinorTic.Size = 0;
            XAxis.MajorTic.IsOpposite = false;
            XAxis.MajorTic.Size = 2;

            YAxis.Title.Text = GraphsResources.AreaPeptideGraphPane_UpdateAxes_Peak_Area;
            YAxis.MajorTic.IsOpposite = false;

            X2Axis.IsVisible = false;
            Y2Axis.IsVisible = false;
            Legend.IsVisible = false;
            Border.IsVisible = false;
            Chart.Border.IsVisible = false;
            Title.IsVisible = false;
            IsFontsScaled = false;

            BarSettings.MinClusterGap = 3f;
            BarSettings.Type = BarType.Overlay;
        }

        public override void Draw(Graphics g)
        {
            if (!Settings.Default.RelativeAbundanceLogScale)
            {
                YAxis.Scale.Min = 0;
                AxisChange(g);
            }
            base.Draw(g);
        }

        public override void UpdateGraph(bool selectionChanged)
        {
            if (Program.MainWindow == null)
                return;

            var document = GraphSummary.DocumentUIContainer.DocumentUI;
            if (!document.Settings.HasResults)
                return;

            var measuredResults = document.Settings.MeasuredResults;
            if (measuredResults == null)
                return;

            bool isLogScale = Settings.Default.RelativeAbundanceLogScale;
            YAxis.Type = isLogScale ? AxisType.Log : AxisType.Linear;

            var useProteinLevel = Settings.Default.AreaProteinTargets;
            var replicateCount = measuredResults.Chromatograms.Count;
            var replicateValues = CollectReplicateAbundances(document, useProteinLevel, replicateCount);

            CurveList.Clear();
            var points = new PointPairList();
            var labels = new string[replicateCount];
            double yMin = double.MaxValue;
            double yMax = double.MinValue;

            for (int i = 0; i < replicateCount; i++)
            {
                labels[i] = measuredResults.Chromatograms[i].Name;
                var values = replicateValues[i];
                if (values.Count == 0)
                {
                    points.Add(new PointPair(i, PointPairBase.Missing));
                    continue;
                }

                // Filter out non-positive values that would break the log scale
                var positive = values.Where(v => v > 0).ToArray();
                if (positive.Length == 0)
                {
                    points.Add(new PointPair(i, PointPairBase.Missing));
                    continue;
                }

                // Always compute statistics in log space because abundance data is
                // log-normally distributed. This ensures symmetric outlier detection
                // regardless of the display axis mode.
                var sortedLog = positive.Select(Math.Log10).OrderBy(v => v).ToArray();
                var logTag = BoxPlotStatistics.ComputeBoxPlot(sortedLog);
                if (logTag == null)
                {
                    points.Add(new PointPair(i, PointPairBase.Missing));
                    continue;
                }

                // Convert log-space statistics back to raw values for rendering
                var tag = new BoxPlotTag(
                    Math.Pow(10, logTag.Q1),
                    Math.Pow(10, logTag.Median),
                    Math.Pow(10, logTag.Q3),
                    Math.Pow(10, logTag.Min),
                    Math.Pow(10, logTag.Max),
                    logTag.Outliers.Select(o => Math.Pow(10, o)).ToArray());

                // HiLowBarItem: Y = high (Q3), Z = low (Q1). Tag has median, whiskers, outliers.
                var pp = BoxPlotBarItem.MakePointPair(i, tag.Q3, tag.Q1,
                    tag.Median, tag.Max, tag.Min, tag.Outliers);
                points.Add(pp);

                double localMin = tag.Outliers.Length > 0 ? Math.Min(tag.Min, tag.Outliers.Min()) : tag.Min;
                double localMax = tag.Outliers.Length > 0 ? Math.Max(tag.Max, tag.Outliers.Max()) : tag.Max;
                yMin = Math.Min(yMin, localMin);
                yMax = Math.Max(yMax, localMax);
            }

            var bar = new BoxPlotBarItem(string.Empty, points, DEFAULT_BAR_COLOR, Color.Black);
            CurveList.Add(bar);
            XAxis.Scale.TextLabels = labels;

            if (yMin < double.MaxValue && yMax > double.MinValue)
            {
                YAxis.Scale.MinAuto = false;
                YAxis.Scale.MaxAuto = false;
                if (isLogScale)
                {
                    YAxis.Scale.Min = Math.Max(1, yMin / 2);
                    YAxis.Scale.Max = yMax * 2;
                }
                else
                {
                    double margin = yMax * 0.05;
                    YAxis.Scale.Min = 0;
                    YAxis.Scale.Max = yMax + margin;
                }
            }

            AxisChange();
        }

        private static List<double>[] CollectReplicateAbundances(SrmDocument document,
            bool useProteinLevel, int replicateCount)
        {
            var replicateValues = new List<double>[replicateCount];
            for (int i = 0; i < replicateCount; i++)
                replicateValues[i] = new List<double>();

            var dataSchema = new SkylineWindowDataSchema(Program.MainWindow);
            var moleculeGroups = document.MoleculeGroups.ToList();

            if (useProteinLevel)
            {
                foreach (var moleculeGroup in moleculeGroups)
                {
                    var path = new IdentityPath(IdentityPath.ROOT, moleculeGroup.PeptideGroup);
                    var protein = new Protein(dataSchema, path);
                    foreach (var kvp in protein.GetProteinAbundances())
                    {
                        if (kvp.Key >= 0 && kvp.Key < replicateCount)
                            replicateValues[kvp.Key].Add(kvp.Value.Raw);
                    }
                }
            }
            else
            {
                foreach (var moleculeGroup in moleculeGroups)
                {
                    var groupPath = new IdentityPath(IdentityPath.ROOT, moleculeGroup.PeptideGroup);
                    foreach (var peptideDocNode in moleculeGroup.Molecules)
                    {
                        var peptidePath = new IdentityPath(groupPath, peptideDocNode.Peptide);
                        var peptide = new Peptide(dataSchema, peptidePath);
                        foreach (var result in peptide.Results.Values)
                        {
                            var replicateIndex = result.ResultFile.Replicate.ReplicateIndex;
                            var area = result.GetQuantificationResult()?.NormalizedArea?.Raw;
                            if (area.HasValue && replicateIndex >= 0 && replicateIndex < replicateCount)
                                replicateValues[replicateIndex].Add(area.Value);
                        }
                    }
                }
            }

            return replicateValues;
        }

    }
}
