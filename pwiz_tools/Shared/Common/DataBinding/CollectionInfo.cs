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
using System.Text;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Helper for displaying Dictionaries and Lists in a BindingListView.
    /// This is not implemented yet, but users will be able to choose to pivot dictionaries,
    /// and will be able to display all collections in a grid by effectively denormalizing the data.
    /// </summary>
    public class CollectionInfo
    {
        public CollectionInfo(Type elementType, Type keyType, Type valueType)
        {
            ElementType = elementType;
            KeyType = keyType;
            ValueType = valueType;
        }

        public Type ElementType { get; private set; }
        public Type KeyType { get; private set; }
        public Type ValueType { get; private set; }
        public bool IsDictionary { get { return KeyType != null;} }

        public object GetValueFromKey(object collection, object key)
        {
            if (!IsDictionary)
            {
                throw new InvalidOperationException("Not a dictionary");
            }
            return ((IDictionary) collection)[key];
        }
        public IEnumerable GetKeys(object collection)
        {
            if (!IsDictionary)
            {
                throw new InvalidOperationException("Not a dictionary");
            }
            return ((IDictionary) collection).Keys;
        }
        public IEnumerable GetValues(object collection)
        {
            if (!IsDictionary)
            {
                throw new InvalidOperationException("Not a dictionary");
            }
            return ((IDictionary)collection).Values;
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
                        if (type.GetGenericTypeDefinition() == typeof(ICollection<>))
                        {
                            var elementType = genericArguments[0];
                            return new CollectionInfo(elementType, null, null);
                        }
                        break;
                    case 2:
                        if (type.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                        {
                            var keyType = genericArguments[0];
                            var valueType = genericArguments[1];
                            var elementType = typeof (KeyValuePair<,>).MakeGenericType(keyType, valueType);
                            return new CollectionInfo(elementType, keyType, valueType);
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
