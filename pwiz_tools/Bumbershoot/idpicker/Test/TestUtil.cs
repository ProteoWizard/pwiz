//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2012 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using IDPicker;

namespace Test
{
    /// <summary>
    /// Summary description for TestUtil
    /// </summary>
    [TestClass]
    public class TestUtil
    {
        public TestUtil (){}
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
        public void TestGetCommonFilename ()
        {
            string[] input;

            input = new string[] {"D:/testfoo.pepXML", "D:/testbar.pepXML"};
            Assert.AreEqual("D:/test.idpDB", Util.GetCommonFilename(input));

            input = new string[] { "D:/test/some/more/foo.pepXML", "D:/test/some/thing/bar.pepXML" };
            Assert.AreEqual("D:/test/some/some.idpDB", Util.GetCommonFilename(input));

            input = new string[] { "foo.pepXML", "foo.idpDB" };
            Assert.AreEqual("foo.idpDB", Util.GetCommonFilename(input));

            input = new string[] { "foo", "bar" };
            StringAssert.StartsWith(Util.GetCommonFilename(input), "idpicker-analysis-");

            input = new string[] { "D:/foo.pepXML", "D:/bar.pepXML" };
            StringAssert.StartsWith(Util.GetCommonFilename(input), "D:/idpicker-analysis-");
        }
    }
}
