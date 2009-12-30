using System;
using System.Collections;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Test.Util
{
    /// <summary>
    /// Summary description for PeakDividerTest
    /// </summary>
    [TestClass]
    public class PeakDividerTest
    {
        public PeakDividerTest()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void TestPeakDivider()
        {
            var values = new double[] {0, 1, 2, 3, 4, 3, 4, 5, 6, 5, 4, 3, 2, 3, 5};
            var peakDivider = new PeakDivider();
            var result = peakDivider.DividePeaks(values);
            Assert.AreEqual(6, result.Count);
            Assert.IsTrue(Lists.EqualsDeep(new[]{0, 4, 5, 12,13,14}, (IList) result));
        }
    }
}
