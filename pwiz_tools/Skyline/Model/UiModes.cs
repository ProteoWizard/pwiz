using System.Collections.Generic;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model
{
    public static class UiModes
    {
        public const string PROTEOMIC = "proteomic";
        public const string SMALL_MOLECULES = "small_molecules";
        public const string MIXED = "mixed";

        public static readonly IEnumerable<string> ALL =
            ImmutableList.ValueOf(new[] {PROTEOMIC, SMALL_MOLECULES, MIXED});

        public static UiMode FromDocumentType(SrmDocument.DOCUMENT_TYPE documentType)
        {
            switch (documentType)
            {
                case SrmDocument.DOCUMENT_TYPE.proteomic:
                    return Proteomic;
                case SrmDocument.DOCUMENT_TYPE.small_molecules:
                    return SmallMolecule;
                default:
                    return Mixed;
            }
        }

        public static readonly UiMode Proteomic = new UiMode(PROTEOMIC, Resources.UIModeProteomic,
            () => Resources.ModeUIAwareFormHelper_SetModeUIToolStripButtons_Proteomics_interface);

        public static readonly UiMode SmallMolecule = new UiMode(SMALL_MOLECULES, Resources.UIModeSmallMolecules,
            () => Resources.ModeUIAwareFormHelper_SetModeUIToolStripButtons_Small_Molecules_interface);

        public static readonly UiMode Mixed = new UiMode(MIXED, Resources.UIModeMixed,
            () => Resources.ModeUIAwareFormHelper_SetModeUIToolStripButtons_Mixed_interface);

        public static readonly ImmutableList<UiMode> AllModes = ImmutableList.ValueOf(new []{Proteomic, SmallMolecule, Mixed});
    }
}
