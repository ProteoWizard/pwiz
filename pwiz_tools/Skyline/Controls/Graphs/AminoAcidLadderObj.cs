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
        /// Observed peak intensity keyed by 1-based ordinal.
        /// Absent entries have no matched peak (drop line goes to x-axis).
        /// </summary>
        public IReadOnlyDictionary<int, double> PeakIntensities { get; }

        public IonSeriesData(IonType ionType, double[] boundaries,
            IReadOnlyDictionary<int, double> peakIntensities)
        {
            IonType = ionType;
            Color = IonTypeExtension.GetTypeColor(ionType);
            Boundaries = boundaries;
            PeakIntensities = peakIntensities;
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
        public AminoAcidLadderObj(
            string[] intervalLabels,
            double[] referenceBoundaries,
            IReadOnlyList<IonSeriesData> series,
            float yLine,
            float yLabel,
            float fontSize)
            : base(0.5, yLine, CoordType.XScaleYChartFraction)
        {
            IsClippedToChartRect = true;
            _intervalLabels = intervalLabels;
            _referenceBoundaries = referenceBoundaries;
            _series = series;
            _yLine = yLine;
            _yLabel = yLabel;
            _fontSize = fontSize;
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

            var savedClip = g.Clip;
            g.SetClip(chartRect, CombineMode.Intersect);

            // 1. Drop lines — light grey, all series
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
                        float bottomY;
                        if (series.PeakIntensities.TryGetValue(ordinal, out double intensity))
                            bottomY = graphPane.YAxis.Scale.Transform(intensity);
                        else
                            bottomY = chartRect.Bottom;
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

            // 4. Residue labels at reference interval midpoints — neutral grey
            using (var font  = new Font(FONT_FACE, _fontSize * scaleFactor))
            using (var brush = new SolidBrush(LABEL_COLOR))
            using (var fmt = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Far   // bottom of text at labelY
            })
            {
                for (int i = 0; i < _intervalLabels.Length; i++)
                {
                    float midX = (refScreenX[i] + refScreenX[i + 1]) / 2f;
                    g.DrawString(_intervalLabels[i], font, brush, midX, labelY, fmt);
                }
            }

            g.Clip = savedClip;
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
