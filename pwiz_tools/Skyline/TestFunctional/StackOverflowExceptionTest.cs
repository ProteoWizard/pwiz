using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Test which causes a StackOverflowException to be thrown, causing TestRunner.exe to exit immediately.
    ///
    /// This test should not be merged into the master branch. The only purpose of this test is to make sure that TeamCity is properly able
    /// to see that the tests have failed when this test is part of the suite.
    /// </summary>
    [TestClass]
    public class StackOverflowExceptionTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestStackOverflowException()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>OverflowStack());
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void OverflowStack()
        {
            OverflowStack();
        }
    }
}
