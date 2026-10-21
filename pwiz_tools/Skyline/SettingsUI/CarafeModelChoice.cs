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
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.CarafeSharp.Proteome;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI
{
    /// <summary>
    /// A recently used fine-tuned Carafe model in the Build Library model list, described by what
    /// it was trained on, so a user can pick the model that matches the instrument and method the
    /// library is for. Its text is its path, which the editable list shows once it is chosen.
    /// </summary>
    public class CarafeModelChoice
    {
        private const int LINE_COUNT = 3;
        private const int PADDING = 3;

        /// <summary>
        /// The choice for the model file at <paramref name="path"/>, or null when the file is missing
        /// or is not a fine-tuned Carafe model.
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
            Details = GetDetails();
        }

        public string Path { get; }

        public string FileName
        {
            get { return System.IO.Path.GetFileName(Path); }
        }

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

        /// <summary>The fine-tuned RT model's R² on held-out peptides, if the file records it.</summary>
        public double? RtR2 { get; }

        /// <summary>When the model was saved, local time.</summary>
        public DateTime? Saved { get; }

        /// <summary>
        /// The gradient, precursor m/z range, NCE, fragmentation and analyzer a library predicted with
        /// the model takes, and when the model was saved.
        /// </summary>
        public string Details { get; }

        public string Title
        {
            get
            {
                return string.IsNullOrEmpty(Instrument) ? FileName : TextUtil.ColonSeparate(FileName, Instrument);
            }
        }

        public override string ToString()
        {
            return Path;
        }

        private static string GetInstrument(CarafeModelFile model)
        {
            var runInstruments = (model.Training?.Runs ?? Array.Empty<CarafeModelTrainingRun>())
                .Select(run => TextUtil.TextSeparate(@" ", run.InstrumentVendor, run.InstrumentModel))
                .Where(instrument => instrument.Length > 0)
                .Distinct().ToList();
            return runInstruments.Count > 0 ? runInstruments.CommaSeparateListItems() : model.Instrument;
        }

        /// <summary>The precursor m/z range as the model list and grid show it, or null when unknown.</summary>
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

        private string GetDetails()
        {
            var culture = LocalizationHelper.CurrentCulture;
            var parts = new List<string>();
            if (GradientMinutes.HasValue)
            {
                parts.Add(string.Format(SettingsUIResources.CarafeModelChoice_GetDetails__0__min_gradient,
                    GradientMinutes.Value.ToString(@"0.#", culture)));
            }
            if (PrecursorMzRange != null)
                parts.Add(string.Format(SettingsUIResources.CarafeModelChoice_GetDetails_precursor_m_z__0_, PrecursorMzRange));
            parts.Add(string.Format(SettingsUIResources.CarafeModelChoice_GetDetails_NCE__0_, Nce.ToString(@"0.#", culture)));
            if (Fragmentation.Length > 0)
                parts.Add(Fragmentation);
            if (Saved.HasValue)
                parts.Add(string.Format(SettingsUIResources.CarafeModelChoice_GetDetails_saved__0_, Saved.Value.ToString(@"d", culture)));
            return parts.CommaSeparateListItems();
        }

        public static int MeasureHeight(Font font)
        {
            return LINE_COUNT * font.Height + 2 * PADDING;
        }

        public int MeasureWidth(Graphics graphics, Font font)
        {
            using var boldFont = new Font(font, FontStyle.Bold);
            var widths = new[]
            {
                TextRenderer.MeasureText(graphics, Title, boldFont).Width,
                TextRenderer.MeasureText(graphics, Details, font).Width,
                TextRenderer.MeasureText(graphics, Path, font).Width,
            };
            return widths.Max() + 2 * PADDING;
        }

        /// <summary>
        /// Draws the model in the list: its file name and instrument in bold, what a library takes
        /// from it, and its path in gray.
        /// </summary>
        public void Draw(DrawItemEventArgs e)
        {
            e.DrawBackground();
            bool selected = (e.State & DrawItemState.Selected) != 0;
            var foreColor = selected ? SystemColors.HighlightText : SystemColors.WindowText;
            var pathColor = selected ? SystemColors.HighlightText : SystemColors.GrayText;
            var flags = TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
            int lineHeight = e.Font.Height;
            var line = new Rectangle(e.Bounds.Left + PADDING, e.Bounds.Top + PADDING, e.Bounds.Width - 2 * PADDING, lineHeight);
            using (var boldFont = new Font(e.Font, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, Title, boldFont, line, foreColor, flags);
            line.Offset(0, lineHeight);
            TextRenderer.DrawText(e.Graphics, Details, e.Font, line, foreColor, flags);
            line.Offset(0, lineHeight);
            TextRenderer.DrawText(e.Graphics, Path, e.Font, line, pathColor, flags | TextFormatFlags.PathEllipsis);
            e.DrawFocusRectangle();
        }
    }
}
