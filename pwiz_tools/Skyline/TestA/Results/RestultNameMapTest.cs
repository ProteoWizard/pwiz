/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA.Results
{
    /// <summary>
    /// Summary description for RestultNameMapTest
    /// </summary>
    [TestClass]
    public class RestultNameMapTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestSearchForDotOrUnderscore()
        {
            var formatInfo = DateTimeFormatInfo.InvariantInfo;
            // for test data, let's just use the names of the months and the days of the week
            var testNames = formatInfo.DayNames
                .Concat(formatInfo.AbbreviatedDayNames)
                .Concat(formatInfo.MonthNames)
                .Concat(formatInfo.AbbreviatedMonthNames).ToArray();
            var resultNameMap = ResultMapFromStrings(testNames);
            // Test case insensitivity
            Assert.AreEqual("January", resultNameMap.Find("january"));
            Assert.AreEqual("Jan", resultNameMap.Find("jAN"));
            Assert.IsNull(resultNameMap.Find("Janu"));
            // Test when searching for something ending with "_final_fragment"
            Assert.IsNull(resultNameMap.Find("jan_"));
            Assert.AreEqual("Jan", resultNameMap.Find("jan_January_final_fragment"));
            Assert.IsNull(resultNameMap.Find("jan_January_final_Fragment"));
            // Test when searching for something ending with a dot.
            Assert.AreEqual("Jan", resultNameMap.Find("jAn.foo"));
            Assert.AreEqual("January", resultNameMap.Find("january.foo"));
        }

        [TestMethod]
        public void TestNamesWithDotsAndUnderscores()
        {
            var formatInfo = DateTimeFormatInfo.InvariantInfo;
            var testNames = formatInfo.DayNames
                .Concat(formatInfo.AbbreviatedDayNames.Select(s => s + "_final_fragment"))
                .Concat(formatInfo.MonthNames)
                .Concat(formatInfo.AbbreviatedMonthNames.Select(s => s + ".ext")).ToArray();
            var resultNameMap = ResultMapFromStrings(testNames);
            Assert.AreEqual("Tue_final_fragment", resultNameMap.Find("Tue"));
            Assert.IsNull(resultNameMap.FindExact("Tue"));

            Assert.AreEqual("Tue_final_fragment", resultNameMap.Find("Tue_final_fragment"));
            Assert.AreEqual("Tue_final_fragment", resultNameMap.FindExact("tuE_final_fragment"));

            Assert.AreEqual("Jan.ext", resultNameMap.Find("jan"));
            Assert.IsNull(resultNameMap.FindExact("jan"));
            Assert.AreEqual("Jan.ext", resultNameMap.Find("jan.ext"));
            Assert.AreEqual("Jan.ext", resultNameMap.FindExact("jan.ext"));
        }

        private static ResultNameMap<string> ResultMapFromStrings(IEnumerable<string> strings)
        {
            return ResultNameMap.FromKeyValuePairs(strings.Select(s => new KeyValuePair<string, string>(s, s)));
        }
    }
}
