/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

using System.Linq;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA.Results
{
    /// <summary>
    /// Summary description for MultiLabelTest
    /// </summary>
    [TestClass]
    public class MultiLabelRatioTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestCustomReportsCompatibility()
        {
            TestSmallMolecules = false; // Mixed molecule docs create different report columns
            var myType = typeof (MultiLabelRatioTest);
            var document = ResultsUtil.DeserializeDocument("MultiLabel.sky", myType);
            // ReSharper disable once AssignNullToNotNullAttribute
            var customReports = (ReportSpecList)new XmlSerializer(typeof(ReportSpecList)).Deserialize(
                myType.Assembly.GetManifestResourceStream(myType, "MultiLabelCustomReports.skyr"));
            Assert.AreNotEqual(0, customReports.Count);
            using (var checkReportCompatibility = new CheckReportCompatibility(document))
            {
                checkReportCompatibility.CheckAll();
                foreach (var customReport in customReports)
                {
                    checkReportCompatibility.CheckReport(customReport);
                }
            }
        }

        [TestMethod]
        public void TestParsePropertyParts()
        {
            var allStringsToTest = new[] {"one", "two", "_", "___", "a_b", "a_", "_x", "a b", "", ":", "::", "_:", ":_"};
            foreach (string prefix in allStringsToTest)
            {
                foreach (string firstPart in allStringsToTest)
                {
                    string nameWithOnePart = RatioPropertyDescriptor.MakePropertyName(prefix, firstPart);
                    CollectionAssert.AreEqual(new[]{firstPart}, RatioPropertyDescriptor.ParsePropertyParts(nameWithOnePart, prefix).ToArray(), 
                        "Parsing {0} with prefix {1} did not yield part {2}", nameWithOnePart, prefix, firstPart);
                    // Adding an underscore to the end of name should be invalid
                    Assert.IsNull(RatioPropertyDescriptor.ParsePropertyParts(nameWithOnePart + "_", prefix));
                    foreach (string secondPart in allStringsToTest)
                    {
                        string nameWithTwoParts = RatioPropertyDescriptor.MakePropertyName(prefix, firstPart, secondPart);
                        CollectionAssert.AreEqual(new[] {firstPart, secondPart},
                            RatioPropertyDescriptor.ParsePropertyParts(nameWithTwoParts, prefix).ToArray(),
                            "Parsing {0} with prefix {1} did not yield parts {2} and {3}", nameWithTwoParts, prefix, firstPart, secondPart);
                        Assert.IsNull(RatioPropertyDescriptor.ParsePropertyParts(nameWithTwoParts + '_', prefix));
                    }
                }
            }
        }
    }
}
