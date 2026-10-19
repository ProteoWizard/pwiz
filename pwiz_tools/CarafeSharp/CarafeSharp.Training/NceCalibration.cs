/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Globalization;
using System.Linq;
using pwiz.CarafeSharp.Models;

namespace pwiz.CarafeSharp.Training
{
    /// <summary>
    /// The NCE a run trains and predicts with when its collision energy is not Thermo's
    /// normalized collision energy, which the MS2 model's NCE input is: an energy in eV, as
    /// Sciex, Bruker, Agilent and Waters report it. It is the NCE at which the start MS2 model
    /// predicts the run's own training spectra best, by median PCC over NCE 20 to 40, as
    /// AlphaPeptDeep calibrated the NCE of its SCIEX TripleTOF fine-tune.
    /// </summary>
    public sealed class NceCalibration
    {
        public const int MIN_NCE = 20;
        public const int MAX_NCE = 40;

        /// <summary>The most spectra a calibration scores, evenly spaced through the run's rows.</summary>
        public const int MAX_SPECTRA = 1000;

        /// <summary>
        /// Scores <paramref name="model"/> on <paramref name="rows"/> (at most
        /// <see cref="MAX_SPECTRA"/> of them) at every whole NCE from <see cref="MIN_NCE"/> to
        /// <see cref="MAX_NCE"/>, and takes the NCE with the highest median PCC; a tie takes the
        /// lower NCE.
        /// </summary>
        public static NceCalibration Calibrate(Ms2Model model, IReadOnlyList<Ms2TrainingExample> rows)
        {
            if (rows.Count == 0)
                throw new ArgumentException(@"An NCE calibration needs spectra to score.", nameof(rows));
            int step = (rows.Count + MAX_SPECTRA - 1) / MAX_SPECTRA;
            var sample = rows.Where((r, i) => i % step == 0).ToArray();
            var scores = new List<(int Nce, double MedianPcc)>();
            for (int nce = MIN_NCE; nce <= MAX_NCE; nce++)
            {
                var atNce = sample.Select(r => r.WithNce(nce)).ToArray();
                scores.Add((nce, Ms2Metrics.Evaluate(model, atNce).Pcc));
            }
            var best = scores.Where(s => !double.IsNaN(s.MedianPcc)).OrderByDescending(s => s.MedianPcc).ThenBy(s => s.Nce)
                .DefaultIfEmpty((Nce: MIN_NCE, MedianPcc: double.NaN)).First();
            return new NceCalibration(best.Nce, sample.Length, scores);
        }

        private NceCalibration(int nce, int spectra, IReadOnlyList<(int Nce, double MedianPcc)> scores)
        {
            Nce = nce;
            Spectra = spectra;
            Scores = scores;
        }

        /// <summary>The NCE with the highest median PCC.</summary>
        public int Nce { get; }

        /// <summary>How many spectra were scored.</summary>
        public int Spectra { get; }

        /// <summary>The median PCC at every NCE scored, in NCE order.</summary>
        public IReadOnlyList<(int Nce, double MedianPcc)> Scores { get; }

        /// <summary>The best NCE is an end of the range, so the best one may lie beyond it.</summary>
        public bool AtLimit
        {
            get { return Nce == MIN_NCE || Nce == MAX_NCE; }
        }

        public override string ToString()
        {
            double Score(int nce) => Scores.First(s => s.Nce == nce).MedianPcc;
            return string.Format(CultureInfo.InvariantCulture, @"NCE {0}, median PCC {1:F4} over {2} spectra (NCE {3}: {4:F4}, NCE {5}: {6:F4})",
                Nce, Score(Nce), Spectra, MIN_NCE, Score(MIN_NCE), MAX_NCE, Score(MAX_NCE));
        }
    }
}
