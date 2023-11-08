using System;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Results.Scoring
{
    public class MissingScoreBehavior
    {
        public static MissingScoreBehavior FAIL = new MissingScoreBehavior(@"fail", () => "Fail");
        public static MissingScoreBehavior REPLACE = new MissingScoreBehavior(@"replace", () => "Replace");
        public static MissingScoreBehavior SKIP = new MissingScoreBehavior(@"skip", () => "Skip");

        private MissingScoreBehavior(string name, Func<string> getLabelFunc)
        {
            Name = name;
            _getLabelFunc = getLabelFunc;
        }

        private readonly Func<string> _getLabelFunc;
        public string Name { get; }
        public string Label
        {
            get { return _getLabelFunc(); }
        }
        public override string ToString()
        {
            return Label;
        }

        public static readonly ImmutableList<MissingScoreBehavior> ALL = ImmutableList.ValueOf(new []{FAIL, REPLACE, SKIP});

        public static MissingScoreBehavior FromName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return FAIL;
            }

            return ALL.FirstOrDefault(x => x.Name == name) ?? FAIL;
        }
    }
}
