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
using System.IO;
using System.Text.Json;
using pwiz.CarafeSharp.Core;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// What one of a saved model's models is, and where it came from: the network (<see cref="Model"/>), the
    /// pretrained release it descends from (<see cref="Version"/>), and what the training that wrote the file
    /// fine-tuned it from (<see cref="Start"/>). A model that started from <see cref="START_BASE"/> came from the
    /// saved model first in <c>base_models</c>, whose entry records its own origins, and so on back, so one file
    /// holds each model's whole lineage (docs/06-saved-models.md).
    /// </summary>
    public sealed class CarafeModelOrigin
    {
        /// <summary>AlphaPeptDeep's networks: the MS2 model, and the RT model unless it is Chronologer.</summary>
        public const string ALPHAPEPTDEEP = @"alphapeptdeep";

        /// <summary>The fine-tune started from the bundled pretrained model of <see cref="Model"/>.</summary>
        public const string START_PRETRAINED = @"pretrained";
        /// <summary>It started from the same model of the saved model it was fine-tuned further from (<c>-model</c>).</summary>
        public const string START_BASE = @"base";
        /// <summary>The MS2 fine-tune started from <c>-ms2_model</c>, which <c>ms2_start_model</c> names.</summary>
        public const string START_MS2_MODEL = @"ms2_model";

        /// <summary>
        /// The network: <see cref="ALPHAPEPTDEEP"/> for MS2; an <see cref="RtModelType"/> name for RT. For a model
        /// the file does not hold, the one a library from it predicts with: that network's pretrained model.
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// The pretrained release the model is or descends from (AlphaPeptDeep's v1, Chronologer's 20220601193755), or
        /// null when unknown: a lineage that starts from <c>-ms2_model</c>.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// What the training that wrote the file fine-tuned the model from (<see cref="START_PRETRAINED"/>,
        /// <see cref="START_BASE"/>, <see cref="START_MS2_MODEL"/>), or null when it did not fine-tune it.
        /// </summary>
        public string Start { get; set; }

        /// <summary>The RT network, for an RT model's origin.</summary>
        public RtModelType RtModel
        {
            get { return (RtModelType)Enum.Parse(typeof(RtModelType), Model); }
        }

        /// <summary>A one-line description for the log and <c>-model_info</c>: the network and release, and the start.</summary>
        public override string ToString()
        {
            string start = Start == null ? @"not fine-tuned" : Start == START_BASE ? @"fine-tuned from the base model" :
                Start == START_MS2_MODEL ? @"fine-tuned from -ms2_model" : @"fine-tuned from the pretrained model";
            return string.Format(@"{0} {1}, {2}", Model, Version ?? @"(version unknown)", start);
        }

        /// <summary>Writes the fields into the model's open JSON object.</summary>
        public void WriteFields(Utf8JsonWriter json)
        {
            json.WriteString(@"model", Model);
            WriteStringOrNull(json, @"model_version", Version);
            WriteStringOrNull(json, @"start", Start);
        }

        /// <summary>
        /// The origin in a model's JSON object, refusing a network this CarafeSharp does not know (MS2: AlphaPeptDeep's;
        /// RT: an <see cref="RtModelType"/>) or a start it does not know.
        /// </summary>
        public static CarafeModelOrigin ReadFields(string path, string name, JsonElement element)
        {
            string model = element.GetProperty(@"model").GetString();
            bool known = name == @"rt"
                ? model != null && Enum.GetNames(typeof(RtModelType)).Contains(model, StringComparer.Ordinal)
                : model == ALPHAPEPTDEEP;
            if (!known)
            {
                throw new InvalidDataException(string.Format(@"{0} has {1} model {2}, which this CarafeSharp does not know.",
                    path, name.ToUpperInvariant(), model ?? @"(none)"));
            }
            string start = GetStringOrNull(element, @"start");
            if (start != null && start != START_PRETRAINED && start != START_BASE && start != START_MS2_MODEL)
                throw new InvalidDataException(string.Format(@"{0} has {1} model start {2}, which this CarafeSharp does not know.", path, name, start));
            return new CarafeModelOrigin { Model = model, Version = GetStringOrNull(element, @"model_version"), Start = start };
        }

        private static void WriteStringOrNull(Utf8JsonWriter json, string name, string value)
        {
            if (value == null)
                json.WriteNull(name);
            else
                json.WriteString(name, value);
        }

        private static string GetStringOrNull(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }
    }
}
