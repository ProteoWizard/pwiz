/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.IO;
using System.Text;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Summary description for UtilTest
    /// </summary>
    [TestClass]
    public class UtilTest
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
        public void DsvHelperTest()
        {
            TestDsvFields(TextUtil.SEPARATOR_CSV, TextUtil.SEPARATOR_CSV);
            TestDsvFields(TextUtil.SEPARATOR_CSV, TextUtil.SEPARATOR_CSV_INTL);
            TestDsvFields(TextUtil.SEPARATOR_CSV_INTL, TextUtil.SEPARATOR_CSV);
            TestDsvFields(TextUtil.SEPARATOR_CSV_INTL, TextUtil.SEPARATOR_CSV_INTL);
            TestDsvFields(TextUtil.SEPARATOR_TSV, TextUtil.SEPARATOR_TSV);

            Assert.AreEqual("End in quote", "\"End in quote".ParseCsvFields()[0]);
            Assert.AreEqual("Internal quotes", "Intern\"al quot\"es,9.7".ParseCsvFields()[0]);
            Assert.AreEqual("Multiple \"quote\" blocks",
                "\"Mult\"iple \"\"\"quote\"\"\" bl\"ocks\",testing,#N/A".ParseCsvFields()[0]);
        }

        private static void TestDsvFields(char punctuation, char separator)
        {
            var fields = new[]
                             {
                                 "separator" + punctuation + " test", // Just separator
                                 "\"\"",    // Just quotes
                                 "separator" + punctuation + " \"quote" + punctuation + "\" test", // Quotes and separator
                             };
            var sb = new StringBuilder();
            var writer = new StringWriter(sb);
            foreach (string field in fields)
            {
                if (sb.Length > 0)
                    writer.Write(separator);
                writer.WriteDsvField(field, separator);
            }
            var fieldsOut = sb.ToString().ParseDsvFields(separator);
            Assert.IsTrue(ArrayUtil.EqualsDeep(fields, fieldsOut));
        }
    }
}