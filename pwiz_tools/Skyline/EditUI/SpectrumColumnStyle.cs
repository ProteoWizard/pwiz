/*
 * Original author: Brian Pratt <bspratt .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Drawing;
using pwiz.Skyline.Model.Results.Spectra;

namespace pwiz.Skyline.EditUI
{
    /// <summary>
    /// How a column's <see cref="SpectrumColumnScanner.Standing"/> is shown, in one place.
    ///
    /// Two screens draw this: the property list in <see cref="EditSpectrumFilterDlg"/>, and the help that
    /// explains what the list's styling means. Stating the rule twice would let them disagree, and the one
    /// that would be wrong is the help - which fails silently, since a screen that describes a convention
    /// cannot be checked against it by any test that does not know the convention itself. Here, the help
    /// renders its examples through the same functions the list draws with, so it cannot drift.
    /// </summary>
    public static class SpectrumColumnStyle
    {
        /// <summary>
        /// The accent for a column this data answers, and <paramref name="defaultColor"/> otherwise - the
        /// caller's own color, so the drawing context (a grid row, a block of text) keeps its usual look
        /// where nothing is being said.
        /// </summary>
        public static Color GetForeColor(SpectrumColumnScanner.Standing standing, Color defaultColor)
        {
            return standing == SpectrumColumnScanner.Standing.answerable ? SystemColors.HotTrack : defaultColor;
        }

        /// <summary>
        /// Italic for a column examined and not answered by this data, upright otherwise. Italic rather
        /// than graying: graying is the scheme's disabled treatment, and these entries remain selectable -
        /// a term absent from today's data is a reasonable thing to filter on for data yet to come.
        /// </summary>
        public static FontStyle GetFontStyle(SpectrumColumnScanner.Standing standing)
        {
            return standing == SpectrumColumnScanner.Standing.unanswerable ? FontStyle.Italic : FontStyle.Regular;
        }
    }
}
