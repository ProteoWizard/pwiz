/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) main.java.ai.JMeta
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
using System.Text.Json;
using pwiz.CarafeSharp.Core;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// One training MS file's entry in a Carafe model folder's meta.json (Carafe's
    /// <c>JMeta</c>). A field the file lacks, and one a writer does not set, keeps JMeta's
    /// default, as Carafe's JSON reader leaves it.
    /// </summary>
    public sealed class CarafeRunMeta
    {
        // Carafe's JMeta field defaults.
        public const string DEFAULT_MS_FILE = @"-";
        public const string DEFAULT_MS_INSTRUMENT = @"-";
        public const double DEFAULT_NCE = 0;
        public const double DEFAULT_MIN_FRAGMENT_ION_MZ = 200.0;
        public const double DEFAULT_MAX_FRAGMENT_ION_MZ = 2000.0;
        public const double DEFAULT_LF_FRAG_MZ_MIN = 200.0;
        public const double DEFAULT_LF_FRAG_MZ_MAX = 1800.0;
        public const int DEFAULT_LF_TOP_N_FRAGMENT_IONS = 20;
        public const double DEFAULT_RT_MAX = 0;
        public const double DEFAULT_RT_MIN = 0;
        public const double DEFAULT_PRECURSOR_ION_MZ_MIN = 300.0;
        public const double DEFAULT_PRECURSOR_ION_MZ_MAX = 1800.0;

        public static CarafeRunMeta FromJson(JsonElement element)
        {
            return new CarafeRunMeta
            {
                MsFile = GetString(element, ModelFiles.META_MS_FILE, DEFAULT_MS_FILE),
                MsInstrument = GetString(element, ModelFiles.META_MS_INSTRUMENT, DEFAULT_MS_INSTRUMENT),
                Nce = GetDouble(element, ModelFiles.META_NCE, DEFAULT_NCE),
                MinFragmentIonMz = GetDouble(element, ModelFiles.META_MIN_FRAGMENT_ION_MZ, DEFAULT_MIN_FRAGMENT_ION_MZ),
                MaxFragmentIonMz = GetDouble(element, ModelFiles.META_MAX_FRAGMENT_ION_MZ, DEFAULT_MAX_FRAGMENT_ION_MZ),
                LfFragMzMin = GetDouble(element, ModelFiles.META_LF_FRAG_MZ_MIN, DEFAULT_LF_FRAG_MZ_MIN),
                LfFragMzMax = GetDouble(element, ModelFiles.META_LF_FRAG_MZ_MAX, DEFAULT_LF_FRAG_MZ_MAX),
                LfTopNFragmentIons = (int)GetDouble(element, ModelFiles.META_LF_TOP_N_FRAGMENT_IONS, DEFAULT_LF_TOP_N_FRAGMENT_IONS),
                RtMax = GetDouble(element, ModelFiles.META_RT_MAX, DEFAULT_RT_MAX),
                RtMin = GetDouble(element, ModelFiles.META_RT_MIN, DEFAULT_RT_MIN),
                PrecursorMzMin = GetDouble(element, ModelFiles.META_PRECURSOR_ION_MZ_MIN, DEFAULT_PRECURSOR_ION_MZ_MIN),
                PrecursorMzMax = GetDouble(element, ModelFiles.META_PRECURSOR_ION_MZ_MAX, DEFAULT_PRECURSOR_ION_MZ_MAX),
            };
        }

        /// <summary>The training MS file, which meta.json also keys the entry by.</summary>
        public string MsFile { get; set; } = DEFAULT_MS_FILE;

        /// <summary>The instrument Carafe detected in the run, empty when it recognized none.</summary>
        public string MsInstrument { get; set; } = DEFAULT_MS_INSTRUMENT;

        public double Nce { get; set; } = DEFAULT_NCE;

        /// <summary>The run's MS2 scan window.</summary>
        public double MinFragmentIonMz { get; set; } = DEFAULT_MIN_FRAGMENT_ION_MZ;

        public double MaxFragmentIonMz { get; set; } = DEFAULT_MAX_FRAGMENT_ION_MZ;

        /// <summary>The library fragment m/z range; a Carafe training run leaves the default.</summary>
        public double LfFragMzMin { get; set; } = DEFAULT_LF_FRAG_MZ_MIN;

        public double LfFragMzMax { get; set; } = DEFAULT_LF_FRAG_MZ_MAX;

        public int LfTopNFragmentIons { get; set; } = DEFAULT_LF_TOP_N_FRAGMENT_IONS;

        /// <summary>The training gradient length, minutes.</summary>
        public double RtMax { get; set; } = DEFAULT_RT_MAX;

        public double RtMin { get; set; } = DEFAULT_RT_MIN;

        /// <summary>The training data's precursor isolation range.</summary>
        public double PrecursorMzMin { get; set; } = DEFAULT_PRECURSOR_ION_MZ_MIN;

        public double PrecursorMzMax { get; set; } = DEFAULT_PRECURSOR_ION_MZ_MAX;

        /// <summary>The entry as Carafe's fastjson writes it: every field, keys in alphabetical order.</summary>
        public IReadOnlyDictionary<string, object> ToJson()
        {
            return new SortedDictionary<string, object>(StringComparer.Ordinal)
            {
                { ModelFiles.META_MS_FILE, MsFile },
                { ModelFiles.META_MS_INSTRUMENT, MsInstrument },
                { ModelFiles.META_NCE, Nce },
                { ModelFiles.META_MIN_FRAGMENT_ION_MZ, MinFragmentIonMz },
                { ModelFiles.META_MAX_FRAGMENT_ION_MZ, MaxFragmentIonMz },
                { ModelFiles.META_LF_FRAG_MZ_MIN, LfFragMzMin },
                { ModelFiles.META_LF_FRAG_MZ_MAX, LfFragMzMax },
                { ModelFiles.META_LF_TOP_N_FRAGMENT_IONS, LfTopNFragmentIons },
                { ModelFiles.META_RT_MAX, RtMax },
                { ModelFiles.META_RT_MIN, RtMin },
                { ModelFiles.META_PRECURSOR_ION_MZ_MIN, PrecursorMzMin },
                { ModelFiles.META_PRECURSOR_ION_MZ_MAX, PrecursorMzMax },
            };
        }

        private static string GetString(JsonElement element, string name, string defaultValue)
        {
            return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : defaultValue;
        }

        private static double GetDouble(JsonElement element, string name, double defaultValue)
        {
            return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
                ? value.GetDouble()
                : defaultValue;
        }
    }
}
