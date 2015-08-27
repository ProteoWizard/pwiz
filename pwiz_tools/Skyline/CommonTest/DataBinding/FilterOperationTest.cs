using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.SkylineTestUtil;

namespace CommonTest.DataBinding
{
    /// <summary>
    /// Summary description for FilterOperationTest
    /// </summary>
    [TestClass]
    public class FilterOperationTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestCharFilterOperations()
        {
            var alphabet = Enumerable.Range(0, 26).Select(i => (char) ('a' + i)).ToArray();
            VerifyFilterCountStructs(alphabet, FilterOperations.OP_IS_BLANK, null, 0);
            VerifyFilterCountStructs(alphabet, FilterOperations.OP_IS_NOT_BLANK, null, 26);
            VerifyFilterCountStructs(alphabet, FilterOperations.OP_EQUALS, "m", 1);
            VerifyFilterCountStructs(alphabet, FilterOperations.OP_NOT_EQUALS, "m", 25);
            VerifyFilterCountStructs(alphabet, FilterOperations.OP_IS_LESS_THAN, "m", 12);
            VerifyFilterCountStructs(alphabet, FilterOperations.OP_IS_LESS_THAN_OR_EQUAL, "m", 13);
            VerifyFilterCountStructs(alphabet, FilterOperations.OP_IS_GREATER_THAN, "m", 13);
            VerifyFilterCountStructs(alphabet, FilterOperations.OP_IS_GREATER_THAN_OR_EQUAL, "m", 14);
            Assert.IsFalse(IsValidForType<char>(FilterOperations.OP_STARTS_WITH));
            Assert.IsFalse(IsValidForType<char>(FilterOperations.OP_CONTAINS));
            Assert.IsFalse(IsValidForType<char>(FilterOperations.OP_NOT_CONTAINS));
        }

        [TestMethod]
        public void TestIntFilterOperations()
        {
            var ints = Enumerable.Range(-10, 21).ToArray();
            VerifyFilterCountStructs(ints, FilterOperations.OP_IS_BLANK, null, 0);
            VerifyFilterCountStructs(ints, FilterOperations.OP_IS_NOT_BLANK, null, 21);
            VerifyFilterCountStructs(ints, FilterOperations.OP_EQUALS, "3", 1);
            VerifyFilterCountStructs(ints, FilterOperations.OP_NOT_EQUALS, "3", 20);
            VerifyFilterCountStructs(ints, FilterOperations.OP_IS_LESS_THAN, "3", 13);
            VerifyFilterCountStructs(ints, FilterOperations.OP_IS_LESS_THAN, (3.5).ToString(CultureInfo.CurrentCulture), 14);
            VerifyFilterCountStructs(ints, FilterOperations.OP_IS_LESS_THAN_OR_EQUAL, "3", 14);
            VerifyFilterCountStructs(ints, FilterOperations.OP_IS_LESS_THAN_OR_EQUAL, (3.5).ToString(CultureInfo.CurrentCulture), 14);
            VerifyFilterCountStructs(ints, FilterOperations.OP_IS_GREATER_THAN, "3", 7);
            VerifyFilterCountStructs(ints, FilterOperations.OP_IS_GREATER_THAN_OR_EQUAL, "3", 8);
            Assert.IsFalse(IsValidForType<int>(FilterOperations.OP_STARTS_WITH));
            Assert.IsFalse(IsValidForType<int>(FilterOperations.OP_CONTAINS));
            Assert.IsFalse(IsValidForType<int>(FilterOperations.OP_NOT_CONTAINS));
        }

