namespace Pwiz.Util.Tests;

[TestClass]
public class SmokeTest
{
    [TestMethod]
    public void AssemblyLoads()
    {
        Assert.AreEqual("Pwiz.Util", typeof(Pwiz.Util.AssemblyInfo).Assembly.GetName().Name);
    }
}
