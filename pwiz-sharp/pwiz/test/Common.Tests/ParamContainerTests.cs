using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.Data.Common.Tests.Params;

/// <summary>
/// Tests <see cref="ParamContainer"/> behavior. Methods are grouped by behavior class:
/// container lifecycle, CV-param storage / lookup / fallback, user params, ParamGroup
/// recursion, and equality.
/// </summary>
[TestClass]
public class ParamContainerTests
{
    [TestMethod]
    public void Lifecycle_EmptyAndClear()
    {
        // Fresh container has IsEmpty == true.
        Assert.IsTrue(new ParamContainer().IsEmpty);

        // Clear() empties even after CV + user params have been added.
        var pc = new ParamContainer();
        pc.Set(CVID.MS_software, "a");
        pc.UserParams.Add(new UserParam("u", "v"));
        pc.Clear();
        Assert.IsTrue(pc.IsEmpty);
    }

    [TestMethod]
    public void CvParams_StoreUpdateLookupAndFallback()
    {
        var pc = new ParamContainer();

        // HasCVParam: empty → false.
        Assert.IsFalse(pc.HasCVParam(CVID.MS_software));

        // Set + CvParam round-trip the value; HasCVParam → true.
        pc.Set(CVID.MS_software, "proteowizard");
        Assert.IsTrue(pc.HasCVParam(CVID.MS_software));
        Assert.AreEqual("proteowizard", pc.CvParam(CVID.MS_software).Value);

        // Re-Set with the same CVID updates in place rather than appending.
        pc.Set(CVID.MS_software, "v2");
        Assert.AreEqual(1, pc.CVParams.Count, "duplicate CVID should update, not append");
        Assert.AreEqual("v2", pc.CvParam(CVID.MS_software).Value);

        // Looking up a missing CVID returns an empty CVParam (not null).
        Assert.IsTrue(new ParamContainer().CvParam(CVID.MS_software).IsEmpty);

        // CvParamValueOrDefault: returns the actual value when present, the fallback otherwise.
        var withTime = new ParamContainer();
        withTime.Set(CVID.MS_scan_start_time, 3.5);
        Assert.AreEqual(3.5, withTime.CvParamValueOrDefault(CVID.MS_scan_start_time, 0.0), "present");
        Assert.AreEqual(99.0,
            new ParamContainer().CvParamValueOrDefault(CVID.MS_scan_start_time, 99.0), "absent");
    }

    [TestMethod]
    public void UserParam_FoundByName_OrEmpty()
    {
        var pc = new ParamContainer();
        pc.UserParams.Add(new UserParam("custom", "hello"));
        Assert.AreEqual("hello", pc.UserParam("custom").Value);
        Assert.IsTrue(pc.UserParam("missing").IsEmpty);
    }

    [TestMethod]
    public void ParamGroup_RecursiveLookup()
    {
        // CV lookups recurse into ParamGroups attached to the container.
        var pg = new ParamGroup("group1");
        pg.Set(CVID.MS_software, "inner");
        var pc = new ParamContainer();
        pc.ParamGroups.Add(pg);

        Assert.AreEqual("inner", pc.CvParam(CVID.MS_software).Value);
        Assert.IsTrue(pc.HasCVParam(CVID.MS_software));
    }

    [TestMethod]
    public void Equality_BySetMembership()
    {
        var a = new ParamContainer();
        a.Set(CVID.MS_software, "pwiz");

        var sameAsA = new ParamContainer();
        sameAsA.Set(CVID.MS_software, "pwiz");

        var differentValue = new ParamContainer();
        differentValue.Set(CVID.MS_software, "v2");

        Assert.AreEqual(a, sameAsA);
        Assert.AreNotEqual(a, differentValue);
    }
}