        [TestMethod]
        public void TestObjectFilterOperations()
        {
            var strings = new[] {"urn:one", "urn:two", "urn:three"};
            var uris = strings.Select(s => new Uri(s)).ToArray();
            CollectionAssert.AreEqual(strings, uris.Select(uri=>uri.ToString()).ToArray());
            VerifyFilterCount(uris, FilterOperations.OP_IS_BLANK, null, 0);
            VerifyFilterCount(uris, FilterOperations.OP_IS_NOT_BLANK, null, 3);
            VerifyFilterCount(uris, FilterOperations.OP_EQUALS, "urn:two", 1);
            VerifyFilterCount(uris, FilterOperations.OP_NOT_EQUALS, "urn:two", 2);
            VerifyFilterCount(uris, FilterOperations.OP_EQUALS, "invaliduri", 0);
            Assert.IsFalse(IsValidForType<Uri>(FilterOperations.OP_STARTS_WITH));
            Assert.IsFalse(IsValidForType<Uri>(FilterOperations.OP_CONTAINS));
            Assert.IsFalse(IsValidForType<Uri>(FilterOperations.OP_NOT_CONTAINS));
            Assert.IsFalse(IsValidForType<Uri>(FilterOperations.OP_IS_LESS_THAN));
            Assert.IsFalse(IsValidForType<Uri>(FilterOperations.OP_IS_LESS_THAN_OR_EQUAL));
            Assert.IsFalse(IsValidForType<Uri>(FilterOperations.OP_IS_GREATER_THAN));
            Assert.IsFalse(IsValidForType<Uri>(FilterOperations.OP_IS_GREATER_THAN_OR_EQUAL));
        }

        [TestMethod]
        public void TestDateFilterOperations()
        {
            var dates = new[]
            {
                DateTime.Parse("1776-07-04", CultureInfo.InvariantCulture),
                DateTime.Parse("1969-07-20", CultureInfo.InvariantCulture), 
                DateTime.Parse("2012-12-21", CultureInfo.InvariantCulture)
            };
            VerifyFilterCountStructs(dates, FilterOperations.OP_IS_BLANK, null, 0);
            VerifyFilterCountStructs(dates, FilterOperations.OP_IS_NOT_BLANK, null, 3);
            VerifyFilterCountStructs(dates, FilterOperations.OP_EQUALS, "1969-07-20", 1);
            VerifyFilterCountStructs(dates, FilterOperations.OP_NOT_EQUALS, "1969-07-20", 2);
            VerifyFilterCountStructs(dates, FilterOperations.OP_IS_LESS_THAN, "1969-07-20", 1);
            VerifyFilterCountStructs(dates, FilterOperations.OP_IS_LESS_THAN_OR_EQUAL, "1969-07-20", 2);
            VerifyFilterCountStructs(dates, FilterOperations.OP_IS_GREATER_THAN, "1969-07-20", 1);
            VerifyFilterCountStructs(dates, FilterOperations.OP_IS_GREATER_THAN_OR_EQUAL, "1969-07-20", 2);
            Assert.IsFalse(IsValidForType<DateTime>(FilterOperations.OP_STARTS_WITH));
            Assert.IsFalse(IsValidForType<DateTime>(FilterOperations.OP_CONTAINS));
            Assert.IsNull(GetOperandError<DateTime>("1969-07-20"));
            Assert.IsNotNull(GetOperandError<DateTime>("invalid date"));
        }

        [TestMethod]
        public void TestBoolFilterOperations()
        {
            var bools = new[]
            {
                true,
                false,
            };
            VerifyFilterCountStructs(bools, FilterOperations.OP_IS_BLANK, null, 0);
            VerifyFilterCountStructs(bools, FilterOperations.OP_IS_NOT_BLANK, null, 2);
            VerifyFilterCountStructs(bools, FilterOperations.OP_EQUALS, "True", 1);
            VerifyFilterCountStructs(bools, FilterOperations.OP_EQUALS, "False", 1);
            VerifyFilterCountStructs(bools, FilterOperations.OP_NOT_EQUALS, "True", 1);
            VerifyFilterCountStructs(bools, FilterOperations.OP_NOT_EQUALS, "False", 1);
            Assert.IsFalse(IsValidForType<Boolean>(FilterOperations.OP_STARTS_WITH));
            Assert.IsFalse(IsValidForType<Boolean>(FilterOperations.OP_CONTAINS));
            Assert.IsFalse(IsValidForType<Boolean>(FilterOperations.OP_NOT_CONTAINS));
            Assert.IsFalse(IsValidForType<Boolean>(FilterOperations.OP_IS_LESS_THAN));
            Assert.IsFalse(IsValidForType<Boolean>(FilterOperations.OP_IS_LESS_THAN_OR_EQUAL));
            Assert.IsFalse(IsValidForType<Boolean>(FilterOperations.OP_IS_GREATER_THAN));
            Assert.IsFalse(IsValidForType<Boolean>(FilterOperations.OP_IS_GREATER_THAN_OR_EQUAL));
            Assert.IsNull(GetOperandError<Boolean>("True"));
            Assert.IsNull(GetOperandError<Boolean>("False"));
            Assert.IsNotNull(GetOperandError<Boolean>("t"));
        }

