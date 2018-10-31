/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.SkylineTestUtil;

namespace CommonTest.DataBinding
{
    [TestClass]
    public class FilterSpecTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestFilterSpecRoundTrips()
        {
            var propertyPath = PropertyPath.Root.Property("property");
            var invariantDataSchema = new DataSchema();
            foreach (var cultureInfo in ListTestCultureInfos())
            {
                var dataSchema = new DataSchema(new DataSchemaLocalizer(cultureInfo, cultureInfo));
                foreach (var testOperand in ListTestOperands())
                {
                    var columnType = testOperand.GetType();
                    var filterPredicate = FilterPredicate.CreateFilterPredicate(dataSchema, columnType,
                        FilterOperations.OP_EQUALS, ValueToString(testOperand, cultureInfo));
                    var invariantFilterPredicate = FilterPredicate.CreateFilterPredicate(invariantDataSchema, columnType,
                        FilterOperations.OP_EQUALS, ValueToString(testOperand, CultureInfo.InvariantCulture));
                    Assert.AreEqual(invariantFilterPredicate, filterPredicate);
                    var filterSpec = new FilterSpec(propertyPath, filterPredicate);
                    var predicateOperandValue = filterSpec.Predicate.GetOperandValue(dataSchema, columnType);
                    var expectedOperandValue = predicateOperandValue is double
                        ? Convert.ChangeType(testOperand, typeof (double))
                        : testOperand;
                    Assert.AreEqual(expectedOperandValue, predicateOperandValue);
                    Assert.AreEqual(ValueToString(testOperand, cultureInfo), filterSpec.Predicate.GetOperandDisplayText(dataSchema, columnType));
                    var filterSpecRoundTrip = RoundTripToXml(filterSpec);
                    Assert.AreEqual(expectedOperandValue, filterSpecRoundTrip.Predicate.GetOperandValue(dataSchema, columnType));
                    Assert.AreEqual(ValueToString(testOperand, cultureInfo), filterSpecRoundTrip.Predicate.GetOperandDisplayText(dataSchema, columnType));
                }
            }
        }

        private IEnumerable<CultureInfo> ListTestCultureInfos()
        {
            return new[]
            {
                LocalizationHelper.CurrentCulture,
                CultureInfo.GetCultureInfo("en-US"), 
                CultureInfo.GetCultureInfo("fr-FR"),
                CultureInfo.GetCultureInfo("tr-TR")
            };
        }

        private IEnumerable<object> ListTestOperands()
        {
            var baseOperands = new object[]
            {
                true,
                false,
                0,
                1,
                1f,
                1.0,
                1.5,
                2.7e16,
                2.7e-16,
                "string1",
                'x',
            };
            return baseOperands.Concat(baseOperands.Select(ToNullable));
        }

        private object ToNullable(object value)
        {
            if (!value.GetType().IsValueType)
            {
                return value;
            }
            var nullableType = typeof (Nullable<>).MakeGenericType(value.GetType());
            var constructor = nullableType.GetConstructor(new[] {value.GetType()});
            Assert.IsNotNull(constructor);
            return constructor.Invoke(new[] {value});
        }

        private string ValueToString(object value, CultureInfo cultureInfo)
        {
            var oldCultureInfo = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = cultureInfo;
                return value.ToString();
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = oldCultureInfo;
            }
        }

        private FilterSpec RoundTripToXml(FilterSpec filterSpec)
        {
            var viewSpec = new ViewSpec().SetFilters(new[] {filterSpec});
            var xmlSerializer = new XmlSerializer(typeof(ViewSpecList));
            var memoryStream = new MemoryStream();
            xmlSerializer.Serialize(memoryStream, new ViewSpecList(new []{viewSpec}));
            memoryStream.Seek(0, SeekOrigin.Begin);
            var roundTripViewSpecList = (ViewSpecList) xmlSerializer.Deserialize(memoryStream);
            return roundTripViewSpecList.ViewSpecs.First().Filters.First();
        }
    }
}
