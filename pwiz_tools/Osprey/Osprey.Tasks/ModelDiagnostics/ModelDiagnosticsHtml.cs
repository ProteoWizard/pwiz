/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using pwiz.Osprey.FDR.ModelDiagnostics;

namespace pwiz.Osprey.Tasks.ModelDiagnostics
{
    /// <summary>
    /// Renders a <see cref="ModelDiagnosticsData"/> into a single self-contained
    /// HTML page. The page markup + CSS + JS live in an embedded resource
    /// (<c>model-diagnostics-template.html</c>); this class serializes the data
    /// to a JSON blob and splices it in at the <c>__OSPREY_DATA__</c> token. The
    /// result references no external host (no CDN, fonts, or images) so it opens
    /// offline by double-click and survives being emailed around.
    /// </summary>
    public static class ModelDiagnosticsHtml
    {
        private const string TemplateResource = @"model-diagnostics-template.html";
        private const string DataToken = @"/*__OSPREY_DATA__*/";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Include,
            Formatting = Formatting.None,
            FloatFormatHandling = FloatFormatHandling.String, // NaN/Infinity -> "NaN" (JSON.parse via reviver n/a; JS treats as null-ish)
        };

        public static string Render(ModelDiagnosticsData data)
        {
            string template = LoadTemplate();
            string json = JsonConvert.SerializeObject(data, JsonSettings);
            // The token lives inside a <script type="application/json"> element,
            // so the only sequence that could break out is a literal </script>.
            // JSON strings here are Osprey feature labels + file names -- no tags
            // -- but guard anyway.
            json = json.Replace(@"</", @"<\/");
            int idx = template.IndexOf(DataToken, StringComparison.Ordinal);
            if (idx < 0)
                throw new InvalidOperationException(@"model-diagnostics template is missing its data token");
            return template.Substring(0, idx) + json + template.Substring(idx + DataToken.Length);
        }

        private static string LoadTemplate()
        {
            var asm = typeof(ModelDiagnosticsHtml).Assembly;
            string name = FindResourceName(asm, TemplateResource);
            if (name == null)
                throw new InvalidOperationException(@"embedded model-diagnostics template resource not found");
            using (var stream = asm.GetManifestResourceStream(name))
            using (var reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }

        private static string FindResourceName(Assembly asm, string endsWith)
        {
            foreach (var n in asm.GetManifestResourceNames())
            {
                if (n.EndsWith(endsWith, StringComparison.OrdinalIgnoreCase))
                    return n;
            }
            return null;
        }
    }
}
