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

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// A saved model another one was fine-tuned further from (<c>-model</c> with training): the
    /// file, its SHA-256 to find it again, what wrote it and when, the runs it was trained on,
    /// and its models' origins, so the chain of <c>base_models</c> holds each model's lineage
    /// without the older files. It is one entry of the manifest's <c>base_models</c>
    /// (docs/06-saved-models.md).
    /// </summary>
    public sealed class CarafeModelBase
    {
        /// <summary>The file name, as the training run's <c>-model</c> named it.</summary>
        public string File { get; set; }
        /// <summary>The SHA-256 of the whole file (lowercase hex).</summary>
        public string Sha256 { get; set; }
        public string Creator { get; set; }
        public string Created { get; set; }
        /// <summary>The MS files it was trained on.</summary>
        public IReadOnlyList<string> Runs { get; set; } = new List<string>();
        /// <summary>Its MS2 model's origin, or null for a file that does not record one (<see cref="CarafeModelFile.FORMAT_1"/>).</summary>
        public CarafeModelOrigin Ms2Origin { get; set; }
        /// <summary>Its RT model's origin, or null for a file that does not record one.</summary>
        public CarafeModelOrigin RtOrigin { get; set; }

        /// <summary>A saved model, as a base of the models fine-tuned from it.</summary>
        public static CarafeModelBase Of(CarafeModelFile model)
        {
            return new CarafeModelBase
            {
                File = System.IO.Path.GetFileName(model.Path),
                Sha256 = model.FileSha256,
                Creator = model.Creator,
                Created = model.Created,
                Runs = model.Runs.Select(r => System.IO.Path.GetFileName(r.MsFile)).ToList(),
                Ms2Origin = model.Ms2Origin,
                RtOrigin = model.RtOrigin,
            };
        }

        public void WriteJson(Utf8JsonWriter json)
        {
            json.WriteStartObject();
            json.WriteString(@"file", File);
            json.WriteString(@"sha256", Sha256);
            json.WriteString(@"creator", Creator);
            json.WriteString(@"created", Created);
            json.WriteStartArray(@"runs");
            foreach (string run in Runs)
                json.WriteStringValue(run);
            json.WriteEndArray();
            json.WritePropertyName(@"models");
            if (Ms2Origin == null || RtOrigin == null)
            {
                json.WriteNullValue();
            }
            else
            {
                json.WriteStartObject();
                json.WriteStartObject(@"ms2");
                Ms2Origin.WriteFields(json);
                json.WriteEndObject();
                json.WriteStartObject(@"rt");
                RtOrigin.WriteFields(json);
                json.WriteEndObject();
                json.WriteEndObject();
            }
            json.WriteEndObject();
        }

        public static CarafeModelBase ReadJson(string path, JsonElement element)
        {
            bool hasModels = element.TryGetProperty(@"models", out var models) && models.ValueKind == JsonValueKind.Object;
            return new CarafeModelBase
            {
                File = element.GetProperty(@"file").GetString(),
                Sha256 = element.GetProperty(@"sha256").GetString(),
                Creator = element.GetProperty(@"creator").GetString(),
                Created = element.GetProperty(@"created").GetString(),
                Runs = element.GetProperty(@"runs").EnumerateArray().Select(r => r.GetString()).ToList(),
                Ms2Origin = hasModels ? CarafeModelOrigin.ReadFields(path, @"ms2", models.GetProperty(@"ms2"), false) : null,
                RtOrigin = hasModels ? CarafeModelOrigin.ReadFields(path, @"rt", models.GetProperty(@"rt"), false) : null,
            };
        }
    }
}
