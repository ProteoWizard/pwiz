/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class FormattableListTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestFormattableList()
        {
            Assert.IsTrue(typeof(IFormattable).IsAssignableFrom(typeof(FormattableList<double>)));
            var doubles = new[] { Math.PI, Math.E };
            var list = new FormattableList<double>(doubles);
            Assert.IsInstanceOfType(list, typeof(IFormattable));

            Assert.AreEqual(list.ToString(), Convert.ToString(list));
            Assert.AreEqual(string.Join(TextUtil.CsvSeparator.ToString(), doubles.Select(Convert.ToString)), Convert.ToString(list));
            Assert.AreEqual(string.Join(TextUtil.CsvSeparator.ToString(), doubles.Select(d=>d.ToString(Formats.Percent, CultureInfo.CurrentCulture))),
                list.ToString(Formats.Percent, CultureInfo.CurrentCulture));
            foreach (var cultureInfo in GetTestCultureInfos())
            {
                Assert.AreEqual(list.ToString(null, cultureInfo), Convert.ToString(list, cultureInfo));
                var csvSeparator = TextUtil.GetCsvSeparator(cultureInfo).ToString();
                Assert.AreEqual(string.Join(csvSeparator, doubles.Select(d=>Convert.ToString(d, cultureInfo))),
                    Convert.ToString(list, cultureInfo));
                Assert.AreEqual(string.Join(csvSeparator, doubles.Select(d=>d.ToString(Formats.Percent, cultureInfo))),
                    list.ToString(Formats.Percent, cultureInfo));
            }
        }

        [TestMethod]
        public void TestListColumnValue()
        {
            Assert.AreEqual(TextUtil.CsvSeparator, ListColumnValue.GetCsvSeparator(CultureInfo.CurrentCulture));
            Assert.IsFalse(typeof(IFormattable).IsAssignableFrom(typeof(ListColumnValue<string>)));
            var strings = new[] { "Hello", "World" };
            var stringListColumnValue = ListColumnValue.FromItems(strings);
            Assert.AreEqual(stringListColumnValue.ToString(), Convert.ToString(stringListColumnValue));
            Assert.AreEqual(string.Join(TextUtil.CsvSeparator.ToString(), strings), Convert.ToString(stringListColumnValue));
            var formattableStringList = stringListColumnValue as IFormattable;
            Assert.IsNotNull(formattableStringList);
            Assert.AreEqual(stringListColumnValue.ToString(), formattableStringList.ToString(null, CultureInfo.CurrentCulture));
            Assert.AreEqual(stringListColumnValue.ToString(), formattableStringList.ToString(Formats.Percent, CultureInfo.CurrentCulture));

            var doubles = new[] { Math.PI, Math.E };
            var doublesListColumnValue = ListColumnValue.FromItems(doubles);
            Assert.AreEqual(string.Join(TextUtil.CsvSeparator.ToString(),
                doubles.Select(Convert.ToString)), Convert.ToString(doublesListColumnValue));

            var formattableDoublesList = doublesListColumnValue as IFormattable;
            Assert.IsNotNull(formattableDoublesList);
            // Verify that format argument is ignored
            Assert.AreEqual(formattableDoublesList.ToString(null, CultureInfo.CurrentCulture),
                formattableDoublesList.ToString(Formats.Percent, CultureInfo.CurrentCulture));

            foreach (var cultureInfo in GetTestCultureInfos())
            {
                var csvSeparator = TextUtil.GetCsvSeparator(cultureInfo).ToString();
                Assert.AreEqual(string.Join(csvSeparator, strings),
                    Convert.ToString(stringListColumnValue, cultureInfo));
                Assert.AreEqual(Convert.ToString(stringListColumnValue, cultureInfo), formattableStringList.ToString(null, cultureInfo));
                Assert.AreEqual(Convert.ToString(stringListColumnValue, cultureInfo), formattableStringList.ToString(Formats.Percent, cultureInfo));
                

                Assert.AreEqual(string.Join(csvSeparator, doubles.Select(d=>Convert.ToString(d, cultureInfo))),
                    Convert.ToString(doublesListColumnValue, cultureInfo));
                Assert.AreEqual(Convert.ToString(doublesListColumnValue, cultureInfo),
                    formattableDoublesList.ToString(null, cultureInfo));
                Assert.AreEqual(Convert.ToString(doublesListColumnValue, cultureInfo),
                    formattableDoublesList.ToString(Formats.Percent, cultureInfo));
            }
        }

        private IList<CultureInfo> GetTestCultureInfos()
        {
            return new[]
            {
                CultureInfo.CurrentCulture,
                CultureInfo.InvariantCulture,
                CultureInfo.GetCultureInfo("en-US"),
                CultureInfo.GetCultureInfo("tr-TR"),
                CultureInfo.GetCultureInfo("fr-FR")
            };
        }
    }
}
