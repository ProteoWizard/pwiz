/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Results;

namespace pwiz.SkylineTestA.Results
{
    /// <summary>
    /// Summary description for ResultsUtilTest
    /// </summary>
    [TestClass]
    public class ResultsUtilTest
    {
        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }

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
        public void TestBaseNameMatch()
        {
            Assert.IsTrue(MeasuredResults.IsBaseNameMatch("BaseName123", "BaseName123.wiff"));
            Assert.IsTrue(MeasuredResults.IsBaseNameMatch("BaseName123.c", "BaseName123"));
            Assert.IsTrue(MeasuredResults.IsBaseNameMatch("BaseName123.c", "BASENAME123"));
            Assert.IsTrue(MeasuredResults.IsBaseNameMatch("BASENAME123", "basename123"));
            Assert.IsFalse(MeasuredResults.IsBaseNameMatch("BaseName123", "BaseName123-wiff"));
            Assert.IsFalse(MeasuredResults.IsBaseNameMatch("BaseName123", "BaseName123_wiff"));
            Assert.IsFalse(MeasuredResults.IsBaseNameMatch("BaseName123", "BaseName1234"));
            Assert.IsFalse(MeasuredResults.IsBaseNameMatch("BaseName1234", "BaseName123"));
        }
    }
}