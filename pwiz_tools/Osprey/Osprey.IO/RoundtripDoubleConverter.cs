/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
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
using System.Globalization;
using Newtonsoft.Json;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Newtonsoft converter that emits every <see cref="double"/> via
    /// <see cref="Diagnostics.FormatF64Roundtrip"/>, so cross-impl JSON
    /// files (e.g., the Stage 5 → Stage 6 boundary
    /// <c>reconciliation.json</c>) carry a single canonical fixed-point
    /// decimal form for f64s on both runtimes. Newtonsoft's default
    /// <c>R</c>/Grisu output and Rust's default <c>ryu</c> output disagree
    /// on the decimal-vs-scientific threshold for small values (e.g.,
    /// <c>4.58e-5</c>), so neither default is suitable for byte-identical
    /// cross-impl comparison; routing both sides through
    /// <c>format_f64_roundtrip</c> sidesteps the disagreement.
    ///
    /// Reading is symmetric and lossless: numbers parse back to f64
    /// regardless of whether they were written via this converter or the
    /// Newtonsoft default.
    /// </summary>
    public class RoundtripDoubleConverter : JsonConverter<double>
    {
        /// <summary>
        /// <paramref name="value"/> as indented JSON with every double written through this
        /// converter, in the canonical form of <see cref="NormalizeJson"/>.
        /// </summary>
        public static string SerializeIndented(object value)
        {
            var settings = new JsonSerializerSettings { Converters = { new RoundtripDoubleConverter() } };
            return NormalizeJson(JsonConvert.SerializeObject(value, Formatting.Indented, settings));
        }

        /// <summary>
        /// Newtonsoft's <see cref="Formatting.Indented"/> emits CRLF on Windows; normalize to LF
        /// so the bytes match the Rust original (serde_json always writes LF), and end with a
        /// newline, matching the one Rust appends after the serializer - so the file ends with
        /// a closing brace and LF on every platform.
        /// </summary>
        public static string NormalizeJson(string json)
        {
            json = json.Replace(TextUtil.CRLF, TextUtil.LF);
            if (!json.EndsWith(TextUtil.LF, StringComparison.Ordinal))
                json += TextUtil.LF;
            return json;
        }

        public override void WriteJson(JsonWriter writer, double value, JsonSerializer serializer)
        {
            // Diagnostics.FormatF64Roundtrip returns "NaN" / "inf" / "-inf"
            // for non-finite values, none of which are valid JSON tokens.
            // Reconciliation arrays should never carry non-finite f64s, but
            // guard anyway so a malformed JSON file is never silently
            // produced.
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new JsonWriterException(string.Format(
                    OspreyIOResources.RoundtripDoubleConverter_WriteJson_Cannot_write__0__to_a_JSON_file__only_finite_numbers_can_be_stored_, value));
            }
            writer.WriteRawValue(Diagnostics.FormatF64Roundtrip(value));
        }

        public override double ReadJson(JsonReader reader, Type objectType, double existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            // Newtonsoft's default `Convert.ToDouble(reader.Value)` happily
            // returns 0.0 for `JsonToken.Null` (because `Convert.ToDouble`
            // accepts null), and silently truncates non-numeric tokens via
            // `ToString` paths. Both would let malformed cross-impl input
            // pass through without surfacing — reject explicitly so a
            // future schema drift produces a parse error instead of zeros.
            if (reader.TokenType != JsonToken.Integer && reader.TokenType != JsonToken.Float)
            {
                throw new JsonSerializationException(string.Format(
                    OspreyIOResources.RoundtripDoubleConverter_ReadJson_Expected_a_number_in_the_JSON_file__but_found__0__, reader.TokenType));
            }
            if (reader.Value == null)
                throw new JsonSerializationException(OspreyIOResources.RoundtripDoubleConverter_ReadJson_A_number_in_the_JSON_file_has_no_value_);
            return Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture);
        }
    }
}
