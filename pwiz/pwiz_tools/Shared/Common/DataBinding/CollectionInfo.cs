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
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace pwiz.Common.DataBinding
{
    public interface ICollectionInfo
    {
        Type ElementType { get; }
        Type KeyType { get; }
        bool IsDictionary { get; }
        object GetItemFromKey(object collection, object key);
        IEnumerable GetKeys(object collection);
        IEnumerable GetItems(object collection);
    }
    /// <summary>
    /// Helper for displaying Dictionaries and Lists in a BindingListView.
    /// </summary>
    public static class CollectionInfo
    {
        public static ICollectionInfo ForType(Type type)
        {
            if (!typeof(IEnumerable).IsAssignableFrom(type))
            {
                return null;
            }
            return GetCollectionInfo(type);
        }

        private static ICollectionInfo GetCollectionInfo(Type type)
        {
            if (type.IsGenericType && !type.ContainsGenericParameters)
            {
                var genericArguments = type.GetGenericArguments();
                switch (genericArguments.Length)
                {
                    case 1:
                        if (type.GetGenericTypeDefinition() == typeof(IList<>))
                        {
                            var elementType = genericArguments[0];
                            var listCollectionInfoType = typeof(ListCollectionInfo<>).MakeGenericType(elementType);
                            var array = Array.CreateInstance(listCollectionInfoType, 1);
                            return (ICollectionInfo) array.GetValue(0);
                        }
                        break;
                    case 2:
                        if (type.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                        {
                            var keyType = genericArguments[0];
                            var valueType = genericArguments[1];
                            var dictionaryCollectionInfoType =
                                typeof (DictionaryCollectionInfo<,>).MakeGenericType(keyType, valueType);
                            var array = Array.CreateInstance(dictionaryCollectionInfoType, 1);
                            return (ICollectionInfo) array.GetValue(0);
                        }
                        break;
                }
            }
            if (type.BaseType != null)
            {
                var result = GetCollectionInfo(type.BaseType);
                if (result != null)
                {
                    return result;
                }
            }
            foreach (var interfaceType in type.GetInterfaces())
            {
                var result = GetCollectionInfo(interfaceType);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }
        
        private struct DictionaryCollectionInfo<TKey, TValue> : ICollectionInfo
        {
            public Type ElementType
            {
                get { return typeof(KeyValuePair<TKey, TValue>); }
            }

            public Type KeyType
            {
                get { return typeof(TKey); }
            }

            public bool IsDictionary
            {
                get { return true; }
            }

            public object GetItemFromKey(object collection, object key)
            {
                if (collection == null || key == null)
                {
                    return null;
                }
                TValue value;
                if (((IDictionary<TKey, TValue>) collection).TryGetValue((TKey) key, out value))
                {
                    return new KeyValuePair<TKey, TValue>((TKey) key, value);
                }
                return null;
            }

            public IEnumerable GetKeys(object collection)
            {
                if (null == collection)
                {
                    return new TKey[0];
                }
                return ((IDictionary<TKey, TValue>)collection).Keys;
            }

            public IEnumerable GetItems(object collection)
            {
                return (IDictionary<TKey, TValue>) collection;
            }
        }

        private struct ListCollectionInfo<TItem> : ICollectionInfo
        {
            public Type KeyType
            {
                get { return typeof (int); }
            }

            public Type ElementType
            {
                get { return typeof(TItem); }
            }

            public bool IsDictionary
            {
                get { return false; }
            }

            public object GetItemFromKey(object collection, object key)
            {
                var list = collection as IList<TItem>;
                if (null == list)
                {
                    return null;
                }
                int? index = key as int?;
                if (!index.HasValue || index < 0 || index >= list.Count)
                {
                    return null;
                }
                return list[index.Value];
            }

            public IEnumerable GetKeys(object collection)
            {
                var list = collection as IList<TItem>;
                if (null == list)
                {
                    return Enumerable.Range(0, 0);
                }
                return Enumerable.Range(0, list.Count);
            }

            public IEnumerable GetItems(object collection)
            {
                return collection as IList<TItem> ?? new TItem[0];
            }
        }
    }
}
