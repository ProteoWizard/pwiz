using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ProfileMemory : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMemorySimple()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Do nothing.  We're evaluating memory leaks for starting and stopping Skyline.
        }

        [TestMethod]
        public void ListLeakTest()
        {
            // List allocates a static, zero-length array, which shows up in the SciTech
            // memory profiler as a potential leak.  Currently no way to hide it.
            // ReSharper disable once CollectionNeverUpdated.Local
            var x = new List<MyClass>();
            x.Clear();
        }

        private class MyClass
        {
        }
    }
}
