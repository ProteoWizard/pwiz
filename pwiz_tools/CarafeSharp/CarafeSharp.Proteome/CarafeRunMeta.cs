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

using System.Text.Json;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// One training MS file's entry in a Carafe model folder's meta.json. A field the file
    /// lacks keeps Java's default of 0 or the empty string.
    /// </summary>
    public sealed class CarafeRunMeta
    {
        public static CarafeRunMeta FromJson(JsonElement element)
        {
            return new CarafeRunMeta
            {
                MsFile = GetString(element, @"ms_file"),
                MsInstrument = GetString(element, @"ms_instrument"),
                Nce = GetDouble(element, @"nce"),
                RtMax = GetDouble(element, @"rt_max"),
                RtMin = GetDouble(element, @"rt_min"),
                PrecursorMzMin = GetDouble(element, @"precursor_ion_mz_min"),
                PrecursorMzMax = GetDouble(element, @"precursor_ion_mz_max"),
                LfFragMzMin = GetDouble(element, @"lf_frag_mz_min"),
                LfFragMzMax = GetDouble(element, @"lf_frag_mz_max"),
            };
        }

        public string MsFile { get; private set; }
        public string MsInstrument { get; private set; }
        public double Nce { get; private set; }

        /// <summary>The training gradient length, minutes.</summary>
        public double RtMax { get; private set; }

        public double RtMin { get; private set; }

        /// <summary>The training data's precursor isolation range.</summary>
        public double PrecursorMzMin { get; private set; }

        public double PrecursorMzMax { get; private set; }

        public double LfFragMzMin { get; private set; }
        public double LfFragMzMax { get; private set; }

        private static string GetString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : string.Empty;
        }

        private static double GetDouble(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
                ? value.GetDouble()
                : 0;
        }
    }
}
