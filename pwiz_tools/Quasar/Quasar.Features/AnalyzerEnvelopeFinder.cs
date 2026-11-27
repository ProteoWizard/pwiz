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
using System.Collections.Concurrent;
using System.Collections.Generic;
using pwiz.Quasar.Features.Data;
using pwiz.Quasar.Features.Hardklor;

namespace pwiz.Quasar.Features
{
    /// <summary>
    /// Hardklor with the peak width of the analyzer that recorded each scan: one run can mix them (an Orbitrap
    /// Astral's MS1 in the Orbitrap, MS2 in the Astral). Ion trap scans (a Stellar's, a Tribrid's linear ion trap)
    /// are not searched: their resolution cannot separate the isotopes of multiply charged ions.
    /// </summary>
    public sealed class AnalyzerEnvelopeFinder : IEnvelopeFinder
    {
        /// <summary>
        /// Hardklor's settings for <paramref name="analyzer"/>: Skyline's (Orbitrap model, 60,000 at m/z 400) for an
        /// Orbitrap or an analyzer this does not know; the Astral at a constant 80,000 (time-of-flight model);
        /// none for an ion trap.
        /// </summary>
        public static HardklorSettings SettingsFor(MassAnalyzer analyzer) => SettingsFor(analyzer, HardklorSettings.Default);

        /// <summary>
        /// <paramref name="search"/> with the peak width of <paramref name="analyzer"/>; null for an ion trap.
        /// </summary>
        public static HardklorSettings SettingsFor(MassAnalyzer analyzer, HardklorSettings search) => analyzer switch
        {
            MassAnalyzer.astral => search with { Instrument = HardklorInstrument.tof, Resolution = 80000 },
            MassAnalyzer.ion_trap => null,
            _ => search with { Instrument = HardklorInstrument.orbitrap, Resolution = HardklorSettings.Default.Resolution },
        };

        private readonly IEnvelopeModel _model;
        private readonly ConcurrentDictionary<MassAnalyzer, HardklorFinder> _finders = new ConcurrentDictionary<MassAnalyzer, HardklorFinder>();

        public AnalyzerEnvelopeFinder(IEnvelopeModel model) : this(model, HardklorSettings.Default)
        {
        }

        /// <param name="model">Isotope distributions.</param>
        /// <param name="search">Hardklor's search settings (charges, depth, correlation); each analyzer's peak
        /// width replaces its instrument and resolution.</param>
        public AnalyzerEnvelopeFinder(IEnvelopeModel model, HardklorSettings search)
        {
            _model = model;
            Search = search;
        }

        /// <summary>Hardklor's search settings: charge range, depth, correlation threshold.</summary>
        public HardklorSettings Search { get; }

        public bool CanSearch(MassAnalyzer analyzer) => SettingsFor(analyzer) is not null;

        public IReadOnlyList<IsotopeEnvelope> Find(double[] mz, double[] intensity, MassAnalyzer analyzer) =>
            CanSearch(analyzer)
                ? _finders.GetOrAdd(Key(analyzer), a => new HardklorFinder(SettingsFor(a, Search), _model)).Find(mz, intensity)
                : Array.Empty<IsotopeEnvelope>();

        /// <summary>Analyzers with the same settings share one finder (and its model library).</summary>
        private static MassAnalyzer Key(MassAnalyzer analyzer) => analyzer == MassAnalyzer.astral ? analyzer : MassAnalyzer.orbitrap;
    }
}
