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
using System.Diagnostics;
using System.Linq;

namespace pwiz.Common.DataBinding
{
    public abstract class PivotKey
    {
        public static readonly PivotKey EMPTY = new Empty();
        protected int _hashCode;

        protected PivotKey(PropertyPath propertyPath, object value)
        {
            PropertyPath = propertyPath;
            Value = value;
            _hashCode = PropertyPath.GetHashCode();
            if (Value != null)
            {
                _hashCode = (_hashCode * 397) ^ Value.GetHashCode();
            }
        }
        public static PivotKey GetPivotKey(IDictionary<PivotKey, PivotKey> pivotKeys,
            IEnumerable<KeyValuePair<PropertyPath, object>> keyPairs)
        {
            var result = EMPTY;
            foreach (var entry in keyPairs)
            {
                result = result.AppendValue(entry.Key, entry.Value);
                PivotKey existing;
                if (pivotKeys.TryGetValue(result, out existing))
                {
                    result = existing;
                }
                else
                {
                    pivotKeys.Add(result, result);
                }
            }
            return result;
        }

        public PivotKey Concat(PivotKey pivotKey)
        {
            if (Length == 0)
            {
                return pivotKey;
            }
            if (pivotKey.Length == 0)
            {
                return this;
            }

            return Concat(pivotKey.Parent).AppendValue(pivotKey.PropertyPath, pivotKey.Value);
        }

        protected abstract PivotKey Parent { get; }
        protected PropertyPath PropertyPath { get; set; }
        protected object Value { get; set; }

        public IEnumerable<KeyValuePair<PropertyPath, object>> KeyPairs
        {
            get
            {
                if (null != Parent)
                {
                    foreach (var entry in Parent.KeyPairs)
                    {
                        yield return entry;
                    }
                    yield return new KeyValuePair<PropertyPath, object>(PropertyPath, Value);
                }
            }
        }

        public KeyValuePair<PropertyPath, object> Last
        {
            get
            {
                return new KeyValuePair<PropertyPath, object>(PropertyPath, Value);
            }
        }
        public abstract int Length { get; }
        public object FindValue(PropertyPath propertyPath)
        {
            for (var pivotKey = this; pivotKey != null; pivotKey = pivotKey.Parent)
            {
                if (Equals(pivotKey.PropertyPath, propertyPath))
                {
                    return pivotKey.Value;
                }
            }
            return null;
        }

        public bool Contains(PropertyPath propertyPath)
        {
            for (var pivotKey = this; pivotKey != null; pivotKey = pivotKey.Parent)
            {
                if (Equals(pivotKey.PropertyPath, propertyPath))
                {
                    return true;
                }
            }
            return false;
        }

        public PivotKey AppendValue(PropertyPath propertyPath, object value)
        {
#if DEBUG
            Debug.Assert(!Contains(propertyPath));
#else
            Debug.Assert(true);
#endif
            return MakeChild(propertyPath, value);
        }

        protected abstract PivotKey MakeChild(PropertyPath propertyPath, object value);

        #region Equality Members
        public bool Equals(PivotKey other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(Parent, other.Parent) && Equals(PropertyPath, other.PropertyPath) && Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj is PivotKey pivotKey)
            {
                return Equals(pivotKey);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }
        #endregion
        public static Comparison<PivotKey> GetComparison(DataSchema dataSchema)
        {
            return new Comparer(dataSchema).Compare;
        }

        public override string ToString()
        {
            return (@"[{" + String.Join(@"},{", KeyPairs.Select(vp => vp.Key.ToString() + @"," + vp.Value).ToArray()) + @"}]");
        }
        /// <summary>
        /// Take the values from the PivotKey and plug them into the unbound (i.e. name=null)
        /// parts of the PropertyPath.
        /// </summary>
        public static PropertyPath QualifyPropertyPath(PivotKey pivotKey, PropertyPath propertyPath)
        {
            if (pivotKey == null || propertyPath.IsRoot)
            {
                return propertyPath;
            }
            var parent = QualifyPropertyPath(pivotKey, propertyPath.Parent);
            if (propertyPath.IsUnboundLookup)
            {
                object value = pivotKey.FindValue(propertyPath);
                if (null != value)
                {
                    return parent.LookupByKey(value.ToString());
                }
            }
            if (ReferenceEquals(parent, propertyPath.Parent))
            {
                return propertyPath;
            }
            if (propertyPath.IsUnboundLookup)
            {
                return parent.LookupAllItems();
            }
            if (propertyPath.IsProperty)
            {
                return parent.Property(propertyPath.Name);
            }
            return parent.LookupByKey(propertyPath.Name);
        }

        public static IComparer<PivotKey> GetComparer(DataSchema dataSchema)
        {
            return new Comparer(dataSchema);
        }

        public class Comparer : IComparer<PivotKey>
        {
            private readonly DataSchema _dataSchema;
            public Comparer(DataSchema dataSchema)
            {
                _dataSchema = dataSchema;
            }

            public int Compare(PivotKey x, PivotKey y)
            {
                // ReSharper disable PossibleNullReferenceException
                if (x.Length == 0)
                {
                    return y.Length == 0 ? 0 : -1;
                }
                if (y.Length == 0)
                {
                    return 1;
                }
                if (x.Length > y.Length)
                {
                    int result = Compare(x.Parent, y);
                    if (result != 0)
                    {
                        return result;
                    }
                    return 1;
                }
                else if (x.Length < y.Length)
                {
                    int result = Compare(x, y.Parent);
                    if (result != 0)
                    {
                        return result;
                    }
                    return -1;
                }
                else
                {
                    int result = Compare(x.Parent, y.Parent);
                    if (result != 0)
                    {
                        return result;
                    }
                    result = x.PropertyPath.CompareTo(y.PropertyPath);
                    if (result != 0)
                    {
                        return result;
                    }
                    return _dataSchema.Compare(x.Value, y.Value);
                }
                // ReSharper restore PossibleNullReferenceException
            }
        }

        private class Empty : PivotKey
        {
            public Empty() : base(PropertyPath.Root, null)
            {
            }

            protected override PivotKey Parent
            {
                get { return null; }
            }

            public override int Length
            {
                get { return 0; }
            }
            protected override PivotKey MakeChild(PropertyPath propertyPath, object value)
            {
                return new TopLevel(propertyPath, value);
            }
        }

        public class TopLevel : PivotKey
        {
            public TopLevel(PropertyPath propertyPath, object value) : base(propertyPath, value)
            {
            }

            protected override PivotKey Parent
            {
                get { return EMPTY; }
            }

            public override int Length
            {
                get { return 2; }
            }
            protected override PivotKey MakeChild(PropertyPath propertyPath, object value)
            {
                return new Child(this, propertyPath, value);
            }
        }

        public class Child : PivotKey
        {
            private PivotKey _parent;
            private int _length;
            public Child(PivotKey parent, PropertyPath propertyPath, object value) : base(propertyPath, value)
            {
                _parent = parent;
                _length = parent.Length + 1;
                _hashCode = (_hashCode * 397) ^ parent.GetHashCode();
            }

            protected override PivotKey Parent
            {
                get { return _parent; }
            }
            public override int Length
            {
                get { return _length; }
            }
            protected override PivotKey MakeChild(PropertyPath propertyPath, object value)
            {
                return new Child(this, propertyPath, value);
            }
        }
    }
}
