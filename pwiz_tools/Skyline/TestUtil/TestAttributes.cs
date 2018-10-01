using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;

namespace pwiz.SkylineTestUtil
{
    public class PerfTestAttribute : TestCategoryBaseAttribute
    {
        public const string CATEGORY_NAME = "Perf";
        public override IList<string> TestCategories => ImmutableList.Singleton(CATEGORY_NAME);
    }

    public class SmallMoleculesTestAttribute : TestCategoryBaseAttribute
    {
        public const string CATEGORY_NAME = "SmallMolecules";
        public override IList<string> TestCategories => ImmutableList.Singleton(CATEGORY_NAME);
    }
}