        private List<TItem> ApplyFilter<TItem>(IFilterOperation filterOperation, string operand, IEnumerable<TItem> items)
        {
            var dataSchema = new DataSchema();
            var columnDescriptor = ColumnDescriptor.RootColumn(dataSchema, typeof(TItem));
            if (null == operand)
            {
                Assert.IsNull(filterOperation.GetOperandType(columnDescriptor));
            }
            else
            {
                Assert.IsNotNull(filterOperation.GetOperandType(columnDescriptor));
            }
            var predicate = filterOperation.MakePredicate(columnDescriptor, operand);
            return items.Where(item => predicate(item)).ToList();
        }

        private void VerifyFilterCount<TItem, TNullableItem>(IList<TItem> items, IFilterOperation filterOperation,
            string operand, int expectedCount)
        {
            Assert.AreEqual(expectedCount, ApplyFilter(filterOperation, operand, items).Count);
            var nullableItems = items.Cast<TNullableItem>().ToList();
            CollectionAssert.AreEqual(ApplyFilter(filterOperation, operand, items), 
                ApplyFilter(filterOperation, operand, nullableItems));
            Assert.IsNull(default(TNullableItem));
            var nulls = Enumerable.Repeat(default(TNullableItem), 3).ToList();
            Assert.AreEqual(expectedCount, ApplyFilter(filterOperation, operand, nullableItems).Count);
            var itemsWithNulls = nullableItems.Concat(nulls);
            int nullCount;
            if (filterOperation == FilterOperations.OP_IS_BLANK || filterOperation == FilterOperations.OP_NOT_EQUALS)
            {
                nullCount = nulls.Count;
            }
            else
            {
                nullCount = 0;
            }
            Assert.AreEqual(nullCount, ApplyFilter(filterOperation, operand, nulls).Count);
            Assert.AreEqual(nullCount + expectedCount, ApplyFilter(filterOperation, operand, itemsWithNulls).Count);
        }
        
        private void VerifyFilterCount<TItem>(IList<TItem> items, IFilterOperation filterOperation, string operand, int expectedCount) where TItem : class
        {
            VerifyFilterCount<TItem, TItem>(items, filterOperation, operand, expectedCount);
        }

        private void VerifyFilterCountStructs<TItem>(IList<TItem> items, IFilterOperation filterOperation, string operand,
            int expectedCount) where TItem : struct
        {
            VerifyFilterCount<TItem, TItem?>(items, filterOperation, operand, expectedCount);
        }

        private bool IsValidForType<T>(IFilterOperation filterOperation)
        {
            var dataSchema = new DataSchema();
            ColumnDescriptor columnDescriptor = ColumnDescriptor.RootColumn(dataSchema, typeof (T));
            return filterOperation.IsValidFor(columnDescriptor);
        }

        private string GetOperandError<T>(string operand)
        {
            var dataSchema = new DataSchema();
            var rootColumn = ColumnDescriptor.RootColumn(dataSchema, typeof(DataRowWithProperty<T>));
            var column = rootColumn.ResolveChild("Property");
            FilterInfo filterInfo = new FilterInfo(new FilterSpec(column.PropertyPath, FilterOperations.OP_EQUALS, operand), column, rootColumn);
            return filterInfo.Error;
        }

        class DataRowWithProperty<T>
        {
            public T Property { get; set; }
        }
    }
}
