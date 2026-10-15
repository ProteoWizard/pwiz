/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Databinding;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// The color of the flag drawn in the Targets tree for a node with a note.
    /// Wraps <see cref="Annotations.ColorIndex"/> so that the Document Grid can show it by name.
    /// </summary>
    public sealed record NoteColor : IAuditLogObject
    {
        private static readonly string[] LOCALIZED_CULTURE_NAMES = { @"ja", @"zh-Hans" };

        public static readonly NoteColor ORANGE_RED = new NoteColor(0, @"Orange Red", nameof(ModelResources.NoteColor_ORANGE_RED_Orange_Red));
        public static readonly NoteColor BROWN = new NoteColor(1, @"Brown", nameof(ModelResources.NoteColor_BROWN_Brown));
        public static readonly NoteColor PINK = new NoteColor(2, @"Pink", nameof(ModelResources.NoteColor_PINK_Pink));
        public static readonly NoteColor DARK_YELLOW = new NoteColor(3, @"Dark Yellow", nameof(ModelResources.NoteColor_DARK_YELLOW_Dark_Yellow));
        public static readonly NoteColor BRIGHT_GREEN = new NoteColor(4, @"Bright Green", nameof(ModelResources.NoteColor_BRIGHT_GREEN_Bright_Green));
        public static readonly NoteColor GREEN = new NoteColor(5, @"Green", nameof(ModelResources.NoteColor_GREEN_Green));
        public static readonly NoteColor BRIGHT_BLUE = new NoteColor(6, @"Bright Blue", nameof(ModelResources.NoteColor_BRIGHT_BLUE_Bright_Blue));
        public static readonly NoteColor BLUE = new NoteColor(7, @"Blue", nameof(ModelResources.NoteColor_BLUE_Blue));
        public static readonly NoteColor BLACK = new NoteColor(8, @"Black", nameof(ModelResources.NoteColor_BLACK_Black));
        public static readonly NoteColor PURPLE = new NoteColor(9, @"Purple", nameof(ModelResources.NoteColor_PURPLE_Purple));

        private static readonly ImmutableList<NoteColor> ALL = ImmutableList.ValueOf(new[]
        {
            ORANGE_RED, BROWN, PINK, DARK_YELLOW, BRIGHT_GREEN, GREEN, BRIGHT_BLUE, BLUE, BLACK, PURPLE
        });

        public static IList<NoteColor> ListNoteColors()
        {
            return ALL;
        }

        /// <summary>
        /// Returns the color that the Targets tree draws for a color index, which treats
        /// -1 (no color chosen) as the first color.
        /// </summary>
        public static NoteColor FromIndex(int colorIndex)
        {
            return ALL[Math.Max(0, Math.Min(ALL.Count - 1, colorIndex))];
        }

        /// <summary>
        /// Finds the color with the given invariant name, or with the given title in English
        /// or any language Skyline is localized to. Case-insensitive.
        /// </summary>
        public static NoteColor Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            text = text.Trim();
            return ALL.FirstOrDefault(noteColor => noteColor.ListAllNames()
                .Any(name => string.Equals(name, text, StringComparison.CurrentCultureIgnoreCase)));
        }

        /// <summary>
        /// The note that is given to a node which has a color but no note, since the flag is
        /// only drawn and saved for nodes which have a note or an annotation.
        /// </summary>
        public static string PlaceholderNote
        {
            get { return ModelResources.NoteColor_PlaceholderNote_Flagged; }
        }

        /// <summary>
        /// True if the note is <see cref="PlaceholderNote"/> in any language, so that it can be
        /// removed along with the color even if the language has changed since it was set.
        /// </summary>
        public static bool IsPlaceholderNote(string note)
        {
            if (note == null)
            {
                return false;
            }
            return ListLocalizations(nameof(ModelResources.NoteColor_PlaceholderNote_Flagged)).Contains(note);
        }

        private readonly string _titleResourceName;

        private NoteColor(int index, string name, string titleResourceName)
        {
            Index = index;
            Name = name;
            _titleResourceName = titleResourceName;
        }

        public int Index { get; }
        public string Name { get; }
        public string Title
        {
            get { return ModelResources.ResourceManager.GetString(_titleResourceName, ModelResources.Culture); }
        }

        public Color Color
        {
            get { return ((SolidBrush) Annotations.COLOR_BRUSHES[Index]).Color; }
        }

        public override string ToString()
        {
            if (CultureInfo.InvariantCulture.Equals(CultureInfo.CurrentCulture))
            {
                // When exporting the "Invariant" report for external tools,
                // we always use the persisted Name.
                return Name;
            }
            return Title;
        }

        public string AuditLogText
        {
            get { return Title; }
        }

        public bool IsName
        {
            get { return true; }
        }

        private IEnumerable<string> ListAllNames()
        {
            return ListLocalizations(_titleResourceName).Prepend(Name).Append(Title);
        }

        private static IEnumerable<string> ListLocalizations(string resourceName)
        {
            return LOCALIZED_CULTURE_NAMES.Select(cultureName => new CultureInfo(cultureName))
                .Prepend(CultureInfo.InvariantCulture)
                .Select(culture => ModelResources.ResourceManager.GetString(resourceName, culture))
                .Where(value => !string.IsNullOrEmpty(value));
        }

        /// <summary>
        /// Writes the invariant name to annotation files, and reads either the invariant name or a
        /// localized title.
        /// </summary>
        public class PropertyFormatter : IPropertyFormatter
        {
            public object ParseValue(CultureInfo cultureInfo, string text)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return null;
                }
                var noteColor = Parse(text);
                if (noteColor == null)
                {
                    throw new FormatException(string.Format(ModelResources.NoteColor_PropertyFormatter_ParseValue__0__is_not_a_valid_note_color__Valid_colors_are___1_,
                        text, string.Join(CultureInfo.CurrentCulture.TextInfo.ListSeparator + @" ", ALL.Select(c => c.Title))));
                }
                return noteColor;
            }

            public string FormatValue(CultureInfo cultureInfo, object value)
            {
                return ((NoteColor) value)?.Name;
            }
        }
    }
}
