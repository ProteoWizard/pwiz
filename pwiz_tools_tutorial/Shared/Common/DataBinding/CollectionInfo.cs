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
    /// <summary>
    /// Helper for displaying Dictionaries and Lists in a BindingListView.
    /// This is not implemented yet, but users will be able to choose to pivot dictionaries,
    /// and will be able to display all collections in a grid by effectively denormalizing the data.
    /// </summary>
    public class CollectionInfo
    {
        private Func<object, IEnumerable> _fnGetKeys;
        private Func<object, object, object> _fnLookupItemByKey;
        
        public CollectionInfo(Type elementType, Type keyType, Func<object, IEnumerable> fnGetKeys, Func<object, object, object> fnLookupItemByKey)
        {
            ElementType = elementType;
            KeyType = keyType;
            _fnGetKeys = fnGetKeys;
            _fnLookupItemByKey = fnLookupItemByKey;
        }

        public Type ElementType { get; private set; }
        public Type KeyType { get; private set; }
        public bool IsDictionary
        {
            get
            {
                return ElementType.IsGenericType
                       && ElementType.GetGenericTypeDefinition() == typeof (KeyValuePair<,>)
                       && ElementType.GetGenericArguments()[0] == KeyType;
            }
        }

        public object GetItemFromKey(object collection, object key)
        {
            if (collection == null || key == null)
            {
                return null;
            }
            return _fnLookupItemByKey(collection, key);
        }
        public IEnumerable GetKeys(object collection)
        {
            if (collection == null)
            {
                return new object[0];
            }
            return _fnGetKeys(collection);
        }
        public IEnumerable GetItems(object collection)
        {
            return ((IEnumerable) collection);
        }

        private static CollectionInfo GetCollectionInfo(Type type)
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
                            var countProperty = type.GetInterface(typeof(ICollection<>).Name).GetProperty("Count");
                            var itemProperty = type.GetProperty("Item");
                            var fnLookupItem = new Func<object,object,object>((list, key) =>
                                                   {
                                                       var index = key as int?;
                                                       if (!index.HasValue || index < 0 ||
                                                           index >= (int) countProperty.GetValue(list, null))
                                                       {
                                                           return null;
                                                       }
                                                       return itemProperty.GetValue(list, new[] {key});
                                                   });

                            return new CollectionInfo(
                                elementType, typeof (int),
                                list => Enumerable.Range(0, (int) countProperty.GetValue(list, null)),
                                fnLookupItem
                            );
                        }
                        break;
                    case 2:
                        if (type.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                        {
                            var keyType = genericArguments[0];
                            var valueType = genericArguments[1];
                            var elementType = typeof (KeyValuePair<,>).MakeGenericType(keyType, valueType);
                            var elementConstructor = elementType.GetConstructor(new[] {keyType, valueType});
                            var keysProperty = type.GetProperty("Keys");
                            var tryGetValueMethod = type.GetMethod("TryGetValue");
                            var fnLookupItem = new Func<object, object, object>(
                                (dict, key) =>
                                    {
                                        var parameters = new[] {key, null};
                                        if (!(bool) tryGetValueMethod.Invoke(dict, parameters))
                                        {
                                            return null;
                                        }
                                        return elementConstructor.Invoke(parameters);
                                    });
                            return new CollectionInfo(
                                elementType, keyType,
                                dict =>(IEnumerable)keysProperty.GetValue(dict, null),
                                fnLookupItem
                            );
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
        
        public static CollectionInfo ForType(Type type)
        {
            if (!typeof(IEnumerable).IsAssignableFrom(type))
            {
                return null;
            }
            return GetCollectionInfo(type);
        }
    }
}
