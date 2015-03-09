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
using System.ComponentModel;
using System.Linq;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// PropertyDescriptor which uses a <see cref="ColumnDescriptor"/> to obtain the property value.
    /// </summary>
    public class ColumnPropertyDescriptor : PropertyDescriptor
    {
        public ColumnPropertyDescriptor(DisplayColumn displayColumn, string name) : this(displayColumn, name, displayColumn.PropertyPath, null)
        {
        }
        public ColumnPropertyDescriptor(DisplayColumn displayColumn, string name, PropertyPath propertyPath, PivotKey pivotKey)
            : base(name, displayColumn.GetAttributes(pivotKey).ToArray())
        {
            DisplayColumn = displayColumn;
            PropertyPath = propertyPath;
            PivotKey = pivotKey;
        }
        public PropertyPath PropertyPath
        {
            get; private set;
        }
        public PivotKey PivotKey { get; private set; }

        public DisplayColumn DisplayColumn { get; private set; }
        public override bool CanResetValue(object component)
        {
            return false;
        }

        public override object GetValue(object component)
        {
            return DisplayColumn.GetValue(component as RowItem, PivotKey);
        }

        public override void ResetValue(object component)
        {
            throw new NotSupportedException();
        }

        public override void SetValue(object component, object value)
        {
            DisplayColumn.SetValue(component as RowItem, PivotKey, value);
        }

        public override bool ShouldSerializeValue(object component)
        {
            throw new NotSupportedException();
        }

        public override Type ComponentType
        {
            get { return typeof(RowItem); }
        }

        public override bool IsReadOnly
        {
            get { return DisplayColumn.IsReadOnly; }
        }

        public override Type PropertyType
        {
            get { return DisplayColumn.PropertyType; }
        }

        public override string DisplayName
        {
            get
            {
                return DisplayColumn.GetColumnCaption(PivotKey, ColumnCaptionType.localized);
            }
        }
        public delegate void HookPropertyChange(object component, PropertyDescriptor propertyDescriptor);

        #region Equality Members
        protected bool Equals(ColumnPropertyDescriptor other)
        {
            return base.Equals(other) 
                && Equals(PropertyPath, other.PropertyPath) 
                && Equals(PivotKey, other.PivotKey) 
                && Equals(DisplayColumn, other.DisplayColumn);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ColumnPropertyDescriptor) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode*397) ^ (PropertyPath != null ? PropertyPath.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (PivotKey != null ? PivotKey.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (DisplayColumn != null ? DisplayColumn.GetHashCode() : 0);
                return hashCode;
            }
        }
        #endregion
    }
}
