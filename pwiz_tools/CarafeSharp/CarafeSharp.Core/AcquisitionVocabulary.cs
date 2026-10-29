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
using System.Collections.Generic;
using System.Linq;

namespace pwiz.CarafeSharp.Core
{
    /// <summary>
    /// The acquisition conditions an MS2 model distinguishes beyond peptdeep's instrument
    /// families, CarafeSharp's addition: how precursors were activated and which analyzer read
    /// the MS2 spectra out. Each value is one input column of the model's acquisition layer,
    /// whose output is added to peptdeep's instrument and NCE layer.
    /// <para>
    /// A model that has never trained them (peptdeep's pretrained model, a Carafe checkpoint)
    /// has every column at zero, so it predicts exactly as without them; fine-tuning learns each
    /// value's effect from the spectra that carry it. A model holds its own list, in column order,
    /// recorded in its safetensors metadata, so the number of values is not fixed: a value is
    /// looked up by name, and a model loaded with a list lacking one of <see cref="KNOWN_ACTIVATIONS"/>
    /// or <see cref="KNOWN_ANALYZERS"/> gains a zero column for it.
    /// </para>
    /// <para>
    /// The activation names avoid vendors' terms, which disagree: Thermo's CID is resonance CID in
    /// an ion trap, while Sciex's and Bruker's CID is beam-type, Thermo's HCD.
    /// </para>
    /// </summary>
    public sealed class AcquisitionVocabulary
    {
        /// <summary>Beam-type CID: Thermo's HCD, and Sciex's and Bruker's CID.</summary>
        public const string BEAM_CID = @"beam-CID";

        /// <summary>Resonance CID in an ion trap: Thermo's CID.</summary>
        public const string RE_CID = @"reCID";

        public const string ORBITRAP = @"Orbitrap";

        /// <summary>A linear ion trap: a Stellar's, a Tribrid's.</summary>
        public const string LIT = @"LIT";

        /// <summary>A time-of-flight analyzer: an Astral's, a timsTOF's, a Sciex TOF's.</summary>
        public const string TOF = @"ToF";

        public const string ACTIVATIONS_KEY = @"carafesharp.activations";
        public const string ANALYZERS_KEY = @"carafesharp.analyzers";

        public static readonly IReadOnlyList<string> KNOWN_ACTIVATIONS = new[] { BEAM_CID, RE_CID };
        public static readonly IReadOnlyList<string> KNOWN_ANALYZERS = new[] { ORBITRAP, LIT, TOF };

        /// <summary>Every known value, in the order a new model's columns take.</summary>
        public static readonly AcquisitionVocabulary DEFAULT = new AcquisitionVocabulary(KNOWN_ACTIVATIONS, KNOWN_ANALYZERS);

        public AcquisitionVocabulary(IEnumerable<string> activations, IEnumerable<string> analyzers)
        {
            Activations = activations.ToArray();
            Analyzers = analyzers.ToArray();
        }

        public IReadOnlyList<string> Activations { get; }
        public IReadOnlyList<string> Analyzers { get; }

        /// <summary>Input columns: one per activation, then one per analyzer.</summary>
        public int Width
        {
            get { return Activations.Count + Analyzers.Count; }
        }

        /// <summary>This list with every known value it lacks appended, in known order: the columns a loaded model predicts with.</summary>
        public AcquisitionVocabulary WithKnownValues()
        {
            return new AcquisitionVocabulary(Activations.Concat(KNOWN_ACTIVATIONS.Except(Activations, StringComparer.Ordinal)),
                Analyzers.Concat(KNOWN_ANALYZERS.Except(Analyzers, StringComparer.Ordinal)));
        }

        /// <summary>The column of <paramref name="activation"/>, or -1 for null or a value the model does not have.</summary>
        public int ActivationColumn(string activation)
        {
            return IndexOf(Activations, activation, 0);
        }

        /// <summary>The column of <paramref name="analyzer"/> (after the activations), or -1 for null or a value the model does not have.</summary>
        public int AnalyzerColumn(string analyzer)
        {
            return IndexOf(Analyzers, analyzer, Activations.Count);
        }

        /// <summary>The safetensors metadata recording this list.</summary>
        public IReadOnlyDictionary<string, string> ToMetadata()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { ACTIVATIONS_KEY, string.Join(@",", Activations) },
                { ANALYZERS_KEY, string.Join(@",", Analyzers) },
            };
        }

        /// <summary>The list a model's metadata records, or null when it records none.</summary>
        public static AcquisitionVocabulary FromMetadata(IReadOnlyDictionary<string, string> metadata)
        {
            if (!metadata.TryGetValue(ACTIVATIONS_KEY, out string activations) || !metadata.TryGetValue(ANALYZERS_KEY, out string analyzers))
                return null;
            return new AcquisitionVocabulary(Split(activations), Split(analyzers));
        }

        /// <summary>The known activation <paramref name="name"/> names (case-insensitive), or null.</summary>
        public static string ParseActivation(string name)
        {
            return KNOWN_ACTIVATIONS.FirstOrDefault(a => string.Equals(a, name?.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The known analyzer <paramref name="name"/> names (case-insensitive), or null.</summary>
        public static string ParseAnalyzer(string name)
        {
            return KNOWN_ANALYZERS.FirstOrDefault(a => string.Equals(a, name?.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public override string ToString()
        {
            return @"activations " + string.Join(@", ", Activations) + @"; analyzers " + string.Join(@", ", Analyzers);
        }

        private static int IndexOf(IReadOnlyList<string> values, string value, int offset)
        {
            if (value == null)
                return -1;
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], value, StringComparison.Ordinal))
                    return offset + i;
            }
            return -1;
        }

        private static string[] Split(string text)
        {
            return text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()).ToArray();
        }
    }
}
