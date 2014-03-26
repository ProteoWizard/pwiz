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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace pwiz.Common.DataBinding.Internal
{
    /// <summary>
    /// PropertyDescriptor for a property which gets its value from a <see cref="GroupedRow"/> 
    /// inside of a <see cref="RowItem"/>.
    /// </summary>
    internal class GroupedPropertyDescriptor : PropertyDescriptor
    {
        public GroupedPropertyDescriptor(string name, DisplayColumn displayColumn, PivotKey innerPivotKey) : this(name, null, displayColumn, innerPivotKey)
        {
        }

        public GroupedPropertyDescriptor(string name, PivotKey outerPivotKey, DisplayColumn displayColumn, PivotKey innerPivotKey) 
            : base(name, displayColumn.GetAttributes(MergePivotKeys(outerPivotKey, innerPivotKey)).ToArray())
        {
            OuterPivotKey = outerPivotKey;
            InnerPivotKey = innerPivotKey;
            DisplayColumn = displayColumn;
        }

        public DisplayColumn DisplayColumn { get; private set; }
        public PivotKey OuterPivotKey { get; private set; }
        public PivotKey InnerPivotKey { get; private set; }
        public override bool CanResetValue(object component)
        {
            return false;
        }

        public override object GetValue(object component)
        {
            var outerRowItem = component as RowItem;
            if (null == outerRowItem)
            {
                return null;
            }
            var reportRow = outerRowItem.Value as GroupedRow;
            if (null == reportRow)
            {
                return null;
            }
            IEnumerable<RowItem> innerRows;
            if (null == OuterPivotKey)
            {
                innerRows = reportRow.InnerRows;
            }
            else
            {
                RowItem innerRow;
                if (!reportRow.TryGetInnerRow(OuterPivotKey, out innerRow))
                {
                    return null;
                }
                innerRows = new[] {innerRow};
            }

            var values = new List<object>();
            foreach (var innerRow in innerRows)
            {
                var value = DisplayColumn.GetValue(innerRow, InnerPivotKey);
                if (null != value)
                {
                    values.Add(value);
                }
            }
            var distinctValues = values.Distinct().ToArray();
            if (distinctValues.Length == 0)
            {
                return null;
            }
            else if (distinctValues.Length == 1)
            {
                return distinctValues[0];
            }
            else
            {
                return null;
            }
        }

        public override void ResetValue(object component)
        {
        }

        public override void SetValue(object component, object value)
        {
        }

        public override bool ShouldSerializeValue(object component)
        {
            return false;
        }

        public override Type ComponentType
        {
            get { return typeof (RowItem); }
        }

        public override bool IsReadOnly
        {
            get { return true; }
        }

        public override Type PropertyType
        {
            get { return DisplayColumn.PropertyType; }
        }

        private static PivotKey MergePivotKeys(PivotKey outerPivotKey, PivotKey innerPivotKey)
        {
            if (outerPivotKey == null)
            {
                return innerPivotKey;
            }
            else
            {
                if (innerPivotKey == null)
                {
                    return outerPivotKey;
                }
                else
                {
                    return outerPivotKey.Concat(innerPivotKey);
                }
            }
        }

        #region Equality members
        protected bool Equals(GroupedPropertyDescriptor other)
        {
            return base.Equals(other) 
                && Equals(DisplayColumn, other.DisplayColumn) 
                && Equals(OuterPivotKey, other.OuterPivotKey)
                && Equals(InnerPivotKey, other.InnerPivotKey);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((GroupedPropertyDescriptor) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode*397) ^ DisplayColumn.GetHashCode();
                hashCode = (hashCode*397) ^ (OuterPivotKey != null ? OuterPivotKey.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (InnerPivotKey != null ? InnerPivotKey.GetHashCode() : 0);
                return hashCode;
            }
        }
        #endregion
    }
}
