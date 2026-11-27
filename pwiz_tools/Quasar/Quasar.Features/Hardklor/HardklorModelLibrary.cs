/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on skyline-glimpse (https://github.com/maccoss/skyline-glimpse)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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
using System.Threading;

namespace pwiz.Quasar.Features.Hardklor
{
    /// <summary>
    /// Hardklor's averagine: the elemental composition of an average peptide of a given mass.
    /// </summary>
    /// <remarks>Ported from Hardklor (pwiz_tools/Skyline/Executables/Hardklor): CAveragine::calcAveragine with
    /// no averagine_mod variant, and the AVE_* constants and element masses of CAveragine.h / CPeriodicTable.</remarks>
    internal static class Averagine
    {
        internal const double AVE_MASS = 111.2137;
        internal const double AVE_C = 4.9558;
        internal const double AVE_H = 7.8241;
        internal const double AVE_N = 1.3571;
        internal const double AVE_O = 1.4716;
        internal const double AVE_S = 0.0390;

        // Hardklor's periodic table (CPeriodicTable defaults, Hardklor.dat).
        private const double MASS_C = 12.0;
        private const double MASS_N = 14.0030732;
        private const double MASS_O = 15.9949141;
        private const double MASS_S = 31.972070;

        /// <summary>The averagine composition of neutral mass <paramref name="mass"/>.</summary>
        public static ElementCounts Compute(double mass)
        {
            var units = mass / AVE_MASS;
            var c = (int)(AVE_C * units + 0.5);
            var o = (int)(AVE_O * units + 0.5);
            var n = (int)(AVE_N * units + 0.5);
            var s = (int)(AVE_S * units + 0.5);
            // Hydrogens make up the remaining mass, counted as 1 Da each (as Hardklor does).
            var h = (int)(mass - (c * MASS_C + n * MASS_N + o * MASS_O + s * MASS_S) + 0.5);
            return new ElementCounts(c, h, n, o, s);
        }
    }

    /// <summary>One model isotope envelope: a library entry for one charge and one 5 m/z bin.</summary>
    /// <remarks>Ported from Hardklor's mercuryModel (HardklorTypes.h).</remarks>
    internal sealed class EnvelopeModel
    {
        public static readonly EnvelopeModel EMPTY = new EnvelopeModel(new ElementCounts(), 0, Array.Empty<double>(), Array.Empty<float>(), 0);

        public EnvelopeModel(ElementCounts formula, double zeroMass, double[] mz, float[] intensity, float area)
        {
            Formula = formula;
            ZeroMass = zeroMass;
            Mz = mz;
            Intensity = intensity;
            Area = area;
        }

        /// <summary>The averagine composition the envelope models.</summary>
        public ElementCounts Formula { get; }

        /// <summary>Neutral monoisotopic mass of <see cref="Formula"/>.</summary>
        public double ZeroMass { get; }

        /// <summary>m/z of each isotope peak, ascending.</summary>
        public double[] Mz { get; }

        /// <summary>Relative abundance of each isotope peak, the most abundant = 100.</summary>
        public float[] Intensity { get; }

        /// <summary>Summed relative abundance / 100.</summary>
        public float Area { get; }

        public int Size => Mz.Length;
    }

    /// <summary>
    /// The model envelopes Hardklor matches: one per charge and 5 m/z bin, each the averagine distribution of the
    /// molecule whose m/z at that charge is the bin's lower edge. Entries are built on first use and shared by
    /// all threads.
    /// </summary>
    /// <remarks>Ported from Hardklor's CModelLibrary (buildLibrary/getModel), built lazily, with isotope
    /// distributions from <see cref="IEnvelopeModel"/> in place of Mercury.</remarks>
    internal sealed class HardklorModelLibrary
    {
        /// <summary>Hardklor's proton mass (CModelLibrary, CHardklor2).</summary>
        internal const double PROTON_MASS = 1.007276466;

        /// <summary>Isotope peaks below 1% of the most abundant are dropped (Mercury data &lt; 1.0 of 100).</summary>
        internal const double ABUNDANCE_CUTOFF = 0.01;

        /// <summary>Width of a library bin in m/z.</summary>
        internal const double BIN_WIDTH = 5;

        /// <summary>Neutral monoisotopic mass of a CHNOS composition (Mercury's zero mass).</summary>
        internal static double MonoisotopicMass(ElementCounts f) =>
            f.C * 12.0 + f.H * 1.00782503207 + f.N * 14.0030740048 + f.O * 15.99491461956 + f.S * 31.97207100;

        private readonly IEnvelopeModel _model;
        private readonly int _minCharge;
        private readonly EnvelopeModel[][] _models;

        public HardklorModelLibrary(IEnvelopeModel model, int minCharge, int maxCharge, double maxMoleculeMz)
        {
            _model = model;
            _minCharge = minCharge;
            BinCount = (int)(maxMoleculeMz / BIN_WIDTH) + 1;
            _models = new EnvelopeModel[maxCharge - minCharge + 1][];
            for (var i = 0; i < _models.Length; i++)
                _models[i] = new EnvelopeModel[BinCount];
        }

        /// <summary>Number of 5 m/z bins (CModelLibrary::setSize).</summary>
        public int BinCount { get; }

        /// <summary>
        /// The model for a peak at <paramref name="mz"/> with <paramref name="charge"/>, or null above the
        /// library's m/z range (where Hardklor stops with "Hard boundary encountered").
        /// </summary>
        public EnvelopeModel Get(int charge, double mz)
        {
            var bin = (int)(mz / BIN_WIDTH);
            if (bin < 0 || bin >= BinCount)
                return null;
            var row = _models[charge - _minCharge];
            var model = Volatile.Read(ref row[bin]);
            if (model == null)
            {
                var built = Build(charge, bin);
                model = Interlocked.CompareExchange(ref row[bin], built, null) ?? built;
            }
            return model;
        }

        internal EnvelopeModel Build(int charge, int bin)
        {
            // Hardklor leaves bin 0 empty.
            if (bin == 0)
                return EnvelopeModel.EMPTY;
            var mass = bin * (int)BIN_WIDTH * charge - PROTON_MASS * charge;
            var formula = Averagine.Compute(mass);
            var peaks = _model.Distribution(formula, ABUNDANCE_CUTOFF);
            var mz = new double[peaks.Count];
            var intensity = new float[peaks.Count];
            var area = 0f;
            for (var i = 0; i < peaks.Count; i++)
            {
                mz[i] = (peaks[i].Mass + PROTON_MASS * charge) / charge;
                intensity[i] = (float)(peaks[i].Abundance * 100);
                area += intensity[i];
            }
            area /= 100f;
            return new EnvelopeModel(formula, MonoisotopicMass(formula), mz, intensity, area);
        }
    }
}
