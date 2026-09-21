/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
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
using System.Drawing.Drawing2D;
using System.Linq;
using pwiz.Skyline.Model;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Holds drawing data for a single ion series within a ruler group.
    /// </summary>
    public class IonSeriesData
    {
        public IonType IonType { get; }
        public Color Color { get; }

        /// <summary>
        /// m/z boundaries: indices 0..nSeq-1 are ions at ordinals 1..nSeq;
        /// index nSeq is the molecular-ion endpoint.  Length = nSeq + 1.
        /// </summary>
        public double[] Boundaries { get; }

        /// <summary>
        /// Observed peak intensity in the main spectrum keyed by 1-based ordinal.
        /// Absent entries have no matched peak in the main panel.
        /// </summary>
        public IReadOnlyDictionary<int, double> PeakIntensities { get; }

        /// <summary>
        /// Observed peak intensity in the mirror spectrum (positive values; the mirror
        /// panel renders them inverted) keyed by 1-based ordinal. Null when there is no
        /// mirror panel; absent entries have no matched peak in the mirror.
        /// </summary>
        public IReadOnlyDictionary<int, double> MirrorPeakIntensities { get; }

        public IonSeriesData(IonType ionType, double[] boundaries,
            IReadOnlyDictionary<int, double> peakIntensities,
            IReadOnlyDictionary<int, double> mirrorPeakIntensities = null)
        {
            IonType = ionType;
            Color = IonTypeExtension.GetTypeColor(ionType);
            Boundaries = boundaries;
            PeakIntensities = peakIntensities;
            MirrorPeakIntensities = mirrorPeakIntensities;
        }
    }

    /// <summary>
    /// A ZedGraph GraphObj that draws a sequence ruler for a group of ion series sharing
    /// the same N/C-terminal direction and charge state above the spectrum chart area.
    ///
    /// Layout:
    ///   - One neutral-grey horizontal ruler line spanning the full m/z range of the group.
    ///   - Ticks at every fragment-ion boundary for each series, colored by ion type.
    ///   - Light-grey vertical drop lines from each tick down to the matched peak top, or
    ///     all the way to the x-axis when no peak is present at that ordinal.
    ///   - Residue labels at the midpoints of the reference-series (b or y) intervals,
    ///     drawn in a neutral grey so they are not confused with ion-type colors.
    ///
    /// Y coordinates use the XScaleYChartFraction convention (0 = top, 1 = bottom).
    /// </summary>
    public class AminoAcidLadderObj : GraphObj
    {
        internal const string FONT_FACE = @"Arial";
        private const float TICK_HALF_HEIGHT_PX = 4f;
        // Horizontal gap kept between the chart's left edge and the group label so the
        // label doesn't overlap the Y-axis ticks when the ruler extends offscreen left.
        private const float GROUP_LABEL_LEFT_MARGIN_PX = 4f;
        private static readonly Color RULER_LINE_COLOR = Color.DarkGray;
        private static readonly Color LABEL_COLOR = Color.DimGray;

        /// <summary>Residue label for each of the nSeq intervals (from reference series).</summary>
        private readonly string[] _intervalLabels;

        /// <summary>
        /// m/z boundaries from the reference ion series (b for N-terminal, y for C-terminal).
        /// Midpoints of consecutive pairs define the residue label x-positions.
        /// Length = nSeq + 1.
        /// </summary>
        private readonly double[] _referenceBoundaries;

        /// <summary>One or more ion series sharing this ruler group.</summary>
        private readonly IReadOnlyList<IonSeriesData> _series;

        private readonly float _yLine;
        private readonly float _yLabel;
        private readonly float _fontSize;
        private readonly int _charge;
        private readonly string _lossText;

        /// <summary>
        /// Creates a grouped sequence ruler.
        /// </summary>
        /// <param name="intervalLabels">
        /// Residue label for each of the nSeq intervals, computed from the reference ion type.
        /// </param>
        /// <param name="referenceBoundaries">
        /// m/z boundaries from the reference series (b for N-terminal, y for C-terminal).
        /// Used only to position the residue labels.  Length = nSeq + 1.
        /// </param>
        /// <param name="series">
        /// One or more ion series in the group.  Each contributes its own ticks and drop lines.
        /// </param>
        /// <param name="yLine">Y chart fraction (0=top) for the ruler line.</param>
        /// <param name="yLabel">Y chart fraction for the residue label bottom edge.</param>
        /// <param name="fontSize">Font size for residue labels.</param>
        /// <param name="charge">Adduct charge shared by all series in the group; rendered
        /// as "++"/"+++" etc. in the group label when greater than 1.</param>
        /// <param name="lossText">Neutral-loss display string for the group (e.g. "-18"),
        /// rendered between the ion-type letter(s) and the charge suffix. Empty for non-loss.</param>
        public AminoAcidLadderObj(
            string[] intervalLabels,
            double[] referenceBoundaries,
            IReadOnlyList<IonSeriesData> series,
            float yLine,
            float yLabel,
            float fontSize,
            int charge,
            string lossText)
            : base(0.5, yLine, CoordType.XScaleYChartFraction)
        {
            IsClippedToChartRect = true;
            _intervalLabels = intervalLabels;
            _referenceBoundaries = referenceBoundaries;
            _series = series;
            _yLine = yLine;
            _yLabel = yLabel;
            _fontSize = fontSize;
            _charge = charge;
            _lossText = lossText ?? string.Empty;
        }

        /// <summary>
        /// The ruler is a pure visual overlay; it must never intercept hit-testing so that
        /// peak labels (TextObj) beneath it remain reachable by FindNearestObject.
        /// </summary>
        public override bool PointInBox(PointF pt, PaneBase pane, Graphics g, float scaleFactor) => false;

        public override void Draw(Graphics g, PaneBase pane, float scaleFactor)
        {
            var graphPane = pane as GraphPane;
            if (graphPane == null || _series.Count == 0 || _referenceBoundaries.Length < 2)
                return;

            var chartRect = graphPane.Chart.Rect;
            if (chartRect.Width <= 0 || chartRect.Height <= 0)
                return;

            float lineY  = chartRect.Top + _yLine  * chartRect.Height;
            float labelY = chartRect.Top + _yLabel * chartRect.Height;

            // Residue labels are drawn upward from labelY (StringAlignment.Far), so the
            // topmost ruler's labels would clip above the chart when the pane is short
            // (the fixed top fraction maps to fewer pixels than the label height). Reserve
            // at least one label-height of space above the line by shifting the whole ruler
            // (line, ticks, labels, drop lines) down by any shortfall.
            using (var measureFont = new Font(FONT_FACE, _fontSize * scaleFactor))
            {
                float minLabelY = chartRect.Top + measureFont.GetHeight(g);
                if (labelY < minLabelY)
                {
                    float shift = minLabelY - labelY;
                    labelY += shift;
                    lineY  += shift;
                }
            }

            // Reference boundaries → screen X for label midpoints
            var refScreenX = _referenceBoundaries
                .Select(b => graphPane.XAxis.Scale.Transform(b))
                .ToArray();

            // Per-series screen X arrays
            var seriesScreenX = _series
                .Select(s => s.Boundaries
                    .Select(b => graphPane.XAxis.Scale.Transform(b))
                    .ToArray())
                .ToList();

            // Overall ruler span: min of first boundaries, max of last boundaries
            float xStart = seriesScreenX.Min(sx => sx[0]);
            float xEnd   = seriesScreenX.Max(sx => sx[sx.Length - 1]);

            // Use Save()/Restore() (rather than saving g.Clip directly) so the cloned
            // Region's native GDI handle doesn't leak when Draw is called repeatedly.
            var gState = g.Save();
            g.SetClip(chartRect, CombineMode.Intersect);

            // 1. Drop lines — light grey, all series.
            // Each drop line ends at the topmost (smallest screen Y) of: the matched main
            // peak top, the matched mirror peak top (its top is at -intensity since the
            // mirror panel is inverted), and the x-axis. In a single (non-mirror) spectrum
            // this is the main peak top if matched, else the x-axis. In mirror mode, a
            // peak matched only in the mirror still leaves the drop line at the x-axis
            // since the mirror peak sits below it.
            float xAxisY = graphPane.YAxis.Scale.Transform(0);
            using (var dropPen = new Pen(Color.LightGray, 1f))
            {
                for (int si = 0; si < _series.Count; si++)
                {
                    var series  = _series[si];
                    var screenX = seriesScreenX[si];
                    int fragmentCount = series.Boundaries.Length - 1; // = nSeq
                    for (int k = 0; k < fragmentCount; k++)
                    {
                        int ordinal = k + 1;
                        float bottomY = xAxisY;
                        if (series.PeakIntensities.TryGetValue(ordinal, out double mainIntensity))
                        {
                            float yMain = graphPane.YAxis.Scale.Transform(mainIntensity);
                            if (yMain < bottomY) bottomY = yMain;
                        }
                        if (series.MirrorPeakIntensities != null &&
                            series.MirrorPeakIntensities.TryGetValue(ordinal, out double mirrorIntensity))
                        {
                            float yMirror = graphPane.YAxis.Scale.Transform(-mirrorIntensity);
                            if (yMirror < bottomY) bottomY = yMirror;
                        }
                        g.DrawLine(dropPen, screenX[k], lineY, screenX[k], bottomY);
                    }
                }
            }

            // 2. Horizontal ruler line — neutral grey
            using (var rulerPen = new Pen(RULER_LINE_COLOR, 1.5f))
                g.DrawLine(rulerPen, xStart, lineY, xEnd, lineY);

            // 3. Ticks — per series, colored by ion type
            for (int si = 0; si < _series.Count; si++)
            {
                var screenX = seriesScreenX[si];
                using (var tickPen = new Pen(_series[si].Color, 1f))
                {
                    foreach (float x in screenX)
                        g.DrawLine(tickPen, x, lineY - TICK_HALF_HEIGHT_PX,
                                            x, lineY + TICK_HALF_HEIGHT_PX);
                }
            }

            // 4. Residue labels at reference interval midpoints — neutral grey.
            // Properties are assigned inside the using body so an exception in a setter
            // would still dispose the StringFormat (it's already captured by `using`).
            using (var font  = new Font(FONT_FACE, _fontSize * scaleFactor))
            using (var brush = new SolidBrush(LABEL_COLOR))
            using (var fmt = new StringFormat())
            {
                fmt.Alignment = StringAlignment.Center;
                fmt.LineAlignment = StringAlignment.Far;   // bottom of text at labelY
                for (int i = 0; i < _intervalLabels.Length; i++)
                {
                    float midX = (refScreenX[i] + refScreenX[i + 1]) / 2f;
                    g.DrawString(_intervalLabels[i], font, brush, midX, labelY, fmt);
                }
            }

            // 5. Group label: ion-type letters (each in its type color) + charge suffix.
            // Drawn last so it renders on top of any residue label it might overlap when the
            // ruler extends offscreen left and the label has to be clamped to the chart edge.
            DrawGroupLabel(g, scaleFactor, chartRect, xStart, xEnd, labelY);

            g.Restore(gState);
        }

        private void DrawGroupLabel(Graphics g, float scaleFactor, RectangleF chartRect,
            float xStart, float xEnd, float labelY)
        {
            // Suppress the label when the ruler is entirely outside the visible chart
            // area (zoomed/panned away). The ticks and drop lines are already clipped
            // to chartRect, but the label is clamped to the chart's left edge — so it
            // would otherwise appear with no visible ruler beside it.
            if (xEnd < chartRect.Left || xStart > chartRect.Right)
                return;

            var ionTokens = _series
                .Select(s => (s.Color, Text: s.IonType.GetLocalizedString()))
                .ToList();
            string chargeStr = _charge > 1 ? new string('+', _charge) : string.Empty;
            // Loss text uses the ion-type color of the (single) series in a loss group,
            // so "y-18" reads as one visual token. Falls back to the first series' color
            // if a loss group ever ends up with more than one ion type (defensive).
            Color lossColor = ionTokens.Count > 0 ? ionTokens[0].Color : LABEL_COLOR;

            using (var font = new Font(FONT_FACE, _fontSize * scaleFactor))
            using (var fmt = new StringFormat())
            {
                fmt.Alignment     = StringAlignment.Near;
                fmt.LineAlignment = StringAlignment.Far;  // text bottom at labelY

                float totalWidth = ionTokens.Sum(t => g.MeasureString(t.Text, font).Width);
                if (_lossText.Length > 0)
                    totalWidth += g.MeasureString(_lossText, font).Width;
                if (chargeStr.Length > 0)
                    totalWidth += g.MeasureString(chargeStr, font).Width;

                // Anchor the label's right edge just left of the first tick; clamp the
                // left edge to the chart's left edge (plus a small margin to clear the
                // Y-axis ticks) so the label stays fully on-chart.
                float labelLeft = Math.Max(xStart - totalWidth,
                    chartRect.Left + GROUP_LABEL_LEFT_MARGIN_PX);

                float curX = labelLeft;
                foreach (var (color, text) in ionTokens)
                {
                    using (var brush = new SolidBrush(color))
                        g.DrawString(text, font, brush, curX, labelY, fmt);
                    curX += g.MeasureString(text, font).Width;
                }
                if (_lossText.Length > 0)
                {
                    using (var brush = new SolidBrush(lossColor))
                        g.DrawString(_lossText, font, brush, curX, labelY, fmt);
                    curX += g.MeasureString(_lossText, font).Width;
                }
                if (chargeStr.Length > 0)
                {
                    using (var brush = new SolidBrush(LABEL_COLOR))
                        g.DrawString(chargeStr, font, brush, curX, labelY, fmt);
                }
            }
        }

        public override void GetCoords(PaneBase pane, Graphics g, float scaleFactor,
            out string shape, out string coords)
        {
            shape  = string.Empty;
            coords = string.Empty;
        }

        /// <summary>
        /// Splits a modified sequence display string (e.g. "PEPTM[+16.0]IDE") into
        /// per-residue tokens, preserving bracket-notation modifications.
        /// </summary>
        internal static string[] ParseModifiedSequenceResidues(string modifiedSequence)
        {
            var residues = new List<string>();
            int i = 0;
            while (i < modifiedSequence.Length)
            {
                if (!char.IsUpper(modifiedSequence[i]))
                {
                    i++;
                    continue;
                }
                int start = i++;
                if (i < modifiedSequence.Length && modifiedSequence[i] == '[')
                {
                    while (i < modifiedSequence.Length && modifiedSequence[i] != ']')
                        i++;
                    if (i < modifiedSequence.Length)
                        i++;
                }
                residues.Add(modifiedSequence.Substring(start, i - start));
            }
            return residues.ToArray();
        }
    }
}
