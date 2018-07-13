using System;
using pwiz.Common.Collections;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.DocSettings
{
    public class FullScanAcquisitionMethod
    {
        public static readonly FullScanAcquisitionMethod None = new FullScanAcquisitionMethod("None", ()=>Resources.FullScanAcquisitionExtension_LOCALIZED_VALUES_None);
        public static readonly FullScanAcquisitionMethod Targeted = new FullScanAcquisitionMethod("Targeted", ()=>Resources.FullScanAcquisitionExtension_LOCALIZED_VALUES_Targeted);

        public static readonly FullScanAcquisitionMethod DIA = new FullScanAcquisitionMethod("DIA",
            () => Resources.FullScanAcquisitionExtension_LOCALIZED_VALUES_DIA);
        public static readonly FullScanAcquisitionMethod DDA = new FullScanAcquisitionMethod("DDA", ()=>"DDA");

        public static readonly ImmutableList<FullScanAcquisitionMethod> ALL =
            ImmutableList.ValueOf(new[] {None, Targeted, DIA, DDA});

        private readonly Func<string> _getLabelFunc;
        private FullScanAcquisitionMethod(string name, Func<string> getLabelFunc)
        {
            Name = name;
            _getLabelFunc = getLabelFunc;
        }

        public string Name {get; private set; }
        public string Label { get { return _getLabelFunc(); } }

        public override string ToString()
        {
            return Label;
        }

        public static FullScanAcquisitionMethod FromName(string name)
        {
            foreach (var method in ALL)
            {
                if (method.Name == name)
                {
                    return method;
                }
            }
            return None;
        }

        public static FullScanAcquisitionMethod FromLegacyName(string legacyName)    // Skyline 1.2 and earlier // Not L10N
        {
            if (legacyName == null)
            {
                return null;
            }
            if (legacyName == "Single")
            {
                return Targeted;
            }
            if (legacyName == "Multiple")
            {
                return DIA;
            }
            return None;
        }
    }
}