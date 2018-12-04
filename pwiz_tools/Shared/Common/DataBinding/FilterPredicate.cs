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

        /// <summary>
        /// Constructs a new FilterPredicate from a value that the user has typed in the user interface.
        /// "operandText" is expected to be formatted according to the locale settings of "dataSchema".
        /// </summary>
        public static FilterPredicate CreateFilterPredicate(DataSchema dataSchema, Type columnType, IFilterOperation filterOperation, string operandText)
        {
            object operandValue;
            if (string.IsNullOrEmpty(operandText))
            {
                operandValue = null;
            }
            else
            {
                Type operandType = filterOperation.GetOperandType(dataSchema, columnType);
                if (null == operandType)
                {
                    operandValue = null;
                }
                else
                {
                    operandValue = ParseOperandValue(dataSchema.DataSchemaLocalizer.FormatProvider, operandType, operandText);
                }
            }
            string invariantOperandText = OperandValueToString(CultureInfo.InvariantCulture, operandValue);
            return new FilterPredicate(filterOperation, invariantOperandText);
        }

        private FilterPredicate(IFilterOperation filterOperation, String invariantOperandText)
        {
            FilterOperation = filterOperation;
            InvariantOperandText = invariantOperandText;
        }
        [TrackChildren(ignoreName: true)]
        public IFilterOperation FilterOperation { get; private set; }
        [Track]
        public string InvariantOperandText { get; private set; }

        public object GetOperandValue(ColumnDescriptor columnDescriptor)
        {
            return GetOperandValue(columnDescriptor.DataSchema, columnDescriptor.PropertyType);
        }

        public object GetOperandValue(DataSchema dataSchema, Type columnType)
        {
            return ParseOperandValue(CultureInfo.InvariantCulture, FilterOperations.GetTypeToConvertOperandTo(dataSchema, columnType), InvariantOperandText);
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
                object operand = GetOperandValue(dataSchema, propertyType);
                return (string) Convert.ChangeType(operand, typeof(string),
                    dataSchema.DataSchemaLocalizer.FormatProvider);
            }
            catch (Exception)
            {
                return InvariantOperandText;
            }
        }

        private static object ParseOperandValue(CultureInfo cultureInfo, Type type, string operandValue)
        {
            if (null == operandValue)
            {
                return null;
            }
            if (type == null)
            {
                return operandValue;
            }
            if (typeof(char) == type)
            {
                if (operandValue.Length != 1)
                {
                    return null;
                }
                return operandValue[0];
            }
            if (type.IsEnum)
            {
                if (string.IsNullOrEmpty(operandValue))
                {
                    return null;
                }
                try
                {
                    return Enum.Parse(type, operandValue);
                }
                catch
                {
                    return Enum.Parse(type, operandValue, true);
                }
            }
            if (string.IsNullOrEmpty(operandValue))
            {
                return null;
            }
            return Convert.ChangeType(operandValue, type, cultureInfo);
        }

        private static string OperandValueToString(CultureInfo cultureInfo, object operandValue)
        {
            if (operandValue == null)
            {
                return null;
            }
            return (string) Convert.ChangeType(operandValue, typeof (string), cultureInfo);
        }

        public Predicate<object> MakePredicate(DataSchema dataSchema, Type columnType)
        {
            object operandValue = GetOperandValue(dataSchema, columnType);
            return columnValue => FilterOperation.Matches(dataSchema, columnType, columnValue, operandValue);
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
