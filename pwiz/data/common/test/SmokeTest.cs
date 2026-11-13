namespace Pwiz.Data.Common.Tests;

[TestClass]
public class SmokeTest
{
    [TestMethod]
    public void AssemblyLoads()
    {
        Assert.AreEqual("Pwiz.Data.Common", typeof(Pwiz.Data.Common.AssemblyInfo).Assembly.GetName().Name);
    }
}
