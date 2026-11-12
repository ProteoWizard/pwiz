/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Globalization;
using System.Linq;
using pwiz.CarafeSharp.Proteome;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI
{
    /// <summary>
    /// What a fine-tuned Carafe model file says it was trained on - instrument, gradient,
    /// precursor m/z, NCE, fragmentation, training runs, held-out scores and date - read from the
    /// file for the model list's display names and the grid that edits the list, so a user can
    /// pick the model that matches the instrument and method the library is for.
    /// </summary>
    public class CarafeModelChoice
    {
        /// <summary>
        /// The description of the model file at <paramref name="path"/>, or null when the file is
        /// missing or is not a fine-tuned Carafe model.
        /// </summary>
        public static CarafeModelChoice Read(string path)
        {
            try
            {
                return new CarafeModelChoice(CarafeModelFile.ReadDescription(path));
            }
            catch (Exception x)
            {
                if (ExceptionUtil.IsProgrammingDefect(x))
                    throw;
                return null;
            }
        }

        /// <summary>
        /// The name a list shows for <paramref name="spec"/>: its name, with the instrument,
        /// gradient and NCE its file records when the file can be read, as the enzyme list shows
        /// each enzyme with its cleavage.
        /// </summary>
        public static string GetDisplayName(CarafeModelSpec spec)
        {
            var choice = spec.IsDefault ? null : Read(spec.FilePath);
            string summary = choice?.Summary;
            return string.IsNullOrEmpty(summary) ? spec.Name : TextUtil.SpaceSeparate(spec.Name, @"(" + summary + @")");
        }

        private CarafeModelChoice(CarafeModelFile model)
        {
            Path = model.Path;
            Instrument = GetInstrument(model);
            if (model.RtMax > 0)
                GradientMinutes = model.RtMax;
            var runs = model.Training?.Runs ?? Array.Empty<CarafeModelTrainingRun>();
            var mzMins = runs.Where(run => run.IsolationMzMin.HasValue).Select(run => run.IsolationMzMin.Value).ToList();
            var mzMaxes = runs.Where(run => run.IsolationMzMax.HasValue).Select(run => run.IsolationMzMax.Value).ToList();
            if (mzMins.Count > 0 && mzMaxes.Count > 0)
            {
                PrecursorMzMin = mzMins.Min();
                PrecursorMzMax = mzMaxes.Max();
            }
            Nce = model.Nce;
            Fragmentation = TextUtil.TextSeparate(@" ", model.Activation, model.Analyzer);
            TrainingRunCount = runs.Count > 0 ? runs.Count : model.Runs.Count;
            var metrics = model.Training?.HeldOutMetrics;
            if (metrics != null && metrics.TryGetValue(@"ms2.finetuned.cos", out double ms2Cosine))
                Ms2Cosine = ms2Cosine;
            if (metrics != null && metrics.TryGetValue(@"rt.finetuned.r2", out double rtR2))
                RtR2 = rtR2;
            if (DateTime.TryParse(model.Created, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var saved))
                Saved = saved.ToLocalTime();
        }

        public string Path { get; }

        /// <summary>The instrument the training runs were acquired on, as their files name it.</summary>
        public string Instrument { get; }

        /// <summary>The gradient length a library predicted with the model takes its retention times on, if any.</summary>
        public double? GradientMinutes { get; }

        /// <summary>The precursor m/z range the training runs isolated, if they say.</summary>
        public double? PrecursorMzMin { get; }
        public double? PrecursorMzMax { get; }

        /// <summary>The NCE a library predicted with the model takes.</summary>
        public double Nce { get; }

        /// <summary>The activation and MS2 analyzer a library predicted with the model takes.</summary>
        public string Fragmentation { get; }

        public int TrainingRunCount { get; }

        /// <summary>The fine-tuned MS2 model's cosine similarity on held-out spectra, if the file records it.</summary>
        public double? Ms2Cosine { get; }

        /// <summary>The fine-tuned RT model's R2 on held-out peptides, if the file records it.</summary>
        public double? RtR2 { get; }

        /// <summary>When the model was saved, local time.</summary>
        public DateTime? Saved { get; }

        /// <summary>The precursor m/z range as the grid shows it, or null when unknown.</summary>
        public string PrecursorMzRange
        {
            get
            {
                if (!PrecursorMzMin.HasValue || !PrecursorMzMax.HasValue)
                    return null;
                var culture = LocalizationHelper.CurrentCulture;
                return string.Format(SettingsUIResources.CarafeModelChoice_PrecursorMzRange__0___1_,
                    PrecursorMzMin.Value.ToString(@"0", culture), PrecursorMzMax.Value.ToString(@"0", culture));
            }
        }

        /// <summary>
        /// The instrument, gradient and NCE, comma separated: what tells one model from another
        /// in a list of names.
        /// </summary>
        public string Summary
        {
            get
            {
                var culture = LocalizationHelper.CurrentCulture;
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(Instrument))
                    parts.Add(Instrument);
                if (GradientMinutes.HasValue)
                {
                    parts.Add(string.Format(SettingsUIResources.CarafeModelChoice_GetDetails__0__min_gradient,
                        GradientMinutes.Value.ToString(@"0.#", culture)));
                }
                parts.Add(string.Format(SettingsUIResources.CarafeModelChoice_GetDetails_NCE__0_, Nce.ToString(@"0.#", culture)));
                return parts.CommaSeparateListItems();
            }
        }

        private static string GetInstrument(CarafeModelFile model)
        {
            var runInstruments = (model.Training?.Runs ?? Array.Empty<CarafeModelTrainingRun>())
                .Select(run => TextUtil.TextSeparate(@" ", run.InstrumentVendor, run.InstrumentModel))
                .Where(instrument => instrument.Length > 0)
                .Distinct().ToList();
            return runInstruments.Count > 0 ? runInstruments.CommaSeparateListItems() : model.Instrument;
        }
    }
}
