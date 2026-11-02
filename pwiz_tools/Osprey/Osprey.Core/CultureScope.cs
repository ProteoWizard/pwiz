/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using System.Globalization;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Runs Osprey under one culture - for formatting (<see cref="CultureInfo.CurrentCulture"/>)
    /// and for resource lookup (<see cref="CultureInfo.CurrentUICulture"/>) - until disposed,
    /// then puts back what was there. Skyline's <c>--culture</c> sets the same pair.
    ///
    /// <para>The process defaults are set as well as the calling thread's, because Osprey does
    /// most of its work on pool threads (<c>Parallel.For</c>, per-file workers), which take the
    /// default rather than the thread that started them. Restoring both is what lets a test
    /// run a command line under fr-FR in process without the next test inheriting it.</para>
    /// </summary>
    public sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _culture;
        private readonly CultureInfo _uiCulture;
        private readonly CultureInfo _defaultCulture;
        private readonly CultureInfo _defaultUiCulture;

        public CultureScope(CultureInfo culture)
        {
            _culture = CultureInfo.CurrentCulture;
            _uiCulture = CultureInfo.CurrentUICulture;
            _defaultCulture = CultureInfo.DefaultThreadCurrentCulture;
            _defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
            Apply(culture, culture, culture, culture);
        }

        public void Dispose()
        {
            Apply(_culture, _uiCulture, _defaultCulture, _defaultUiCulture);
        }

        private static void Apply(CultureInfo culture, CultureInfo uiCulture,
            CultureInfo defaultCulture, CultureInfo defaultUiCulture)
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
            CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
            CultureInfo.DefaultThreadCurrentUICulture = defaultUiCulture;
        }
    }
}
