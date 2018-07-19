/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.AuditLog
{
    public class Reflector<T>
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
                var attributes = property.GetCustomAttributes(false);
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

        /// <summary>
        /// Diffs the <see cref="ObjectInfo&lt;T&gt;.ObjectPair"/> contained in the given objectinfo 
        /// </summary>
        /// <param name="objectInfo">The set of objects to compare</param>
        /// <param name="rootProperty">The property of the initial <see cref="ObjectInfo&lt;T&gt;.ObjectPair"/></param>
        /// <param name="timeStamp">Time at which the tree was created (usually DateTime.Now)</param>
        /// <returns>Tree representing all differences found</returns>
        public static DiffTree BuildDiffTree(ObjectInfo<object> objectInfo, Property rootProperty, DateTime? timeStamp)
        {
            return new DiffTree(
                BuildDiffTree(objectInfo, rootProperty, PropertyPath.Root.Property(rootProperty.PropertyName), null),
                timeStamp ?? DateTime.Now);
        }

        /// <summary>
        /// Creates an expanded diff tree for the given object
        /// </summary>
        /// <param name="rootPair">The root objects for the object</param>
        /// <param name="rootProperty">The property of the initial object</param>
        /// <param name="obj">The object to diff</param>
        /// <param name="timeStamp">Time at which the tree was created (usually DateTime.Now)</param>
        /// <returns></returns>
        public static DiffTree BuildDiffTree(ObjectPair<object> rootPair, Property rootProperty, T obj, DateTime? timeStamp = null)
        {
            var objInfo = new ObjectInfo<object>(null, obj, null, null, rootPair.OldObject, rootPair.NewObject);

            return new DiffTree(BuildDiffTree(objInfo, rootProperty, PropertyPath.Root.Property(rootProperty.PropertyName), null, true),
                timeStamp ?? DateTime.Now);
        }

        private static DiffNode BuildDiffTree(ObjectInfo<object> objectInfo, Property thisProperty, PropertyPath propertyPath, object elementKey, bool expand = false, IList<object> defaults = null)
        {
            if (objectInfo.ObjectPair.ReferenceEquals() && !expand)
                return null;

            DiffNode resultNode = thisProperty.IsCollectionElement
                ? new ElementPropertyDiffNode(thisProperty, propertyPath, objectInfo.ObjectPair, elementKey, null, expand)
                : new PropertyDiffNode(thisProperty, propertyPath, objectInfo.ObjectPair, null, expand);

            var expandAnyways = false;
            var auditObjPair = objectInfo.ObjectPair.Transform(AuditLogObject.GetAuditLogObject);
            var nameChanged = auditObjPair.OldObject.IsName && auditObjPair.NewObject.IsName &&
                              auditObjPair.OldObject.AuditLogText != auditObjPair.NewObject.AuditLogText;

            if (ReferenceEquals(objectInfo.OldObject, null) && !ReferenceEquals(objectInfo.NewObject, null) &&
                auditObjPair.NewObject.IsName || nameChanged &&
                !typeof(IIdentiyContainer).IsAssignableFrom(thisProperty.GetPropertyType(objectInfo.ObjectPair))) // IIdentiyContainer name changes are actually displayed, since we match IIdentiyContainers by their global indices
            {
                if (!thisProperty.IsRoot)
                {
                    if (!expand)
                        resultNode.IsFirstExpansionNode = true;

                    expandAnyways = expand = true;
                }
            }

            defaults = defaults ?? new List<object>();
            if (expand && Reflector.ProcessDefaults(objectInfo, thisProperty, ref defaults))
            {
                return expandAnyways ? resultNode : null; // Don't expand if we changed to a default object
            }

            // We only compare sub properties if both objects are non null, unless we're expanding
            // and only the old object is null
            if (ReferenceEquals(objectInfo.OldObject, null) && !expand || ReferenceEquals(objectInfo.NewObject, null))
                return resultNode;

            // Deal with Collections
            var collection = Reflector.GetCollectionInfo(thisProperty.GetPropertyType(objectInfo.ObjectPair),
                objectInfo.ObjectPair);

            if (collection != null)
            {
                var result = Reflector.DiffCollections(objectInfo.ChangeObjectPair(collection.Collections), collection,
                    thisProperty, propertyPath, expand, defaults);

                if (result != null && resultNode.IsFirstExpansionNode)
                    result.IsFirstExpansionNode = true;

                return result;
            }

            // Properties that should directly be checked for equality -- stop recursion
            if (!thisProperty.DiffProperties)
            {
                // Properties that are not TrackParents and not collections are simply compared
                // Also make sure that new val doesn't equal any of the default objects
                if ((!objectInfo.ObjectPair.Equals() || expand) &&
                        (!expand || thisProperty.IgnoreDefaultParent || !defaults.Any(d => Equals(d, objectInfo.ObjectPair.NewObject))))
                    return resultNode;
                return null;
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
                var node = Reflector.BuildDiffTree(valType, newObjectInfo, property, newPropertyPath, null,
                    expand, defaults);

                if (node != null)
                    resultNode.Nodes.Add(node);
            }

            // If there are child nodes, we always want to return the parent node,
            // but if it's just the root node or a diffparent (meaning only the object reference changed)
            // we return null (meaning the objects are equivalent)
            if (resultNode.Nodes.Count == 0 && thisProperty.DiffProperties && !expandAnyways)
                return null;

            return resultNode;
        }

        /// <summary>
        /// Compares two objects based on their properties which are marked with Diff attributes
        /// Structurally similar to BuildDiffTree, but no nodes are created, it simply checks for equality
        /// </summary>
        public static bool Equals(ObjectPair<T> objectPair)
        {
            if (objectPair.ReferenceEquals())
                return true;

            if (ReferenceEquals(objectPair.OldObject, null) || ReferenceEquals(objectPair.NewObject, null))
                return false;

            foreach (var property in _properties)
            {
                var localProperty = property;
                var pair = objectPair.Transform(obj => localProperty.GetValue(obj));

                if (pair.ReferenceEquals())
                    continue;

                if (ReferenceEquals(pair.OldObject, null) || ReferenceEquals(pair.NewObject, null))
                    return false;

                var auditObjects = pair.Transform(AuditLogObject.GetAuditLogObject);

                if (auditObjects.OldObject.IsName && auditObjects.NewObject.IsName)
                {
                    if (auditObjects.OldObject.AuditLogText != auditObjects.NewObject.AuditLogText)
                        return false;
                }

                var valType = property.GetPropertyType(pair);

                var collection = Reflector.GetCollectionInfo(valType, pair);
                if (!property.DiffProperties)
                {
                    if (collection != null)
                    {
                        if (!Reflector.CollectionEquals(collection,
                                (type, objPair) => objPair.Equals()))
                            return false;
                    }
                    else if (!pair.Equals())
                        return false;
                }
                else
                {
                    if (collection != null)
                    {
                        if (!Reflector.CollectionEquals(collection, Reflector.Equals))
                            return false;
                    }
                    else if (!Reflector.Equals(valType, pair))
                    {
                        return false;
                    }                      
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Wrapper class for <see cref="Reflector&lt;T&gt;"/> that takes a Type as parameter in each of its function
    /// and constructs a <see cref="Reflector&lt;T&gt;"/> object using reflection and calls the corresponding
    /// function. Also contains helper functions that don't make use of the <see cref="Reflector&lt;T&gt;._properties"/> from <see cref="Reflector&lt;T&gt;"/>
    /// </summary>
    public partial class Reflector
    {
        public static bool ProcessDefaults(ObjectInfo<object> objectInfo, Property property, ref IList<object> defaults)
        {
            defaults = defaults.Where(d => d != null).Select(property.GetValue).ToList();

            var comparable = objectInfo.NewObject as IAuditLogComparable;
            if (comparable != null)
            {
                var defaultObj = comparable.GetDefaultObject(objectInfo);
                defaults.Add(defaultObj);
            }

            var defaultVals = property.DefaultValues;
            if (defaultVals != null)
            {
                defaults.AddRange(defaultVals.Values);
                if (defaultVals.IsDefault(objectInfo.NewObject, objectInfo.NewParentObject))
                    return true;
            }

            return !property.IgnoreDefaultParent && defaults.Any(d => ReferenceEquals(d, objectInfo.NewObject));
        }

        /// <summary>
        /// Determines whether two IAuditLogObjects (most likely from two different
        /// collections) match, meaning either their name or global index are the same
        /// </summary>
        private static bool Matches(Type type, ObjectPair<IAuditLogObject> element, bool diffProperties)
        {
            if (element.ReferenceEquals())
                return true;

            if (ReferenceEquals(element.OldObject, null) || ReferenceEquals(element.NewObject, null))
                return false;

            var objectPair = element.Transform(AuditLogObject.GetObject);

            if (objectPair.ReferenceEquals())
                return true;
            if (ReferenceEquals(objectPair.OldObject, null) || ReferenceEquals(objectPair.NewObject, null))
                return false;

            type = Property.GetPropertyType(type, objectPair);
            if (typeof(IIdentiyContainer).IsAssignableFrom(type))
            {
                // ReSharper disable once PossibleNullReferenceException
                return (objectPair.OldObject as IIdentiyContainer).Id.GlobalIndex
                    // ReSharper disable once PossibleNullReferenceException
                       == (objectPair.NewObject as IIdentiyContainer).Id.GlobalIndex;
            }

            if (element.OldObject.IsName)
            {
                return element.OldObject.AuditLogText == element.NewObject.AuditLogText;
            }
            else
            {
                // Match by equality
                return diffProperties
                    ? Equals(type, objectPair)
                    : objectPair.Equals();
            }
        }

        private static DiffNode DiffCollections(ObjectInfo<object> objectInfo, Collection collection, Property property,
            PropertyPath propertyPath, bool expand, IList<object> defaults,
            Func<Type, ObjectInfo<object>, Property, PropertyPath, object, bool, IList<object>, DiffNode> func)
        {
            var oldKeys = collection.Info.GetKeys(collection.Collections.OldObject).OfType<object>().ToArray();
            var newKeys = collection.Info.GetKeys(collection.Collections.NewObject).OfType<object>().ToList();

            var resultNode = new PropertyDiffNode(property, propertyPath, objectInfo.ObjectPair, null,
                expand);

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
                            ? Equals(collection.Info.ElementValueType, valuePair)
                            : valuePair.Equals();
                    });
                });

                if (isDefault)
                    return null;

                oldKeys = new object[0];
            }

            // We can't go deeper into collection defaults
            if(defaults.Count > 0)
                defaults = null;

            var newElemObjs = newKeys
                .Select(k => AuditLogObject.GetAuditLogObject(collection.Info.GetItemValueFromKey(collection.Collections.NewObject, k))).ToList();

            // Collection is now the parent
            objectInfo = objectInfo.ChangeParentPair(objectInfo.ObjectPair);

            foreach (var key in oldKeys)
            {
                var oldKey = key;
                var oldElem = collection.Info.GetItemValueFromKey(collection.Collections.OldObject, oldKey);

                var oldElemObj = AuditLogObject.GetAuditLogObject(oldElem);

                // Try to find a matching object
                var index = newElemObjs.FindIndex(o =>
                {
                    var pair = ObjectPair.Create(oldElemObj, o);
                    return Matches(property.GetPropertyType(pair), pair, property.DiffProperties);
                });

                if (index >= 0)
                {
                    var newElem = collection.Info.GetItemValueFromKey(collection.Collections.NewObject, newKeys[index]);
                    var info = objectInfo.ChangeObjectPair(ObjectPair.Create(oldElem, newElem));
                    var node = func(property.GetPropertyType(info.ObjectPair), info, property,
                        propertyPath.LookupByKey(newKeys[index].ToString()), newKeys[index], expand, defaults);

                    // Make sure elements are unequal
                    if (node != null)
                        resultNode.Nodes.Add(node);

                    // This item has been dealt with, so remove it
                    newElemObjs.RemoveAt(index);
                    newKeys.RemoveAt(index);
                }
                else
                {
                    // If the element can't be found, we assume it was removed
                    resultNode.Nodes.Add(new ElementDiffNode(property, propertyPath.LookupByKey(oldKey.ToString()),
                        oldElem, key, true, null, expand));
                }
            }

            // The keys that are left over are most likely elements that were added to the colletion
            var added = newKeys.Select(k =>
                new ElementDiffNode(property, propertyPath.LookupByKey(k.ToString()),
                    collection.Info.GetItemValueFromKey(collection.Collections.NewObject, k), k, false, null, expand)).ToList();

            foreach (var node in added.Where(n => expand || AuditLogObject.GetAuditLogObject(n.Element).IsName))
            {
                var info = objectInfo.ChangeObjectPair(ObjectPair.Create(null, node.Element));
                var newNode = func(node.Property.GetPropertyType(node.Element), info, node.Property,
                    node.PropertyPath, node.ElementKey, true, defaults);
                if (newNode != null)
                    node.Nodes.AddRange(newNode.Nodes);
                node.IsFirstExpansionNode = !expand;
            }

            resultNode.Nodes.AddRange(added);
            if (resultNode.Nodes.Count == 0 && !expand)
                return null;
            return resultNode;
        }

        /// <summary>
        /// Diffs two collections
        /// </summary>
        /// <param name="objectInfo">The collection objects</param>
        /// <param name="collection">Collection object to retrieve elements from collection</param>
        /// <param name="property">The property of the collection</param>
        /// <param name="propertyPath">The path to the collectins property</param>
        /// <param name="expand">Whether to expand subproperties</param>
        /// <param name="defaults">Default collections</param>
        /// <returns>A node representing the differences found in the collections</returns>
        public static DiffNode DiffCollections(ObjectInfo<object> objectInfo, Collection collection, Property property,
            PropertyPath propertyPath, bool expand, IList<object> defaults)
        {
            if (objectInfo.ObjectPair.ReferenceEquals() && !expand)
                return null;

            if (!property.DiffProperties)
            {
                // Here we just want to diff the collection elements and dont recurse further,
                // so the callback simply checks for equality
                return DiffCollections(objectInfo, collection, property, propertyPath,
                    expand, defaults,
                    (type, objInfo, prop, propPath, key, exp, def) =>
                    {
                        if (!objInfo.ObjectPair.Equals() || exp)
                        {
                            return new ElementPropertyDiffNode(prop, propPath,
                                objInfo.ObjectPair, key, null, expand);
                        }

                        return null;
                    });
            }
            else
            {
                // Since the collection is DiffParent, we have to go into the elements and compare their properties
                return DiffCollections(objectInfo, collection, property, propertyPath,
                    expand, defaults, BuildDiffTree);
            }
        }

        /// <summary>
        /// Checks whether two collections are exactly equal, meaning all elements are the same and at the same
        /// indices/keys.
        /// </summary>
        public static bool CollectionEquals(Collection collection, Func<Type, ObjectPair<object>, bool> func)
        {
            var oldKeys = collection.Info.GetKeys(collection.Collections.OldObject).OfType<object>().ToArray();
            var newKeys = collection.Info.GetKeys(collection.Collections.NewObject).OfType<object>().ToArray();

            if (oldKeys.Length != newKeys.Length)
                return false;

            for (var i = 0; i < oldKeys.Length; ++i)
            {
                if (oldKeys[i] != newKeys[i])
                    return false;

                var pair = ObjectPair.Create(collection.Info.GetItemValueFromKey(collection.Collections.OldObject, oldKeys[i]),
                    collection.Info.GetItemValueFromKey(collection.Collections.NewObject, newKeys[i]));

                if (!func(collection.Info.ElementValueType, pair))
                    return false;
            }

            return true;
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
                   (type.IsGenericType && typeof(IEnumerable<>).IsAssignableFrom(type.GetGenericTypeDefinition()));
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

        // ReSharper disable PossibleNullReferenceException
        public static object ToArray(Type type, object obj)
        {
            var enumerableType = typeof(Enumerable);
            var toArray = enumerableType.GetMethod("ToArray", BindingFlags.Public | BindingFlags.Static); // Not L10N
            Assume.IsNotNull(toArray);
            toArray = toArray.MakeGenericMethod(type);
            Assume.IsNotNull(toArray);
            return toArray.Invoke(null, new[] { obj });
        }

        #region Wrapper functions

        public static IList<Property> GetProperties(Type type)
        {
            var reflectorType = typeof(Reflector<>).MakeGenericType(type);
            var property = reflectorType.GetProperty("Properties", BindingFlags.Public | BindingFlags.Static); // Not L10N

            Assume.IsNotNull(property);

            var reflector = Activator.CreateInstance(reflectorType);
            
            return (IList<Property>) property.GetValue(reflector);
        }

        public static DiffNode BuildDiffTree(Type objectType, ObjectInfo<object> objectInfo, Property thisProperty, PropertyPath propertyPath, object elementKey, bool expand, IList<object> defaults)
        {
            var type = typeof(Reflector<>).MakeGenericType(objectType);
            var buildDiffTree = type.GetMethod("BuildDiffTree", BindingFlags.NonPublic | BindingFlags.Static, // Not L10N
                null,
                new[]
                {
                    typeof(ObjectInfo<object>), typeof(Property), typeof(PropertyPath),
                    typeof(object), typeof(bool), typeof(IList<object>)
                },
                null);

            Assume.IsNotNull(buildDiffTree);

            var reflector = Activator.CreateInstance(type);

            return (DiffNode)buildDiffTree.Invoke(reflector, new[] { objectInfo, thisProperty, propertyPath, elementKey, expand, defaults });
        }

        public static bool Equals(Type objectType, ObjectPair<object> objPair)
        {
            var objectPairType = typeof(ObjectPair<>).MakeGenericType(objectType);

            var type = typeof(Reflector<>).MakeGenericType(objectType);
            var equals = type.GetMethod("Equals", BindingFlags.Public | BindingFlags.Static, null, // Not L10N
                new[] { objectPairType }, null);

            Assume.IsNotNull(equals);

            // TODO: just make Equals non generic and call GetProperties using reflection?
            var create = objectPairType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static, null, // Not L10N
                new[] {objectType, objectType}, null);

            Assume.IsNotNull(create);
            var castedPair = create.Invoke(null, new[] { objPair.OldObject, objPair.NewObject });

            var reflector = Activator.CreateInstance(type);
            return (bool)equals.Invoke(reflector, new[] { castedPair });
        }
        #endregion
        // ReSharper restore PossibleNullReferenceException
    }
}