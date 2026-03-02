/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Common.DataBinding.Filtering;
using pwiz.Common.Properties;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding
{
    public interface IFilterOperation
    {
        string OpName { get; }
        [Track]
        string DisplayName { get; }
        string ShortDisplayName { get; }
        bool IsValidFor(ColumnDescriptor columnDescriptor);
        bool IsValidFor(DataSchema dataSchema, Type columnType);
        bool IsValidFor(IFilterHandler filterHandler);
        bool Matches(IFilterHandler filterHandler, object columnValue, object operandValue);
        bool HasOperand();
        bool UsesEquality();
    }

    public static class FilterOperations
    {
        public static readonly IFilterOperation OP_HAS_ANY_VALUE = new OpHasAnyValue();

        public static readonly IFilterOperation OP_EQUALS = new OpEquals();

        public static readonly IFilterOperation OP_NOT_EQUALS = new OpNotEquals();

        public static readonly IFilterOperation OP_IS_BLANK = new OpIsBlank();

        public static readonly IFilterOperation OP_IS_NOT_BLANK = new OpIsNotBlank();

        public static readonly IFilterOperation OP_IS_GREATER_THAN = new OpIsGreaterThan();

        public static readonly IFilterOperation OP_IS_LESS_THAN = new OpIsLessThan();

        public static readonly IFilterOperation OP_IS_GREATER_THAN_OR_EQUAL = new OpIsGreaterThanOrEqual();

        public static readonly IFilterOperation OP_IS_LESS_THAN_OR_EQUAL = new OpIsLessThanOrEqualTo();

        public static readonly IFilterOperation OP_CONTAINS = new OpContains();
        public static readonly IFilterOperation OP_NOT_CONTAINS = new OpNotContains();
        public static readonly IFilterOperation OP_STARTS_WITH = new OpStartsWith();
        public static readonly IFilterOperation OP_NOT_STARTS_WITH = new OpNotStartsWith();
        // ReSharper restore LocalizableElement

        private static readonly IList<IFilterOperation> LstFilterOperations = Array.AsReadOnly(new[]
        {
            OP_HAS_ANY_VALUE,
            OP_EQUALS,
            OP_NOT_EQUALS,
            OP_IS_BLANK,
            OP_IS_NOT_BLANK,
            OP_IS_GREATER_THAN,
            OP_IS_LESS_THAN,
            OP_IS_GREATER_THAN_OR_EQUAL,
            OP_IS_LESS_THAN_OR_EQUAL,
            OP_CONTAINS,
            OP_NOT_CONTAINS,
            OP_STARTS_WITH,
            OP_NOT_STARTS_WITH
        });

        private static readonly IDictionary<string, IFilterOperation> DictFilterOperations =
            LstFilterOperations.ToDictionary(op => op.OpName, op => op);

        public static IFilterOperation GetOperation(string name)
        {
            IFilterOperation result;
            DictFilterOperations.TryGetValue(name, out result);
            return result;
        }

        public static IList<IFilterOperation> ListOperations()
        {
            return LstFilterOperations;
        }

        abstract class FilterOperation : IFilterOperation
        {
            protected FilterOperation(string opName)
            {
                OpName = opName;
            }
            public string OpName { get; private set; }
            public abstract string DisplayName { get; }
            public virtual string ShortDisplayName
            {
                get { return DisplayName; }
            }

            public bool IsValidFor(ColumnDescriptor columnDescriptor)
            {
                return IsValidFor(columnDescriptor.DataSchema, columnDescriptor.PropertyType);
            }

            public bool IsValidFor(DataSchema dataSchema, Type columnType)
            {
                return IsValidFor(dataSchema.GetFilterHandler(columnType));
            }

            public abstract bool IsValidFor(IFilterHandler filterHandler);
            public abstract bool Matches(IFilterHandler filterHandler, object columnValue, object operandValue);
            public virtual bool HasOperand()
            {
                return true;
            }

            public abstract bool UsesEquality();
        }
        abstract class StringFilterOperation : FilterOperation
        {
            protected StringFilterOperation(string opName) 
                : base(opName)
            {
            }

            public override bool IsValidFor(IFilterHandler filterHandler)
            {
                return filterHandler is IFilterHandler.IContains;
            }

            public sealed override bool Matches(IFilterHandler filterHandler, object columnValue, object operandValue)
            {
                if (filterHandler is IFilterHandler.IContains containsHandler)
                {
                    return Matches(containsHandler, columnValue, operandValue);
                }

                return false;
            }

            protected abstract bool Matches(IFilterHandler.IContains filterHandler, object columnValue,
                object operandValue);
            public override bool UsesEquality()
            {
                return true;
            }
        }

        class OpContains : StringFilterOperation
        {
            public OpContains() : base(@"contains")
            {
            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Contains; }
            }

            protected override bool Matches(IFilterHandler.IContains filterHandler, object columnValue, object operandValue)
            {
                return filterHandler.Contains(columnValue, operandValue);
            }

        }

        class OpNotContains : StringFilterOperation
        {
            public OpNotContains() : base(@"notcontains")
            {
            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Does_Not_Contain; }
            }

            protected override bool Matches(IFilterHandler.IContains filterHandler, object columnValue, object operandValue)
            {
                return !filterHandler.Contains(columnValue, operandValue);
            }
        }

        class OpStartsWith : StringFilterOperation
        {
            public OpStartsWith() : base(@"startswith")
            {
            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Starts_With; }
            }

            protected override bool Matches(IFilterHandler.IContains filterHandler, object columnValue, object operandValue)
            {
                return filterHandler.StartsWith(columnValue, operandValue);
            }
        }

        class OpNotStartsWith : StringFilterOperation
        {
            public OpNotStartsWith() : base(@"notstartswith")
            {
            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Does_Not_Start_With; }
            }

            protected override bool Matches(IFilterHandler.IContains filterHandler, object columnValue, object operandValue)
            {
                return !filterHandler.StartsWith(columnValue, operandValue);
            }
        }

        class OpEquals : FilterOperation
        {
            public OpEquals() : base(@"equals")
            {
            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Equals; }
            }

            public override string ShortDisplayName
            {
                get { return @"="; }
            }

            public override bool IsValidFor(IFilterHandler filterHandler)
            {
                return true;
            }

            public override bool Matches(IFilterHandler filterHandler, object columnValue, object operandValue)
            {
                return filterHandler.ValueEqualsOperand(columnValue, operandValue);
            }
            public override bool UsesEquality()
            {
                return true;
            }

        }

        class OpNotEquals : FilterOperation
        {
            public OpNotEquals() : base(@"<>")
            {
            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Does_Not_Equal; }
            }

            public override bool IsValidFor(IFilterHandler filterHandler)
            {
                return true;
            }



            public override bool Matches(IFilterHandler filterHandler, object columnValue, object operandValue)
            {
                return !filterHandler.ValueEqualsOperand(columnValue, operandValue);
            }
            public override bool UsesEquality()
            {
                return true;
            }
        }

        class OpIsBlank : UnaryFilterOperation
        {
            public OpIsBlank() : base(@"isnullorblank")
            {
                
            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Is_Blank; }
            }

            protected override bool Matches(IFilterHandler filterHandler, object columnValue)
            {
                return filterHandler.IsBlank(columnValue);
            }

        }

        class OpIsNotBlank : UnaryFilterOperation
        {
            public OpIsNotBlank() : base(@"isnotnullorblank")
            {
                
            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Is_Not_Blank; }
            }

            protected override bool Matches(IFilterHandler filterHandler, object columnValue)
            {
                return !filterHandler.IsBlank(columnValue);
            }
        }

        class OpIsGreaterThan : ComparisonFilterOperation
        {
            public OpIsGreaterThan() : base(@">")
            {

            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Is_Greater_Than; }
            }

            protected override bool ComparisonMatches(int comparisonResult)
            {
                return comparisonResult > 0;
            }
        }

        class OpIsGreaterThanOrEqual : ComparisonFilterOperation
        {
            public OpIsGreaterThanOrEqual() : base(@">=")
            {

            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Is_Greater_Than_Or_Equal_To; }
            }

            protected override bool ComparisonMatches(int comparisonResult)
            {
                return comparisonResult >= 0;
            }
        }

        class OpIsLessThan : ComparisonFilterOperation
        {
            public OpIsLessThan() : base(@"<")
            {

            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Is_Less_Than; }
            }

            protected override bool ComparisonMatches(int comparisonResult)
            {
                return comparisonResult < 0;
            }
        }

        class OpIsLessThanOrEqualTo : ComparisonFilterOperation
        {
            public OpIsLessThanOrEqualTo() : base(@"<=")
            {
            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Is_Less_Than_Or_Equal_To; }
            }

            protected override bool ComparisonMatches(int comparisonResult)
            {
                return comparisonResult <= 0;
            }
        }

        abstract class ComparisonFilterOperation : FilterOperation
        {
            protected ComparisonFilterOperation(string opName)
                : base(opName)
            {
            }

            public sealed override string ShortDisplayName
            {
                get { return OpName; }
            }

            protected abstract bool ComparisonMatches(int comparisonResult);

            public override bool IsValidFor(IFilterHandler filterHandler)
            {
                return filterHandler is IFilterHandler.IComparison;
            }

            public override bool Matches(IFilterHandler filterHandler, object columnValue, object operandValue)
            {
                int? comparisonValue = (filterHandler as IFilterHandler.IComparison)?.Compare(columnValue, operandValue);
                return comparisonValue.HasValue && ComparisonMatches(comparisonValue.Value);
            }
            public override bool UsesEquality()
            {
                return ComparisonMatches(0);
            }
        }

        abstract class UnaryFilterOperation : FilterOperation
        {
            protected UnaryFilterOperation(string opName)
                : base(opName)
            {
                
            }

            public sealed override bool Matches(IFilterHandler filterHandler, object columnValue, object operandValue)
            {
                return Matches(filterHandler, columnValue);
            }

            protected abstract bool Matches(IFilterHandler filterHandler, object columnValue);
            public override bool IsValidFor(IFilterHandler filterHandler)
            {
                return true;
            }

            public override bool HasOperand()
            {
                return false;
            }
            public override bool UsesEquality()
            {
                return false;
            }

        }

        class OpHasAnyValue : UnaryFilterOperation
        {
            public OpHasAnyValue() : base(string.Empty)
            {
            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Has_Any_Value; }
            }

            protected override bool Matches(IFilterHandler filterHandler, object columnValue)
            {
                return true;
            }
        }
    }
}
