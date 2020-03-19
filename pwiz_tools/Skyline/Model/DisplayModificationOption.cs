/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model
{
    public class DisplayModificationOption
    {
        public static readonly DisplayModificationOption NOT_SHOWN 
            = new DisplayModificationOption(@"not_shown", ()=>Resources.DisplayModificationOption_NOT_SHOWN_Not_Shown, (settings, mod) => string.Empty, modSeq=>modSeq.ToString(), true);

        public static readonly DisplayModificationOption MASS_DELTA =
            new DisplayModificationOption(@"mass_delta", () => Resources.DisplayModificationOption_MASS_DELTA_Mass_Difference,
                FormatMassDelta, modSeq=>modSeq.ToString(), true);

        public static readonly DisplayModificationOption THREE_LETTER_CODE =
            new DisplayModificationOption(@"three_letter_code", () => Resources.DisplayModificationOption_THREE_LETTER_CODE_Three_Letter_Code,
                FormatThreeLetterCode, modSeq=>modSeq.ThreeLetterCodes, false);
        public static readonly DisplayModificationOption FULL_NAME =
            new DisplayModificationOption(@"full_name", () =>Resources.DisplayModificationOption_FULL_NAME_Full_Name, 
                FormatFullName, modSeq=>modSeq.FullNames, false);
        public static readonly DisplayModificationOption UNIMOD_ID =
            new DisplayModificationOption(@"unimod_id", ()=>Resources.DisplayModificationOption_UNIMOD_ID_Unimod_ID, FormatUnimodId, modSeq=>modSeq.UnimodIds, false);
        private Func<String> _menuItemText;
        private ModificationFormatter _modificationFormatter;
        private Func<ModifiedSequence, string> _modifiedSequenceFormatter;

        public DisplayModificationOption(string name, Func<String> menuItemText,
            ModificationFormatter modificationFormatter, Func<ModifiedSequence, string> modifiedSequenceFormatter, bool ignoreZeroMassMods)
        {
            Name = name;
            _menuItemText = menuItemText;
            _modificationFormatter = modificationFormatter;
            _modifiedSequenceFormatter = modifiedSequenceFormatter;
            IgnoreZeroMassMods = ignoreZeroMassMods;
        }

        public String Name { get; private set; }

        public string MenuItemText
        {
            get { return _menuItemText(); }
        }

        public string GetModificationText(SrmSettings settings, IEnumerable<ModifiedSequence.Modification> modifications)
        {
            return _modificationFormatter(settings, modifications);
        }

        public string FormatModifiedSequence(ModifiedSequence modifiedSequence)
        {
            return _modifiedSequenceFormatter(modifiedSequence);
        }

        public bool IgnoreZeroMassMods { get; private set; }

        public static IEnumerable<DisplayModificationOption> All
        {
            get
            {
                return new[]
                {
                    NOT_SHOWN,
                    MASS_DELTA,
                    THREE_LETTER_CODE,
                    FULL_NAME,
                    UNIMOD_ID
                };
            }
        }

        public static DisplayModificationOption FromName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            return All.FirstOrDefault(opt => opt.Name == name);
        }

        public static DisplayModificationOption Current
        {
            get { return FromName(Settings.Default.DisplayModification) ?? NOT_SHOWN; }
            set { Settings.Default.DisplayModification = value?.Name; }
        }

        public delegate string ModificationFormatter(SrmSettings settings,
            IEnumerable<ModifiedSequence.Modification> modifications);


        public static string FormatMassDelta(SrmSettings settings, IEnumerable<ModifiedSequence.Modification> mods)
        {
            return ModifiedSequence.FormatMassModification(mods,
                settings.TransitionSettings.Prediction.PrecursorMassType, false);
        }

        public static string FormatThreeLetterCode(SrmSettings settings,
            IEnumerable<ModifiedSequence.Modification> mods)
        {
            return string.Join(string.Empty,
                mods.Select(mod => ModifiedSequence.Bracket(ModifiedSequence.FormatThreeLetterCode(mod))));
        }

        public static string FormatFullName(SrmSettings settings, IEnumerable<ModifiedSequence.Modification> mods)
        {
            return string.Join(string.Empty,
                mods.Select(mod => ModifiedSequence.Bracket(ModifiedSequence.FormatFullName(mod))));
        }

        public static string FormatUnimodId(SrmSettings settings, IEnumerable<ModifiedSequence.Modification> mods)
        {
            return ModifiedSequence.FormatUnimodIds(mods);
        }
    }
}
