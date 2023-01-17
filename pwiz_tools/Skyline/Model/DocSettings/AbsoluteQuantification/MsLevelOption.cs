using System;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class MsLevelOption : LabeledValues<string>
    {
        private MsLevelOption(string name, Func<string> getLabelFunc) : base(name, getLabelFunc)
        {
        }

        public static readonly MsLevelOption ALL = new MsLevelOption("", () => QuantificationStrings.MsLevelOption_ALL_All);
        public static readonly MsLevelOption ONE = new MsLevelOption(@"1", () => 1.ToString());
        public static readonly MsLevelOption TWO = new MsLevelOption(@"2", () => 2.ToString());
        public static readonly MsLevelOption DEFAULT = new MsLevelOption(@"default", () => QuantificationStrings.MsLevelOption_DEFAULT_Default);

        public static MsLevelOption ForName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return ALL;
            }

            return AllOptions.FirstOrDefault(option => option.Name == name) ?? ALL;
        }

        public static readonly ImmutableList<MsLevelOption> AllOptions = ImmutableList.ValueOf(new[]
        {
            DEFAULT, ALL, ONE, TWO
        });

        public override string ToString()
        {
            return Label;
        }
    }
}
