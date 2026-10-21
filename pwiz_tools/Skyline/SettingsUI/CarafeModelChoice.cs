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
            Details = GetDetails(model);
        }

        public string Path { get; }

        /// <summary>The instrument the training runs were acquired on, as their files name it.</summary>
        public string Instrument { get; }

        /// <summary>
        /// The gradient, precursor m/z range, NCE, fragmentation and analyzer a library predicted with
        /// the model takes, and when the model was saved.
        /// </summary>
        public string Details { get; }

        public string Title
        {
            get
            {
                string fileName = System.IO.Path.GetFileName(Path);
                return string.IsNullOrEmpty(Instrument) ? fileName : TextUtil.ColonSeparate(fileName, Instrument);
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

        private static string GetDetails(CarafeModelFile model)
        {
            var culture = LocalizationHelper.CurrentCulture;
            var parts = new List<string>();
            if (model.RtMax > 0)
            {
                parts.Add(string.Format(SettingsUIResources.CarafeModelChoice_GetDetails__0__min_gradient,
                    model.RtMax.ToString(@"0.#", culture)));
            }
            var runs = model.Training?.Runs ?? Array.Empty<CarafeModelTrainingRun>();
            var mzMins = runs.Where(run => run.IsolationMzMin.HasValue).Select(run => run.IsolationMzMin.Value).ToList();
            var mzMaxes = runs.Where(run => run.IsolationMzMax.HasValue).Select(run => run.IsolationMzMax.Value).ToList();
            if (mzMins.Count > 0 && mzMaxes.Count > 0)
            {
                parts.Add(string.Format(SettingsUIResources.CarafeModelChoice_GetDetails_precursor_m_z__0___1_,
                    mzMins.Min().ToString(@"0", culture), mzMaxes.Max().ToString(@"0", culture)));
            }
            parts.Add(string.Format(SettingsUIResources.CarafeModelChoice_GetDetails_NCE__0_, model.Nce.ToString(@"0.#", culture)));
            string fragmentation = TextUtil.TextSeparate(@" ", model.Activation, model.Analyzer);
            if (fragmentation.Length > 0)
                parts.Add(fragmentation);
            if (DateTime.TryParse(model.Created, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var created))
            {
                parts.Add(string.Format(SettingsUIResources.CarafeModelChoice_GetDetails_saved__0_,
                    created.ToLocalTime().ToString(@"d", culture)));
            }
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
