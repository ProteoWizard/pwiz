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
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
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
            Assert.AreEqual(string.Join(TextUtil.CsvSeparator.ToString(), doubles.Select(Convert.ToString)),
                Convert.ToString(list));
            Assert.AreEqual(string.Join(TextUtil.CsvSeparator.ToString(), doubles.Select(Convert.ToString)),
                Convert.ToString(list, CultureInfo.CurrentCulture));
            Assert.AreEqual(string.Join(TextUtil.SEPARATOR_CSV.ToString(),
                    doubles.Select(d => Convert.ToString(d, CultureInfo.InvariantCulture))),
                Convert.ToString(list, CultureInfo.InvariantCulture));
        }

        [TestMethod]
        public void TestListColumnValue()
        {
            Assert.AreEqual(TextUtil.CsvSeparator, ListColumnValue.GetCsvSeparator(CultureInfo.CurrentCulture));
            Assert.IsFalse(typeof(IFormattable).IsAssignableFrom(typeof(ListColumnValue<string>)));
            var strings = new[] { "Hello", "World" };
            var stringListColumnValue = ListColumnValue.FromItems(strings);
            Assert.IsInstanceOfType(stringListColumnValue, typeof(IFormattable));
            Assert.AreEqual(string.Join(TextUtil.CsvSeparator.ToString(), strings), Convert.ToString(stringListColumnValue));
            Assert.AreEqual(string.Join(TextUtil.CsvSeparator.ToString(), strings),
                Convert.ToString(stringListColumnValue, CultureInfo.CurrentCulture));
            Assert.AreEqual(string.Join(TextUtil.SEPARATOR_CSV.ToString(), strings),
                Convert.ToString(stringListColumnValue, CultureInfo.InvariantCulture));
            var doubles = new[] { Math.PI, Math.E };
            var doublesListColumnValue = ListColumnValue.FromItems(doubles);
            Assert.AreEqual(string.Join(TextUtil.CsvSeparator.ToString(),
                doubles.Select(v => v.ToString(CultureInfo.CurrentCulture))), Convert.ToString(doublesListColumnValue));
            Assert.AreEqual(string.Join(TextUtil.CsvSeparator.ToString(),
                    doubles.Select(v => v.ToString(CultureInfo.CurrentCulture))),
                Convert.ToString(doublesListColumnValue, CultureInfo.CurrentCulture));
            Assert.AreEqual(string.Join(TextUtil.SEPARATOR_CSV.ToString(),
                    doubles.Select(v => v.ToString(CultureInfo.CurrentCulture))),
                Convert.ToString(doublesListColumnValue, CultureInfo.InvariantCulture));
        }
    }
}
