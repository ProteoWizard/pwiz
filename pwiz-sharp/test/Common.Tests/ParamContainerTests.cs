using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.Data.Common.Tests.Params;

[TestClass]
public class ParamContainerTests
{
    [TestMethod]
    public void IsEmpty_FreshContainer_True()
    {
        Assert.IsTrue(new ParamContainer().IsEmpty);
    }

    [TestMethod]
    public void Set_ThenCvParam_ReturnsStoredValue()
    {
        var pc = new ParamContainer();
        pc.Set(CVID.MS_software, "proteowizard");
        var p = pc.CvParam(CVID.MS_software);
        Assert.AreEqual("proteowizard", p.Value);
    }

    [TestMethod]
    public void Set_WithSameCvid_Updates()
    {
        var pc = new ParamContainer();
        pc.Set(CVID.MS_software, "v1");
        pc.Set(CVID.MS_software, "v2");
        Assert.AreEqual(1, pc.CVParams.Count);
        Assert.AreEqual("v2", pc.CvParam(CVID.MS_software).Value);
    }

    [TestMethod]
    public void CvParam_Missing_ReturnsEmpty()
    {
        var pc = new ParamContainer();
        var p = pc.CvParam(CVID.MS_software);
        Assert.IsTrue(p.IsEmpty);
    }

    [TestMethod]
    public void HasCVParam_ExistingTerm_True()
    {
        var pc = new ParamContainer();
        pc.Set(CVID.MS_software, "pwiz");
        Assert.IsTrue(pc.HasCVParam(CVID.MS_software));
    }

    [TestMethod]
    public void HasCVParam_MissingTerm_False()
    {
        Assert.IsFalse(new ParamContainer().HasCVParam(CVID.MS_software));
    }

    [TestMethod]
    public void UserParam_FoundByName()
    {
        var pc = new ParamContainer();
        pc.UserParams.Add(new UserParam("custom", "hello"));
        var u = pc.UserParam("custom");
        Assert.AreEqual("hello", u.Value);
    }

    [TestMethod]
    public void UserParam_Missing_ReturnsEmpty()
    {
        var pc = new ParamContainer();
        Assert.IsTrue(pc.UserParam("missing").IsEmpty);
    }

    [TestMethod]
    public void CvParamValueOrDefault_Present_ReturnsValue()
    {
        var pc = new ParamContainer();
        pc.Set(CVID.MS_scan_start_time, 3.5);
        Assert.AreEqual(3.5, pc.CvParamValueOrDefault(CVID.MS_scan_start_time, 0.0));
    }

    [TestMethod]
    public void CvParamValueOrDefault_Absent_ReturnsDefault()
    {
        var pc = new ParamContainer();
        Assert.AreEqual(99.0, pc.CvParamValueOrDefault(CVID.MS_scan_start_time, 99.0));
    }

    [TestMethod]
    public void ParamGroup_RecursiveLookup_Works()
    {
        var pg = new ParamGroup("group1");
        pg.Set(CVID.MS_software, "inner");

        var pc = new ParamContainer();
        pc.ParamGroups.Add(pg);

        var p = pc.CvParam(CVID.MS_software);
        Assert.AreEqual("inner", p.Value);
        Assert.IsTrue(pc.HasCVParam(CVID.MS_software));
    }

    [TestMethod]
    public void Clear_EmptiesContainer()
    {
        var pc = new ParamContainer();
        pc.Set(CVID.MS_software, "a");
        pc.UserParams.Add(new UserParam("u", "v"));
        pc.Clear();
        Assert.IsTrue(pc.IsEmpty);
    }

    [TestMethod]
    public void Equality_SameParams_Equal()
    {
        var a = new ParamContainer();
        a.Set(CVID.MS_software, "pwiz");
        var b = new ParamContainer();
        b.Set(CVID.MS_software, "pwiz");
        Assert.AreEqual(a, b);
    }

    [TestMethod]
    public void Equality_DifferentParams_NotEqual()
    {
        var a = new ParamContainer();
        a.Set(CVID.MS_software, "v1");
        var b = new ParamContainer();
        b.Set(CVID.MS_software, "v2");
        Assert.AreNotEqual(a, b);
    }
}
