/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Layout
{
    public class ColumnId : IComparable<ColumnId>, IAuditLogObject
    {
        public ColumnId(IColumnCaption columnCaption) : this(columnCaption.GetCaption(DataSchemaLocalizer.INVARIANT))
        {
        }
        public ColumnId(string name)
        {
            Name = name;
        }

        public string Name { get; private set; }

        public override string ToString()
        {
            return Name;
        }

        public string ToPersistedString()
        {
            return Name;
        }

        public IColumnCaption ToColumnCaption()
        {
            return new ColumnCaption(Name);
        }

        public static ColumnId ParsePersistedString(string str)
        {
            return new ColumnId(str);
        }

        protected bool Equals(ColumnId other)
        {
            return string.Equals(Name, other.Name);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ColumnId) obj);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public static ColumnId TryGetColumnId(PropertyDescriptor propertyDescriptor)
        {
            var dataPropertyDescriptor = propertyDescriptor as DataPropertyDescriptor;
            if (dataPropertyDescriptor == null)
            {
                return null;
            }
            return GetColumnId(dataPropertyDescriptor);
        }

        public static ColumnId GetColumnId(DataPropertyDescriptor dataPropertyDescriptor)
        {
            return new ColumnId(dataPropertyDescriptor.ColumnCaption.GetCaption(DataSchemaLocalizer.INVARIANT));
        }

        public int CompareTo(ColumnId other)
        {
            return string.CompareOrdinal(Name, other.Name);
        }

        public string AuditLogText
        {
            get { return string.Format(@"{{5:{0}}}", Name); }
        }

        public bool IsName
        {
            get { return true; }
        }
    }
}
