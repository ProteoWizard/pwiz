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
            = new DisplayModificationOption(@"not_shown", ()=>"Not Shown", (settings, mod) => string.Empty, modSeq=>modSeq.ToString());

        public static readonly DisplayModificationOption MASS_DELTA =
            new DisplayModificationOption(@"mass_delta", () => "Mass Difference",
                FormatMassDelta, modSeq=>modSeq.ToString());

        public static readonly DisplayModificationOption THREE_LETTER_CODE =
            new DisplayModificationOption(@"three_letter_code", () => "Three Letter Code",
                FormatThreeLetterCode, modSeq=>modSeq.ThreeLetterCodes);
        public static readonly DisplayModificationOption FULL_NAME =
            new DisplayModificationOption(@"full_name", () =>"Full Name", 
                FormatFullName, modSeq=>modSeq.FullNames);
        public static readonly DisplayModificationOption UNIMOD_ID =
            new DisplayModificationOption(@"unimod_id", ()=>"Unimod ID", FormatUnimodId, modSeq=>modSeq.UnimodIds);
        private Func<String> _menuItemText;
        private ModificationFormatter _modificationFormatter;
        private Func<ModifiedSequence, string> _modifiedSequenceFormatter;

        public DisplayModificationOption(string name, Func<String> menuItemText,
            ModificationFormatter modificationFormatter, Func<ModifiedSequence, string> modifiedSequenceFormatter)
        {
            Name = name;
            _menuItemText = menuItemText;
            _modificationFormatter = modificationFormatter;
            _modifiedSequenceFormatter = modifiedSequenceFormatter;
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
