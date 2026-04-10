using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Test
{
    /// <summary>
    /// Tests for core Osprey types.
    /// </summary>
    [TestClass]
    public class CoreTypesTest
    {
        [TestMethod]
        public void TestLibraryEntryCreation()
        {
            var entry = new LibraryEntry();
            Assert.IsNotNull(entry);
        }
    }
}
