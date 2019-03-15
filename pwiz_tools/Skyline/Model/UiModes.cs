using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model
{
    public static class UiModes
    {
        public const string PROTEOMIC = "proteomic";
        public const string SMALL_MOLECULES = "small_molecules";
        public const string MIXED = "mixed";

        public static readonly IEnumerable<string> ALL =
            ImmutableList.ValueOf(new[] {PROTEOMIC, SMALL_MOLECULES, MIXED});

        public static string FromDocumentType(SrmDocument.DOCUMENT_TYPE documentType)
        {
            switch (documentType)
            {
                case SrmDocument.DOCUMENT_TYPE.proteomic:
                    return PROTEOMIC;
                case SrmDocument.DOCUMENT_TYPE.small_molecules:
                    return SMALL_MOLECULES;
                default:
                    return MIXED;
            }
        }
    }
}
