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
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// A ZedGraph GraphObj that draws a sequence ruler for one ion series (one ion type at one
    /// charge state) above the spectrum chart area.  Each residue label is placed at the midpoint
    /// of the m/z interval defined by the two flanking theoretical ion positions.
    ///
    /// The ruler spans from the ordinal-1 ion (e.g. b1 or y1) to the molecular ion mass at the
    /// same fragment charge.  The terminal residue that sits outside this range (N-terminal for
    /// b/a/c series, C-terminal for y/x/z series) is intentionally omitted because it has no
    /// well-defined inner boundary ion.
    ///
    /// At each fragment-ion boundary a vertical drop line is drawn from the ruler down to the top
    /// of the corresponding annotated peak, or all the way to the x-axis when no peak is present.
    ///
    /// Y coordinates use the XScaleYChartFraction convention: X is m/z data scale, Y is a
    /// fraction of the chart area height (0 = top, 1 = bottom).
    /// </summary>
    public class AminoAcidLadderObj : GraphObj
    {
        private const string FONT_FACE = @"Arial";
        private const float TICK_HALF_HEIGHT_PX = 4f;

        /// <summary>
        /// m/z boundaries indexed 0..nSeq:
        ///   [0..nSeq-1] = theoretical m/z for fragment ions at ordinals 1..nSeq
        ///   [nSeq]      = molecular-ion m/z (right-hand endpoint of the ruler)
        /// Defines nSeq intervals (one per residue shown).
        /// </summary>
        private readonly double[] _boundaries;

        /// <summary>
        /// Residue label for each of the nSeq intervals.
        /// For N-terminal series (b/a/c): label[i] = residue[i+1] (skip N-terminal residue).
        /// For C-terminal series (y/x/z): label[i] = residue[nSeq-i-1] (C→N order, skip C-terminal).
        /// </summary>
        private readonly string[] _intervalLabels;

        /// <summary>
        /// Peak intensity for each fragment-ion boundary (index = ordinal - 1).
        /// Key: 1-based ion ordinal.  Value: observed peak intensity.
        /// Boundaries without a matched peak are absent from this dictionary.
        /// </summary>
        private readonly IReadOnlyDictionary<int, double> _peakIntensities;

        private readonly Color _color;

        /// <summary>Y chart fraction (0=top, 1=bottom) for the horizontal ruler line.</summary>
        private readonly float _yLine;

        /// <summary>Y chart fraction for the bottom edge of residue labels (must be &lt; _yLine so labels appear above the line).</summary>
        private readonly float _yLabel;

        private readonly float _fontSize;

        /// <summary>
        /// Creates a sequence ruler for the given ion series.
        /// </summary>
        /// <param name="representativeIon">
        /// One matched fragment ion from the series.  Determines the ion type, charge, and
        /// which mass calculator to use.  The ion's predicted m/z is not used directly — all
        /// theoretical positions are (re-)computed from the peptide mass table.
        /// </param>
        /// <param name="peptideDocNode">Peptide, used for the sequence and explicit modifications.</param>
        /// <param name="transitionGroupDocNode">Transition group, used for the isotope label type.</param>
        /// <param name="settings">Document settings, used to obtain the fragment mass calculator.</param>
        /// <param name="peakIntensities">
        /// Observed peak intensities keyed by 1-based ion ordinal.  Boundaries whose ordinal is
        /// absent are treated as having no peak and receive a drop line to the x-axis.
        /// </param>
        /// <param name="yLine">Y chart fraction (0=top) for the ruler line.</param>
        /// <param name="yLabel">Y chart fraction for residue label bottom edge; must be less than yLine.</param>
        /// <param name="fontSize">Font size for residue labels.</param>
        public AminoAcidLadderObj(
            MatchedFragmentIon representativeIon,
            PeptideDocNode peptideDocNode,
            TransitionGroupDocNode transitionGroupDocNode,
            SrmSettings settings,
            IReadOnlyDictionary<int, double> peakIntensities,
            float yLine,
            float yLabel,
            float fontSize)
            : base(0.5, yLine, CoordType.XScaleYChartFraction)
        {
            IsClippedToChartRect = true;
            _yLine = yLine;
            _yLabel = yLabel;
            _fontSize = fontSize;
            _peakIntensities = peakIntensities;
            _color = IonTypeExtension.GetTypeColor(representativeIon.IonType);

            var ionType = representativeIon.IonType;
            int fragmentCharge = representativeIon.Charge.AdductCharge;

            // Fragment mass calculator for this label type and modifications
            var calc = settings.GetFragmentCalc(
                transitionGroupDocNode.TransitionGroup.LabelType,
                peptideDocNode.ExplicitMods);

            // Theoretical masses for every ordinal of every ion series in this peptide.
            // Use the plain peptide sequence (not ModifiedTarget) so the string contains only
            // amino-acid characters; the calc already carries modification masses via ExplicitMods.
            var plainTarget = peptideDocNode.Peptide.Target;
            var ionMasses = calc.GetFragmentIonMasses(plainTarget);
            // GetLength(1) == seq.Length - 1 == number of fragment-ion positions (one per cleavage site).
            // For an n-residue peptide this equals n-1.  Valid ordinals are 1..nSeq (= 1..n-1).
            int nSeq = ionMasses.GetLength(1);

            // Build the boundary array: nSeq+1 entries.
            //   boundaries[0..nSeq-1]  = ions at ordinals 1..nSeq  (b1..b(n-1) or y1..y(n-1))
            //   boundaries[nSeq]       = molecular-ion m/z (right-hand endpoint of the ruler)
            _boundaries = new double[nSeq + 1];
            for (int k = 1; k <= nSeq; k++)
            {
                var mass = ionMasses.GetIonValue(ionType, k);
                _boundaries[k - 1] = SequenceMassCalc.GetMZ(mass, fragmentCharge);
            }
            var precursorMass = calc.GetPrecursorFragmentMass(plainTarget);
            _boundaries[nSeq] = SequenceMassCalc.GetMZ(precursorMass, fragmentCharge);

            // Residue labels: nSeq labels for nSeq intervals.
            var allResidues = ParseModifiedSequenceResidues(peptideDocNode.ModifiedSequenceDisplay);
            _intervalLabels = new string[nSeq];

            if (ionType.IsNTerminal())
            {
                // b/a/c — sequence reads N→C left-to-right; skip the N-terminal residue
                // (it sits before ion[1] with no left boundary).
                // Interval i spans [b_{i+1}, b_{i+2}], representing residue at position i+1.
                for (int i = 0; i < nSeq; i++)
                    _intervalLabels[i] = i + 1 < allResidues.Length ? allResidues[i + 1] : string.Empty;
            }
            else
            {
                // y/x/z — sequence reads C→N left-to-right on the m/z axis; skip the C-terminal
                // residue (it sits before y[1] with no left boundary).
                // Interval i spans [y_{i+1}, y_{i+2}], representing the residue at position
                // nSeq-i-1 from the N-terminus (i.e., counting inward from the C-terminus).
                for (int i = 0; i < nSeq; i++)
                {
                    int residueIndex = nSeq - i - 1;
                    _intervalLabels[i] = residueIndex >= 0 && residueIndex < allResidues.Length
                        ? allResidues[residueIndex]
                        : string.Empty;
                }
            }
        }

        public override void Draw(Graphics g, PaneBase pane, float scaleFactor)
        {
            var graphPane = pane as GraphPane;
            if (graphPane == null || _boundaries.Length < 2)
                return;

            var chartRect = graphPane.Chart.Rect;
            if (chartRect.Width <= 0 || chartRect.Height <= 0)
                return;

            float lineY = chartRect.Top + _yLine * chartRect.Height;
            float labelY = chartRect.Top + _yLabel * chartRect.Height;

            // Convert m/z boundaries to screen X pixels
            var screenX = new float[_boundaries.Length];
            for (int k = 0; k < _boundaries.Length; k++)
                screenX[k] = (float)graphPane.XAxis.Scale.Transform(_boundaries[k]);

            float xStart = screenX[0];
            float xEnd = screenX[_boundaries.Length - 1];

            // Clip drawing to chart rect
            var savedClip = g.Clip;
            g.SetClip(chartRect, System.Drawing.Drawing2D.CombineMode.Intersect);

            // Vertical drop lines in light grey (lighter than unannotated peaks which use Color.Gray)
            using (var dropPen = new Pen(Color.LightGray, 1f))
            {
                int fragmentCount = _boundaries.Length - 1; // = nSeq
                for (int k = 0; k < fragmentCount; k++)
                {
                    int ordinal = k + 1; // 1-based ion ordinal
                    float bottomY;
                    double intensity;
                    if (_peakIntensities.TryGetValue(ordinal, out intensity))
                        bottomY = (float)graphPane.YAxis.Scale.Transform(intensity);
                    else
                        bottomY = chartRect.Bottom;

                    g.DrawLine(dropPen, screenX[k], lineY, screenX[k], bottomY);
                }
            }

            using (var pen = new Pen(_color, 1.5f))
            {
                // Horizontal ruler line (drawn after drop lines so it sits on top)
                g.DrawLine(pen, xStart, lineY, xEnd, lineY);

                // Tick marks: end ticks at both endpoints, inner ticks between intervals
                pen.Width = 1f;
                g.DrawLine(pen, xStart, lineY - TICK_HALF_HEIGHT_PX, xStart, lineY + TICK_HALF_HEIGHT_PX);
                g.DrawLine(pen, xEnd,   lineY - TICK_HALF_HEIGHT_PX, xEnd,   lineY + TICK_HALF_HEIGHT_PX);
                for (int k = 1; k < _boundaries.Length - 1; k++)
                {
                    g.DrawLine(pen, screenX[k], lineY - TICK_HALF_HEIGHT_PX,
                        screenX[k], lineY + TICK_HALF_HEIGHT_PX);
                }
            }

            // Residue labels at interval midpoints
            using (var font = new Font(FONT_FACE, _fontSize * scaleFactor))
            using (var brush = new SolidBrush(_color))
            {
                var fmt = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Far   // bottom of text at labelY
                };
                for (int i = 0; i < _intervalLabels.Length; i++)
                {
                    float midX = (screenX[i] + screenX[i + 1]) / 2f;
                    g.DrawString(_intervalLabels[i], font, brush, midX, labelY, fmt);
                }
            }

            g.Clip = savedClip;
        }

        /// <summary>
        /// The ruler is a pure visual overlay; it should never intercept hit-testing so that
        /// peak labels (TextObj) beneath it remain reachable by FindNearestObject.
        /// </summary>
        public override bool PointInBox(System.Drawing.PointF pt, PaneBase pane,
            System.Drawing.Graphics g, float scaleFactor) => false;

        public override void GetCoords(PaneBase pane, Graphics g, float scaleFactor,
            out string shape, out string coords)
        {
            shape = string.Empty;
            coords = string.Empty;
        }

        /// <summary>
        /// Splits a modified sequence display string (e.g. "PEPTM[+16.0]IDE") into per-residue
        /// tokens, preserving bracket-notation modifications on each amino acid.
        /// </summary>
        private static string[] ParseModifiedSequenceResidues(string modifiedSequence)
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
