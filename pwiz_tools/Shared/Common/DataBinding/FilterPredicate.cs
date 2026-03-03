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
using System.Globalization;
using System.Xml;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding
{
    public class FilterPredicate
    {
        public static readonly FilterPredicate HAS_ANY_VALUE = new FilterPredicate(FilterOperations.OP_HAS_ANY_VALUE, null);
        public static readonly FilterPredicate IS_NOT_BLANK = new FilterPredicate(FilterOperations.OP_IS_NOT_BLANK, null);
        public static readonly FilterPredicate IS_BLANK = new FilterPredicate(FilterOperations.OP_IS_BLANK, null);

        /// <summary>
        /// Constructs a new FilterPredicate from a value that the user has typed in the user interface.
        /// "operandText" is expected to be formatted according to the locale settings of "dataSchema".
        /// </summary>
        public static FilterPredicate Create(IFilterOperation filterOperation, IFormattable operand)
        {
            return new FilterPredicate(filterOperation,
                operand == null ? null : Convert.ToString(operand, CultureInfo.InvariantCulture));
        }

        public static FilterPredicate Create(IFilterOperation filterOperation,
            string invariantOperantText)
        {
            return new FilterPredicate(filterOperation, invariantOperantText);
        }

        public static FilterPredicate Parse(DataSchema dataSchema, Type columnType, IFilterOperation filterOperation,
            string operandText)
        {
            if (!filterOperation.HasOperand())
            {
                return new FilterPredicate(filterOperation, null);
            }

            var handler = dataSchema.GetFilterHandler(columnType);
            return new FilterPredicate(filterOperation, handler.OperandToString(filterOperation, handler.ParseOperand(filterOperation, operandText, CultureInfo.CurrentCulture), CultureInfo.InvariantCulture));
        }

        public static FilterPredicate SafeParse(DataSchema dataSchema, Type columnType,
            IFilterOperation filterOperation, string operandText)
        {
            try
            {
                return Parse(dataSchema, columnType, filterOperation, operandText);
            }
            catch
            {
                return Create(filterOperation, operandText);
            }
        }

        public FilterPredicate(IFilterOperation filterOperation, String invariantOperandText)
        {
            FilterOperation = filterOperation;
            InvariantOperandText = invariantOperandText;
        }
        [TrackChildren(ignoreName: true)]
        public IFilterOperation FilterOperation { get; private set; }
        [Track]
        public string InvariantOperandText { get; private set; }

        public static FilterPredicate FromInvariantOperandText(IFilterOperation filterOperation,
            string invariantOperandText)
        {
            return new FilterPredicate(filterOperation, invariantOperandText);
        }

        public object GetOperandValue(DataSchema dataSchema, Type columnType)
        {
            return dataSchema.GetFilterHandler(columnType).ParseOperand(FilterOperation, InvariantOperandText, CultureInfo.InvariantCulture);
        }

        public string GetOperandDisplayText(ColumnDescriptor columnDescriptor)
        {
            if (null == columnDescriptor)
            {
                return InvariantOperandText;
            }
            return GetOperandDisplayText(columnDescriptor.DataSchema, columnDescriptor.PropertyType);
        }

        public string GetOperandDisplayText(DataSchema dataSchema, Type propertyType)
        {
            try
            {
                var handler = dataSchema.GetFilterHandler(propertyType);
                return handler.OperandToString(FilterOperation, InvariantOperandText, CultureInfo.CurrentCulture);
            }
            catch (Exception)
            {
                return InvariantOperandText;
            }
        }

        public Predicate<object> MakePredicate(DataSchema dataSchema, Type columnType)
        {
            var filterHandler = dataSchema.GetFilterHandler(columnType);
            object operandValue;
            if (FilterOperation.HasOperand())
            {
                operandValue = filterHandler.ParseOperand(FilterOperation, InvariantOperandText, CultureInfo.InvariantCulture);
            }
            else
            {
                operandValue = null;
            }
            return columnValue => FilterOperation.Matches(filterHandler, columnValue, operandValue);
        }

        #region Equality members
        protected bool Equals(FilterPredicate other)
        {
            return Equals(FilterOperation, other.FilterOperation) && string.Equals(InvariantOperandText, other.InvariantOperandText);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((FilterPredicate) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (FilterOperation.GetHashCode()*397) ^ (InvariantOperandText != null ? InvariantOperandText.GetHashCode() : 0);
            }
        }
        #endregion

        #region XML Serialization
        // ReSharper disable LocalizableElement
        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttributeString("opname", FilterOperation.OpName);
            if (InvariantOperandText != null)
            {
                writer.WriteAttributeString("operand", InvariantOperandText);
            }
        }

        public static FilterPredicate ReadXml(XmlReader reader)
        {
            string opName = reader.GetAttribute("opname");
            string operand = reader.GetAttribute("operand");
            return new FilterPredicate(FilterOperations.GetOperation(opName), operand);
        }
        // ReSharper restore LocalizableElement
        #endregion
    }
}
