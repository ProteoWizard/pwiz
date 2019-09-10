/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UWboo
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Reflection;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.AuditLog
{
    /// <summary>
    /// Enumerator that caches values as they are enumerated, so that the enumerator can be reset
    /// without re-enumerating already enumerated items later on
    /// </summary>
    public class CachingEnumerator<T> : IEnumerator<T>
    {
        private readonly IEnumerator<T> _enumerator;
        private readonly List<T> _items;
        private int _enumeratorIndex;
        private int _enumerated;

        public CachingEnumerator(IEnumerator<T> enumerator)
        {
            _enumerator = enumerator;
            _items = new List<T>();
            _enumeratorIndex = -1;
            _enumerated = 0;
        }

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            if (_enumeratorIndex == _enumerated - 1)
            {
                var hasNext = _enumerator.MoveNext();
                if (hasNext)
                {
                    _enumeratorIndex = _enumerated++;
                    _items.Add(_enumerator.Current);
                }

                return hasNext;
            }

            ++_enumeratorIndex;
            return true;
        }

        public void Reset()
        {
            _enumeratorIndex = -1;
        }

        object IEnumerator.Current
        {
            get { return Current; }
        }

        public T Current
        {
            get { return _items[_enumeratorIndex]; }
        }
    }

    public class DiffNodeEnumerator : CachingEnumerator<DiffNode>
    {
        public DiffNodeEnumerator(IEnumerator<DiffNode> enumerator) : base(enumerator)
        {
        }
    }

    // ReSharper disable once UnusedTypeParameter
    public partial class Reflector<T>
    {
        // ReSharper disable once StaticMemberInGenericType
        private static readonly List<Property> _properties;

        static Reflector()
        {
            if (!(typeof(T).IsClass || typeof(T).IsInterface))
            {
                _properties = null;
                return;
            }

            _properties = new List<Property>();
            foreach (var property in GetPropertyInfos())
            {
                var attributes = Attribute.GetCustomAttributes(property, typeof(TrackAttributeBase), true);
                var trackAttribute = attributes.OfType<TrackAttributeBase>().FirstOrDefault();

                if (trackAttribute != null)
                {
                    _properties.Add(new Property(property, trackAttribute));
                }
            }
        }

        private static IEnumerable<PropertyInfo> GetPropertyInfos()
        {
            // Also get properties from interfaces
            return new[] { typeof(T) }.Concat(typeof(T).GetInterfaces()).SelectMany(t => t.GetProperties());
        }

        /// <summary>
        /// Gets all properties of the current type that are diffed
        /// </summary>
        public static IList<Property> Properties
        {
            get { return ImmutableList<Property>.ValueOf(_properties); }
        }

        public static DiffNodeEnumerator EnumerateDiffNodes(ObjectInfo<object> objectInfo, Property rootProperty, SrmDocument.DOCUMENT_TYPE defaultDocumentType, bool expand, Func<DiffNode, bool> nodeSelector = null)
        {
            return new DiffNodeEnumerator(EnumerateDiffNodes(objectInfo, rootProperty, defaultDocumentType, expand,
                PropertyPath.Root.Property(rootProperty.PropertyName), null, null, nodeSelector, null, 0));
        }

        public static DiffNodeEnumerator EnumerateDiffNodes(ObjectPair<object> rootPair, Property rootProperty, SrmDocument.DOCUMENT_TYPE defaultDocumentType, T obj, Func<DiffNode, bool> nodeSelector = null)
        {
            var objInfo = new ObjectInfo<object>()
                .ChangeNewObject(obj)
                .ChangeRootObjectPair(rootPair);
            return new DiffNodeEnumerator(EnumerateDiffNodes(objInfo, rootProperty, defaultDocumentType, true));
        }

        // TODO: make this function and its overloads in nongeneric Reflector class simpler by introducing new class storing parameters
        private static IEnumerator<DiffNode> EnumerateDiffNodes(ObjectInfo<object> objectInfo, Property thisProperty, SrmDocument.DOCUMENT_TYPE defaultDocumentType,
            bool expand, PropertyPath propertyPath, object elementKey, IList<object> defaults, Func<DiffNode, bool> nodeSelector, DiffNode resultNode, int stackDepth)
        {
            var docType = objectInfo.NewObject is SrmDocument document ? document.DocumentType
                : objectInfo.NewParentObject is SrmDocument documentp ? documentp.DocumentType
                : objectInfo.NewRootObject is SrmDocument documentr ? documentr.DocumentType
                : SrmDocument.DOCUMENT_TYPE.none;

            if (docType == SrmDocument.DOCUMENT_TYPE.none)
            {
                docType = defaultDocumentType;
            }

            nodeSelector = nodeSelector ?? (n => true);
            if (objectInfo.ObjectPair.ReferenceEquals() && !expand)
                yield break;

            resultNode = resultNode ?? (thisProperty.IsCollectionElement
                ? new ElementPropertyDiffNode(thisProperty, propertyPath, objectInfo.ObjectPair, elementKey, docType, null, expand)
                : new PropertyDiffNode(thisProperty, propertyPath, objectInfo.ObjectPair, docType, null, expand));

            var expandAnyways = false;

            var auditObjPair = objectInfo.ObjectPair.Transform(AuditLogObject.GetAuditLogObject);
            var nameChanged = auditObjPair.OldObject.IsName && auditObjPair.NewObject.IsName &&
                                auditObjPair.OldObject.AuditLogText != auditObjPair.NewObject.AuditLogText;

            if (ReferenceEquals(objectInfo.OldObject, null) && !ReferenceEquals(objectInfo.NewObject, null) &&
                auditObjPair.NewObject.IsName || nameChanged &&
                // IIdentiyContainer name changes are actually displayed, since we match IIdentiyContainers by their global indices
                !typeof(IIdentiyContainer).IsAssignableFrom(thisProperty.GetPropertyType(objectInfo.ObjectPair)))
            {
                if (!thisProperty.IsRoot)
                {
                    if (!expand)
                        resultNode.IsFirstExpansionNode = true;

                    expandAnyways = expand = true;
                }
            }

            // If the object is an IAuditLogObject but not a name one, we don't care about subproperties
            //if (expand && typeof(IAuditLogObject).IsAssignableFrom(thisProperty.GetPropertyType(objectInfo.ObjectPair)) &&
            //    !auditObjPair.OldObject.IsName && !auditObjPair.NewObject.IsName)
            //    expand = false;

            defaults = defaults ?? new List<object>();
            if (expand && Reflector.ProcessDefaults(objectInfo, thisProperty, ref defaults, out var ignore))
            {
                // Don't expand if we changed to a default object
                if (expandAnyways && !ignore && nodeSelector(resultNode)) // Only show this object if it shouldn't be fully ignored if it's a default object (for instance: small molecule only properties)
                    yield return resultNode;
                yield break;
            }

            // We only compare sub properties if both objects are non null, unless we're expanding
            // and only the old object is null
            if (ReferenceEquals(objectInfo.OldObject, null) && !expand || ReferenceEquals(objectInfo.NewObject, null))
            {
                if (nodeSelector(resultNode))
                    yield return resultNode;
                yield break;
            }

            // Deal with Collections
            var collection = Reflector.GetCollectionInfo(thisProperty.GetPropertyType(objectInfo.ObjectPair),
                objectInfo.ObjectPair);

            if (collection != null)
            {
                var nodeIter = Reflector.EnumerateCollectionDiffNodes(objectInfo.ChangeObjectPair(collection.Collections), collection,
                    thisProperty, propertyPath, docType, expand, defaults, nodeSelector, resultNode, stackDepth);

                while (nodeIter.MoveNext())
                    yield return nodeIter.Current;
                    
                yield break;
            }

            // Properties that should directly be checked for equality -- stop recursion
            if (!thisProperty.DiffProperties)
            {
                // Properties that are not TrackParents and not collections are simply compared
                // Also make sure that new val doesn't equal any of the default objects
                if (nodeSelector(resultNode) && (!objectInfo.ObjectPair.Equals() || expand) &&
                        (!expand || thisProperty.IgnoreDefaultParent || !defaults.Any(d => Equals(d, objectInfo.ObjectPair.NewObject))))
                    yield return resultNode;
                yield break;
            }

            objectInfo = objectInfo.ChangeParentPair(objectInfo.ObjectPair);

            // Compare properties
            foreach (var property in _properties)
            {
                var newPropertyPath = property.AddProperty(propertyPath);

                var newObjectInfo = objectInfo
                    .ChangeOldObject(ReferenceEquals(objectInfo.OldObject, null)
                        ? null
                        : property.GetValue(objectInfo.OldObject))
                    .ChangeNewObject(property.GetValue(objectInfo.NewObject));

                var valType = property.GetPropertyType(newObjectInfo.ObjectPair);
                var nodeIter = Reflector.EnumerateDiffNodes(valType, newObjectInfo, property, docType, expand, newPropertyPath,
                    null, defaults, nodeSelector, null, stackDepth);

                DiffNode current = null;
                while (nodeIter.MoveNext())
                {
                    current = nodeIter.Current;
                    yield return current;
                }

                if (current != null)
                    resultNode.Nodes.Add(current);
            }

            // If there are child nodes, we always want to return the parent node,
            // but if it's just the root node or a diffparent (meaning only the object reference changed)
            // we return null (meaning the objects are equivalent)
            if (resultNode.Nodes.Count == 0 && thisProperty.DiffProperties && !expandAnyways)
                yield break;

            if(nodeSelector(resultNode))
                yield return resultNode;
        }
    }

    /// <summary>
    /// Wrapper class for <see cref="Reflector&lt;T&gt;"/> that takes a Type as parameter in each of its function
    /// and constructs a <see cref="Reflector&lt;T&gt;"/> object using reflection and calls the corresponding
    /// function. Also contains helper functions that don't make use of the <see cref="Reflector&lt;T&gt;._properties"/> from <see cref="Reflector&lt;T&gt;"/>
    /// </summary>
    public static partial class Reflector
    {
        public static bool Equals(Type objectType, ObjectInfo<object> objInfo, Property property, SrmDocument.DOCUMENT_TYPE docType)
        {
            return !EnumerateDiffNodes(objectType, objInfo, property, docType, false).MoveNext();
        }

        public static IEnumerator<DiffNode> EnumerateDiffNodes(Type objectType, ObjectInfo<object> objectInfo, Property thisProperty, SrmDocument.DOCUMENT_TYPE defaultDocumentType,
            bool expand, PropertyPath propertyPath, object elementKey, IList<object> defaults, Func<DiffNode, bool> resultSelector, DiffNode resultNode, int stackDepth)
        {
            if (++stackDepth > MAX_STACK_DEPTH)
                throw new StackOverflowException();

            var type = typeof(Reflector<>).MakeGenericType(objectType);
            var enumerateDiffNodes = type.GetMethod(@"EnumerateDiffNodes", BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(ObjectInfo<object>), typeof(Property), typeof(SrmDocument.DOCUMENT_TYPE), typeof(bool), typeof(PropertyPath),
                    typeof(object), typeof(IList<object>), typeof(Func<DiffNode, bool>), typeof(PropertyDiffNode), typeof(int)
                },
                null);

            Assume.IsNotNull(enumerateDiffNodes);

            var reflector = Activator.CreateInstance(type);

            // ReSharper disable once PossibleNullReferenceException
            return (IEnumerator<DiffNode>)enumerateDiffNodes.Invoke(reflector, new[] { objectInfo, thisProperty, defaultDocumentType, expand, propertyPath, elementKey, defaults, resultSelector, resultNode, stackDepth });
        }

        private static bool Matches(Property property, ObjectInfo<object> objectInfo)
        {
            var objectPair = objectInfo.ObjectPair;

            if (objectPair.ReferenceEquals())
                return true;
            if (ReferenceEquals(objectPair.OldObject, null) || ReferenceEquals(objectPair.NewObject, null))
                return false;

            var type = property.GetPropertyType(objectPair);
            if (typeof(IIdentiyContainer).IsAssignableFrom(type))
            {
                // ReSharper disable once PossibleNullReferenceException
                return ((IIdentiyContainer) objectPair.OldObject).Id.GlobalIndex
                       // ReSharper disable once PossibleNullReferenceException
                       == ((IIdentiyContainer) objectPair.NewObject).Id.GlobalIndex;
            }

            var auditLogObjects = objectInfo.ObjectPair.Transform(AuditLogObject.GetAuditLogObject);
            if (auditLogObjects.OldObject.IsName && auditLogObjects.NewObject.IsName &&
                auditLogObjects.OldObject.AuditLogText == auditLogObjects.NewObject.AuditLogText)
                return true;

            return Equals(type, objectInfo.ToObjectType(), property, SrmDocument.DOCUMENT_TYPE.none);
        }

        /// <summary>
        /// Diffs two collections
        /// </summary>
        /// <param name="objectInfo">The collection objects</param>
        /// <param name="collection">Collection object to retrieve elements from collection</param>
        /// <param name="property">The property of the collection</param>
        /// <param name="propertyPath">The path to the collectins property</param>
        /// <param name="defaultDocumentType">May be used to decide whether to perform "peptide"->"molecule" translation on human readable log entry</param>
        /// <param name="expand">Whether to expand subproperties</param>
        /// <param name="defaults">Default collections</param>
        /// <param name="nodeSelector">Selector to be applied to each node to check if it should be included</param>
        /// <param name="existingNode">Node to use instead of creating a new one</param>
        /// <param name="stackDepth">Depth of stack, used for stack overflow detection</param>
        /// <returns>A node representing the differences found in the collections</returns>
        public static IEnumerator<DiffNode> EnumerateCollectionDiffNodes(ObjectInfo<object> objectInfo, Collection collection, Property property,
            PropertyPath propertyPath, SrmDocument.DOCUMENT_TYPE defaultDocumentType, bool expand, IList<object> defaults, Func<DiffNode, bool> nodeSelector, DiffNode existingNode, int stackDepth)
        {
            var docType = objectInfo.NewObject is SrmDocument document ? document.DocumentType
                : objectInfo.NewParentObject is SrmDocument documentp ? documentp.DocumentType
                : objectInfo.NewRootObject is SrmDocument documentr ? documentr.DocumentType
                : SrmDocument.DOCUMENT_TYPE.none;
            if (docType == SrmDocument.DOCUMENT_TYPE.none)
            {
                docType = defaultDocumentType;
            }

            nodeSelector = nodeSelector ?? (n => true);

            var oldKeys = collection.Info.GetKeys(collection.Collections.OldObject).OfType<object>().ToArray();
            var newKeys = collection.Info.GetKeys(collection.Collections.NewObject).OfType<object>().ToList();

            var resultNode = CollectionPropertyDiffNode.FromPropertyDiffNode(existingNode as PropertyDiffNode) ??
                         new CollectionPropertyDiffNode(property, propertyPath, objectInfo.ObjectPair, docType, null, expand);

            if (oldKeys.Length > 0 && newKeys.Count == 0)
                resultNode = resultNode.SetRemovedAll();

            // All nodes added from here on are element nodes and their property type
            // will be overriden
            property = property.ChangeTypeOverride(collection.Info.ElementValueType);

            // If we assume elements are unequal, we assume the old collection was empty
            // and all elements were added
            if (expand)
            {
                // For collections, we want every key and element to match, it would be odd to just exclude
                // elements that are in the default collection but include the ones that aren't
                var isDefault = defaults.Any(defaultCollection =>
                {
                    if (defaultCollection == null)
                        return false;

                    // Make sure defaultCollection can be compared to collection.Collections objects
                    defaultCollection = EnsureCollectionInfoType(defaultCollection.GetType(), defaultCollection);
                    if (defaultCollection == null)
                        return false;

                    var keys = collection.Info.GetKeys(defaultCollection).OfType<object>().ToArray();
                    if (!ArrayUtil.EqualsDeep(newKeys, keys))
                        return false;

                    return keys.All(key =>
                    {
                        var valuePair = ObjectPair.Create(collection.Info.GetItemValueFromKey(defaultCollection, key),
                            collection.Info.GetItemValueFromKey(collection.Collections.NewObject, key));

                        return property.DiffProperties
                            ? Equals(collection.Info.ElementValueType, objectInfo.ChangeObjectPair(valuePair), property, docType)
                            : valuePair.Equals();
                    });
                });

                if (isDefault)
                    yield break;

                oldKeys = new object[0];
            }

            // We can't go deeper into collection defaults
            if (defaults.Count > 0)
                defaults = null;

            var newElems = newKeys
                .Select(k => collection.Info.GetItemValueFromKey(collection.Collections.NewObject, k)).ToList();

            // Collection is now the parent
            objectInfo = objectInfo.ChangeParentPair(objectInfo.ObjectPair);

            foreach (var key in oldKeys)
            {
                var oldKey = key;
                var oldElem = collection.Info.GetItemValueFromKey(collection.Collections.OldObject, oldKey);

                // Try to find a matching object
                var index = newElems.FindIndex(newElem =>
                {
                    var newInfo = objectInfo.ChangeObjectPair(ObjectPair<object>.Create(oldElem, newElem));
                    return Matches(property, newInfo);
                });

                if (index >= 0)
                {
                    var newElem = collection.Info.GetItemValueFromKey(collection.Collections.NewObject, newKeys[index]);
                    var info = objectInfo.ChangeObjectPair(ObjectPair.Create(oldElem, newElem));
                    var nodeIter = EnumerateDiffNodes(property.GetPropertyType(info.ObjectPair), info, property, docType, expand,
                        propertyPath.LookupByKey(newKeys[index].ToString()), newKeys[index], defaults, nodeSelector, null, stackDepth);

                    DiffNode current = null;
                    while (nodeIter.MoveNext())
                    {
                        current = nodeIter.Current;
                        yield return current;
                    }

                    // Make sure elements are unequal
                    if (current != null)
                        resultNode.Nodes.Add(current);

                    // This item has been dealt with, so remove it
                    newElems.RemoveAt(index);
                    newKeys.RemoveAt(index);
                }
                else
                {
                    // If the element can't be found, we assume it was removed
                    var node = new ElementDiffNode(property, propertyPath.LookupByKey(oldKey.ToString()),
                        oldElem, key, true, docType, null, expand);
                    resultNode.Nodes.Add(node);

                    if (nodeSelector(node))
                        yield return node;
                }
            }

            // The keys that are left over are most likely elements that were added to the colletion
            var added = newKeys.Select(k =>
                    new ElementDiffNode(property, propertyPath.LookupByKey(k.ToString()),
                        collection.Info.GetItemValueFromKey(collection.Collections.NewObject, k), k, false, docType, null,
                        expand))
                .Where(n => nodeSelector(n)).ToList();

            foreach (var node in added)
            {
                if (expand || AuditLogObject.IsNameObject(node.Element))
                {
                    var info = objectInfo.ChangeObjectPair(ObjectPair.Create(null, node.Element));
                    var nodeIter = EnumerateDiffNodes(node.Property.GetPropertyType(node.Element), info, node.Property, docType, expand,
                        node.PropertyPath, node.ElementKey, defaults, nodeSelector, node, stackDepth);

                    while (nodeIter.MoveNext())
                        yield return nodeIter.Current;   
                }
                else
                {
                    yield return node;
                }
            }

            resultNode.Nodes.AddRange(added);
            if (resultNode.Nodes.Count == 0 && !expand)
                yield break;

            if (nodeSelector(resultNode))
                yield return resultNode;
        }

        public class Collection
        {
            public Collection(ICollectionInfo info, Type type, ObjectPair<object> collections)
            {
                Info = info;
                Type = type;
                Collections = collections;
            }

            public ICollectionInfo Info { get; private set; }
            public Type Type { get; private set; }
            public ObjectPair<object> Collections { get; private set; }
        }

        public static bool IsCollectionType(Type type)
        {
            return CollectionInfo.ForType(type) != null || type.DeclaringType == typeof(Enumerable) ||
                   (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type.GetGenericTypeDefinition()));
        }

        private static object EnsureCollectionInfoType(Type collectionType, object collection)
        {
            if (!IsCollectionType(collectionType))
                return null;
            if (CollectionInfo.CanGetCollectionInfo(collectionType))
                return collection;

            var genericType = collectionType.GenericTypeArguments.Last();
            return ToArray(genericType, collection);
        }

        public static Collection GetCollectionInfo(Type type, ObjectPair<object> objectPair)
        {
            var collectionInfo = CollectionInfo.ForType(type);
            if (collectionInfo != null)
                return new Collection(collectionInfo, type, objectPair);

            // See if we can convert to an array
            if (IsCollectionType(type))
            {
                var genericType = type.GenericTypeArguments.Last();
                if (objectPair.OldObject != null)
                    objectPair = objectPair.ChangeOldObject(ToArray(genericType, objectPair.OldObject));
                if (objectPair.NewObject != null)
                    objectPair = objectPair.ChangeNewObject(ToArray(genericType, objectPair.NewObject));

                return new Collection(CollectionInfo.ForType(genericType.MakeArrayType()), genericType, objectPair);
            }

            return null;
        }

        public static object ToArray(Type type, object obj)
        {
            var enumerableType = typeof(Enumerable);
            var toArray = enumerableType.GetMethod(@"ToArray", BindingFlags.Public | BindingFlags.Static);
            Assume.IsNotNull(toArray);
            // ReSharper disable once PossibleNullReferenceException
            toArray = toArray.MakeGenericMethod(type);
            Assume.IsNotNull(toArray);
            return toArray.Invoke(null, new[] { obj });
        }

        public static bool ProcessDefaults(ObjectInfo<object> objectInfo, Property property, ref IList<object> defaults, out bool ignore)
        {
            ignore = false;
            defaults = defaults.Where(d => d != null).Select(property.GetValue).ToList();

            var comparable = objectInfo.NewObject as IAuditLogComparable;
            if (comparable != null)
            {
                var defaultObj = comparable.GetDefaultObject(objectInfo);
                defaults.Add(defaultObj);
            }

            var defaultVals = property.DefaultValues;
            if (defaultVals != null && !property.IsCollectionElement) // TODO: consider allowing default collection elements too
            {
                ignore = defaultVals.IgnoreIfDefault;
                defaults.AddRange(defaultVals.Values);
                if (defaultVals.IsDefault(objectInfo.NewObject, objectInfo.NewParentObject))
                    return true;
            }

            return !property.IgnoreDefaultParent && defaults.Any(d => ReferenceEquals(d, objectInfo.NewObject));
        }

        #region Wrapper functions

        // ReSharper disable PossibleNullReferenceException
        public static IList<Property> GetProperties(Type type)
        {
            var reflectorType = typeof(Reflector<>).MakeGenericType(type);
            var property = reflectorType.GetProperty(@"Properties", BindingFlags.Public | BindingFlags.Static);

            Assume.IsNotNull(property);

            var reflector = Activator.CreateInstance(reflectorType);

            return (IList<Property>)property.GetValue(reflector);
        }

        public static DiffNodeEnumerator EnumerateDiffNodes(Type objectType, ObjectInfo<object> objectInfo, Property rootProperty, SrmDocument.DOCUMENT_TYPE docType, bool expand, Func<DiffNode, bool> resultSelector = null)
        {
            var type = typeof(Reflector<>).MakeGenericType(objectType);
            var enumerateDiffNodes = type.GetMethod(@"EnumerateDiffNodes", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(ObjectInfo<object>), typeof(Property), typeof(SrmDocument.DOCUMENT_TYPE), typeof(bool), typeof(Func<DiffNode, bool>) }, null);

            Assume.IsNotNull(enumerateDiffNodes);

            var reflector = Activator.CreateInstance(type);

            return (DiffNodeEnumerator)enumerateDiffNodes.Invoke(reflector, new object[] { objectInfo, rootProperty, docType, expand, resultSelector });
        }

        #endregion
        // ReSharper restore PossibleNullReferenceException
    }
}
