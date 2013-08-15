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
using System.Text;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding
{
    public class PivotKey
    {
        private readonly KeyValuePair<PropertyPath, object>[] _valuePairs;

        public static readonly PivotKey Root = 
            new PivotKey(null, PropertyPath.Root, new KeyValuePair<PropertyPath, object>[0]);
        public static PivotKey OfValues(PivotKey parent, PropertyPath collectionId, IEnumerable<KeyValuePair<PropertyPath, object>> valuePairs)
        {
            var valuePairList = new List<KeyValuePair<PropertyPath, object>>();
            foreach (var vp in valuePairs)
            {
                if (!vp.Key.StartsWith(collectionId))
                {
                    throw new ArgumentException(vp.Key + " does not start with " + collectionId);
                }
                if (vp.Value == null)
                {
                    continue;
                }
                valuePairList.Add(vp);
            }
            if (valuePairList.Count == 0)
            {
                return parent;
            }
            valuePairList.Sort(Comparer);
            return new PivotKey(parent, collectionId, valuePairList.ToArray());
        }
        private PivotKey(PivotKey parent, PropertyPath collectionId, KeyValuePair<PropertyPath, object>[] valuePairs)
        {
            Parent = parent;
            CollectionId = collectionId;
            _valuePairs = valuePairs;
        }
        private static int CompareValuePairs(KeyValuePair<PropertyPath, object> vp1, KeyValuePair<PropertyPath, object> vp2)
        {
            return vp1.Key.CompareTo(vp2.Key);
        }
        private class ValuePairComparer : IComparer<KeyValuePair<PropertyPath, object>>
        {
            public int Compare(KeyValuePair<PropertyPath, object> x, KeyValuePair<PropertyPath, object> y)
            {
                return CompareValuePairs(x, y);
            }
        }
        private static readonly ValuePairComparer Comparer = new ValuePairComparer();
        public PivotKey(PropertyPath collectionId)
        {
            CollectionId = collectionId;
            _valuePairs = new KeyValuePair<PropertyPath, object>[0];
        }
        public PivotKey(PropertyPath propertyPath, object value)
        {
            CollectionId = PropertyPath.Root;
            _valuePairs = new[]{new KeyValuePair<PropertyPath, object>(propertyPath, value)};
        }
        public PivotKey RemoveSublist(PropertyPath sublistId)
        {
            PivotKey newParent;
            if (sublistId.StartsWith(CollectionId) || Parent == null)
            {
                newParent = null;
            }
            else
            {
                newParent = Parent.RemoveSublist(sublistId);
            }
            return OfValues(newParent, CollectionId, ValuePairs.Where(vp => !sublistId.StartsWith(vp.Key)));
        }
        public PropertyPath CollectionId { get; private set; }
        public PivotKey Parent { get; private set; }
        public IList<KeyValuePair<PropertyPath, object>> ValuePairs { get { return Array.AsReadOnly(_valuePairs); } }
        public object FindValue(PropertyPath propertyPath)
        {
            if (propertyPath.StartsWith(CollectionId))
            {
                int index = Array.BinarySearch(
                    _valuePairs, new KeyValuePair<PropertyPath, object>(propertyPath, null), Comparer);
                if (index < 0)
                {
                    return null;
                }
                return _valuePairs[index].Value;
            }
            if (Parent != null)
            {
                return Parent.FindValue(propertyPath);
            }
            return null;
        }

        #region Equality Members
        public bool Equals(PivotKey other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return _valuePairs.SequenceEqual(other._valuePairs)
                   && Equals(CollectionId, other.CollectionId)
                   && Equals(Parent, other.Parent);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (PivotKey)) return false;
            return Equals((PivotKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = CollectionUtil.GetHashCodeDeep(_valuePairs);
                result = (result*397) ^ CollectionId.GetHashCode();
                result = (result*397) ^ (Parent != null ? Parent.GetHashCode() : 0);
                return result;
            }
        }
        #endregion
        public static Comparison<PivotKey> GetComparison(DataSchema dataSchema, IEnumerable<PropertyPath> keys)
        {
            var sortedKeys = keys.ToArray();
            Array.Sort(sortedKeys);
            return (groupKey1, groupKey2) =>
            {
                foreach (var key in sortedKeys)
                {
                    var result = dataSchema.Compare(groupKey1.FindValue(key), groupKey2.FindValue(key));
                    if (result != 0)
                    {
                        return result;
                    }
                }
                return 0;
            };
        }

        public override string ToString()
        {
            var result = new StringBuilder();
            var collectionIdToString = CollectionId.ToString();
            if (Parent != null)
            {
                result.Append(Parent);
                result.Append("[");
                result.Append(collectionIdToString.Substring(Parent.CollectionId.ToString().Length));
            }
            else
            {
                result.Append(CollectionId);
            }
            result.Append("{");
            result.Append(string.Join("},{", ValuePairs.Select(vp => vp.Key.ToString().Substring(collectionIdToString.Length) + "," + vp.Value).ToArray()));
            result.Append("}");
            if (Parent != null)
            {
                result.Append("]");
            }
            return result.ToString();
        }
        /// <summary>
        /// Take the values from the PivotKey and plug them into the unbound (i.e. name=null)
        /// parts of the PropertyPath.
        /// </summary>
        public static PropertyPath QualifyIdentifierPath(PivotKey pivotKey, PropertyPath propertyPath)
        {
            if (pivotKey == null)
            {
                return propertyPath;
            }
            var parts = new KeyValuePair<string, bool>[propertyPath.Length];
            while (propertyPath.Length > 0)
            {
                parts[propertyPath.Length - 1] = new KeyValuePair<string, bool>(propertyPath.Name, propertyPath.IsProperty);
                propertyPath = propertyPath.Parent;
            }
            while (pivotKey != null)
            {
                if (pivotKey.CollectionId.Length <= parts.Length)
                {
                    if (pivotKey.ValuePairs.Count == 1 && Equals(pivotKey.ValuePairs[0].Key, pivotKey.CollectionId))
                    {
                        parts[pivotKey.CollectionId.Length] = new KeyValuePair<string, bool>(pivotKey.ValuePairs[0].Value.ToString(), false);
                    }
                    else
                    {
                        parts[pivotKey.CollectionId.Length] 
                            = new KeyValuePair<string, bool>(string.Join(",", pivotKey.ValuePairs.Select(vp => vp.ToString()).ToArray()), false);
                    }
                }
                pivotKey = pivotKey.Parent;
            }
            foreach (var part in parts)
            {
                if (part.Value)
                {
                    propertyPath = propertyPath.Property(part.Key);
                }
                else
                {
                    if (null == part.Key)
                    {
                        propertyPath = propertyPath.LookupAllItems();
                    }
                    else
                    {
                        propertyPath = propertyPath.LookupByKey(part.Key);
                    }
                }
            }
            return propertyPath;
        }

        public static IComparer<PivotKey> GetComparer(DataSchema dataSchema)
        {
            return new GroupKeyComparer(dataSchema);
        }

        private class GroupKeyComparer : IComparer<PivotKey>
        {
            public GroupKeyComparer(DataSchema dataSchema)
            {
                DataSchema = dataSchema;
            }

            private DataSchema DataSchema { get; set; }

            public int Compare(PivotKey x, PivotKey y)
            {
                if (x.CollectionId.Length > y.CollectionId.Length)
                {
                    return -Compare(y, x);
                }
                int defResult = x.CollectionId.Length < y.CollectionId.Length ? -1 : 0;
                while (x.CollectionId.Length < y.CollectionId.Length)
                {
                    y = y.Parent;
                }
                int result;
                if (x.CollectionId.Length > 1)
                {
                    result = Compare(x.Parent, y.Parent);
                    if (result != 0)
                    {
                        return result;
                    }
                }
                result = x.CollectionId.CompareTo(y.CollectionId);
                if (result != 0)
                {
                    return result;
                }
                for (int i = 0; i < x.ValuePairs.Count; i++)
                {
                    if (i >= y.ValuePairs.Count)
                    {
                        return 1;
                    }
                    result = x.ValuePairs[i].Key.CompareTo(y.ValuePairs[i].Key);
                    if (result != 0)
                    {
                        return result;
                    }
                    result = DataSchema.Compare(x.ValuePairs[i].Value, y.ValuePairs[i].Value);
                    if (result != 0)
                    {
                        return result;
                    }
                }
                return defResult;
            }
        }
    }
}
