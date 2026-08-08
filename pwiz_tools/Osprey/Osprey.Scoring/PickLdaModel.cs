/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using System.IO;
using Newtonsoft.Json;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// Frozen linear pick model loaded from the JSON at OSPREY_PICK_LDA_MODEL
    /// (<see cref="OspreyEnvironment.PickLdaModelPath"/>). When present it REPLACES the
    /// product-form CWT candidate rank score in <see cref="PeakDataExtractor"/> with a
    /// standardized linear combination of the same four raw terms the
    /// OSPREY_PICK_DUMP_CANDIDATES dump captures:
    ///   rank = w0*z(coelution) + w1*z(ln_intensity) + w2*z(rt_penalty) + w3*z(median_polish)
    /// where z(x_i) = (x_i - mean[i]) / scale[i]. The argmax + total-order tie-break are
    /// unchanged. Loaded and cached ONCE (env vars are read once at process start), so the
    /// hot loop pays a single reference read per <c>TryExtract</c> and nothing when the model
    /// is absent.
    ///
    /// JSON schema:
    ///   { "features": ["coelution","ln_intensity","rt_penalty","median_polish"],
    ///     "weights": [w0,w1,w2,w3], "means": [m0,m1,m2,m3], "scales": [s0,s1,s2,s3] }
    /// </summary>
    internal sealed class PickLdaModel
    {
        private const int N = 4; // coelution, ln_intensity, rt_penalty, median_polish

        // The fixed feature order Score() applies weights[i] to. A loaded model MUST declare its
        // features in this exact order (see the JSON validation in LoadFromEnv) so a re-ordered or
        // older-schema file cannot silently map weights to the wrong term. Matches pick_lda_train.py's
        // FEATURES and the OSPREY_PICK_DUMP_CANDIDATES TSV columns.
        private static readonly string[] ExpectedFeatures =
            { @"coelution", @"ln_intensity", @"rt_penalty", @"median_polish" };

        private readonly double[] _weights;
        private readonly double[] _means;
        private readonly double[] _scales;

        private PickLdaModel(double[] weights, double[] means, double[] scales)
        {
            _weights = weights;
            _means = means;
            _scales = scales;
        }

        // Resolution-keyed default peak-pick models, learned per platform via the paired
        // target/decoy pick-LDA (see pick_lda_train.py / OSPREY_PICK_DUMP_CANDIDATES). Unit
        // resolution -> Stellar-trained weights; HRAM -> Astral-trained weights. In the future
        // we may add more pick models for specific platforms/instruments; for now we split only
        // on resolution (unit vs HRAM). Feature order is fixed: coelution, ln_intensity,
        // rt_penalty, median_polish (see <see cref="Score"/> and the OSPREY_PICK_DUMP_CANDIDATES
        // TSV columns). Values copied verbatim from pick-model-stellar.json / pick-model-astral.json.
        private static readonly PickLdaModel StellarModel = new PickLdaModel(
            new[] { 0.9933168416485256, 0.047052481253413006, 0.027130393118192445, 0.10184133676728513 },
            new[] { 0.14931086687377143, 9.15749607304815, 0.9158212545538758, 0.8904037620854307 },
            new[] { 0.2074610054197197, 2.260347450504608, 0.07333900267211861, 0.08220791724094854 });

        private static readonly PickLdaModel AstralModel = new PickLdaModel(
            new[] { 0.5348241578558818, 0.0041302671426268105, 0.3352868625222239, 0.7755828652613985 },
            new[] { 0.027393438120134818, 6.585876043601798, 0.939316453828307, 0.6880222328717774 },
            new[] { 0.11714825645722571, 3.9104476306002494, 0.05338768554968953, 0.1461117956225306 });

        /// <summary>
        /// The default hardcoded pick model for the given scoring resolution:
        /// HRAM (MS1 features present) -> Astral-trained; unit resolution -> Stellar-trained.
        /// </summary>
        public static PickLdaModel ForResolution(bool hasMs1Features)
        {
            return hasMs1Features ? AstralModel : StellarModel;
        }

        private static readonly object _loadLock = new object();
        private static volatile bool _loaded;
        private static PickLdaModel _cached;

        /// <summary>
        /// The process-wide model, or null when OSPREY_PICK_LDA_MODEL is unset / points at a
        /// missing file. Loaded once and cached; subsequent calls are a volatile read.
        /// </summary>
        public static PickLdaModel Current
        {
            get
            {
                if (_loaded)
                    return _cached;
                lock (_loadLock)
                {
                    if (_loaded)
                        return _cached;
                    _cached = LoadFromEnv();
                    _loaded = true;
                    return _cached;
                }
            }
        }

        private static PickLdaModel LoadFromEnv()
        {
            string path = OspreyEnvironment.PickLdaModelPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;

            Dto dto;
            try
            {
                dto = JsonConvert.DeserializeObject<Dto>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                throw new FormatException(string.Format(
                    @"OSPREY_PICK_LDA_MODEL: failed to parse pick model JSON at '{0}': {1}",
                    path, ex.Message), ex);
            }

            if (dto == null ||
                dto.Weights == null || dto.Weights.Length != N ||
                dto.Means == null || dto.Means.Length != N ||
                dto.Scales == null || dto.Scales.Length != N)
            {
                throw new FormatException(string.Format(
                    @"OSPREY_PICK_LDA_MODEL: pick model JSON at '{0}' must define weights, means, " +
                    @"and scales as length-{1} arrays.", path, N));
            }

            // The weights are positional (Score applies weights[i] to the i-th raw term), so a file
            // whose features are in a different order -- or absent -- would silently score the wrong
            // term. Require the names and the exact expected order rather than trusting array position.
            if (dto.Features == null || dto.Features.Length != N ||
                !dto.Features.SequenceEqual(ExpectedFeatures))
            {
                throw new FormatException(string.Format(
                    @"OSPREY_PICK_LDA_MODEL: pick model JSON at '{0}' must list features as [{1}] in " +
                    @"that exact order; got [{2}].", path, string.Join(@", ", ExpectedFeatures),
                    dto.Features == null ? @"<missing>" : string.Join(@", ", dto.Features)));
            }

            return new PickLdaModel(dto.Weights, dto.Means, dto.Scales);
        }

        /// <summary>
        /// The learned rank score for one candidate from its four raw terms. A zero scale
        /// standardizes to 0 for that feature (a constant feature contributes nothing) rather
        /// than dividing by zero.
        /// </summary>
        public double Score(double coelution, double lnIntensity, double rtPenalty, double medianPolish)
        {
            return _weights[0] * Z(coelution, 0)
                 + _weights[1] * Z(lnIntensity, 1)
                 + _weights[2] * Z(rtPenalty, 2)
                 + _weights[3] * Z(medianPolish, 3);
        }

        private double Z(double x, int i)
        {
            double scale = _scales[i];
            return scale != 0.0 ? (x - _means[i]) / scale : 0.0;
        }

        // Auto-properties (not fields) so the compiler does not flag them CS0649 -- Newtonsoft
        // assigns them by reflection, which the field-assignment analysis cannot see.
        private sealed class Dto
        {
            [JsonProperty(@"features")] public string[] Features { get; set; }
            [JsonProperty(@"weights")] public double[] Weights { get; set; }
            [JsonProperty(@"means")] public double[] Means { get; set; }
            [JsonProperty(@"scales")] public double[] Scales { get; set; }
        }
    }
}
