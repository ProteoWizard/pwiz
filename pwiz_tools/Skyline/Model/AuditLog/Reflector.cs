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
using System.Text;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.AuditLog
{
    public class Reflector<T> where T : class
    {
        // ReSharper disable once StaticMemberInGenericType
        private static readonly List<Property> _properties;

        static Reflector()
        {
            _properties = new List<Property>();
            foreach (var property in typeof(T).GetProperties())
            {
                var attributes = property.GetCustomAttributes(false);
                var trackAttribute = attributes.OfType<TrackAttributeBase>().FirstOrDefault();

                if (trackAttribute != null)
                {
                    _properties.Add(new Property(property, trackAttribute));
                }
            }
        }

        public static IList<Property> Properties
        {
            get { return ImmutableList<Property>.ValueOf(_properties); }
        }

        public static DiffTree BuildDiffTree(Property thisProperty, T oldObj, T newObj, DateTime? timeStamp)
        {
            return new DiffTree(BuildDiffTree(thisProperty, oldObj, newObj), timeStamp ?? DateTime.Now);
        }

        public static DiffNode BuildDiffTree(Property thisProperty, T oldObj, T newObj)
        {
            return BuildDiffTree(thisProperty, oldObj, newObj, PropertyPath.Root, null);
        }

        private static DiffNode BuildDiffTree(Property thisProperty, T oldObj, T newObj, PropertyPath propertyPath, object elementKey, bool expand = false)
        {
            if (ReferenceEquals(oldObj, newObj) && !expand)
                return null;

            // thisProperty.PropertyInfo is null only upon the initial call, where Property.ROOT is passed.
            // Therefore the first object to diff can't be a collection.
            var collectionDiff = thisProperty.HasPropertyInfo &&
                                 thisProperty.IsCollectionElement;

            // The propertyPath does not contain the current property at this point
            if (collectionDiff)
                propertyPath = propertyPath.LookupByKey(elementKey.ToString());
            else if (thisProperty.HasPropertyInfo)
                propertyPath = thisProperty.AddProperty(propertyPath);

            DiffNode resultNode = collectionDiff
                ? new ElementPropertyDiffNode(thisProperty, propertyPath, oldObj, newObj, elementKey, null, expand)
                : new PropertyDiffNode(thisProperty, propertyPath, oldObj, newObj, null, expand);

            if (!expand)
            {
                var oldAuditObj = AuditLogObject.GetAuditLogObject(oldObj);
                var newAuditObj = AuditLogObject.GetAuditLogObject(newObj);
                if (ReferenceEquals(oldObj, null) && !ReferenceEquals(newObj, null) && newAuditObj.IsName ||
                    (oldAuditObj.IsName && newAuditObj.IsName && oldAuditObj.AuditLogText != newAuditObj.AuditLogText &&
                     !typeof(DocNode).IsAssignableFrom(thisProperty.GetPropertyType(oldObj, newObj))))
                {
                    expand = true;
                    resultNode.IsFirstExpansionNode = true;
                }
            }

            // We only look at properties if both objects are non null, unless we're expanding
            // and only the old object is null
            if (ReferenceEquals(oldObj, null) && !expand || ReferenceEquals(newObj, null)) 
                return resultNode;

            foreach (var property in _properties)
            {
                var oldVal = oldObj == null ? null : property.GetValue(oldObj);
                var newVal = property.GetValue(newObj);

                var valType = property.GetPropertyType(oldVal, newVal);

                // If the objects should be treated as unequal, all comparisons are ignored
                if (ReferenceEquals(oldVal, newVal) && !expand)
                    continue;

                if (!property.DiffProperties)
                {
                    var collectionInfo = CollectionInfo.ForType(valType);
                    if (collectionInfo != null)
                    {
                        // Here we just want to diff the collection elements and dont recurse further,
                        // so the callback simply checks for equality
                        var nodes = Reflector.CompareCollections(collectionInfo, property, propertyPath, oldVal, newVal, expand,
                            (type, prop, oldElem, newElem, propPath, key, exp) =>
                            {
                                if (!Equals(oldElem, newElem) || exp)
                                {
                                    return new ElementPropertyDiffNode(prop, propPath.LookupByKey(key.ToString()),
                                        oldElem, newElem, key, null, expand);
                                }
                                return null;
                            });

                        // Only if any elements are different we add a new diff node to the tree,
                        // In this case the current property name has to be added to the property path since we don't recurse futher
                        if (nodes.Any() || expand)
                        {
                            resultNode.Nodes.Add(new PropertyDiffNode(property,
                                property.AddProperty(propertyPath), oldVal, newVal, nodes, expand));
                        }
                    }
                    else
                    {
                        // Properties that are not DiffParent's and not collections are simply compared
                        if (!Equals(oldVal, newVal) || expand)
                        {
                            resultNode.Nodes.Add(new PropertyDiffNode(property,
                                property.AddProperty(propertyPath), oldVal, newVal, null, expand));
                        }
                    }
                }
                else
                {
                    var propertyType = valType;
                    var collectionInfo = Reflector.GetCollectionInfo(ref propertyType, ref oldVal, ref newVal);

                    if (collectionInfo != null)
                    {
                        // Since the collection is DiffParent, we have to go into the elements and compare their properties
                        var nodes = Reflector.CompareCollections(collectionInfo, property, propertyPath, oldVal, newVal, expand, Reflector.BuildDiffTree);

                        if (nodes.Any() || expand)
                            resultNode.Nodes.Add(new PropertyDiffNode(property, property.AddProperty(propertyPath), oldVal, newVal, nodes, expand));
                    }
                    else
                    {
                        var node = Reflector.BuildDiffTree(propertyType, property, oldVal, newVal, propertyPath, null, expand);
                        // Only add the node if the elements are unequal
                        if (node != null)
                            resultNode.Nodes.Add(node);
                    }
                }
            }

            // If there are child nodes, we always want to return the parent node,
            // but if it's just the root node or a diffparent (meaning only the object reference changed)
            // we return null (meaning the objects are equivalent)
            if (resultNode.Nodes.Count == 0 && (!thisProperty.HasPropertyInfo || thisProperty.DiffProperties))
                return null;

            return resultNode;
        }

        // Compares two objects based on their properties which are marked with Diff attributes
        // Structurally similar to BuildDiffTree, but no nodes are created, it simply checks for equality
        public static bool Equals(T obj1, T obj2)
        {
            if (ReferenceEquals(obj1, obj2))
                return true;

            if (ReferenceEquals(obj1, null) || ReferenceEquals(obj2, null))
                return false;

            foreach (var property in _properties)
            {
                var val1 = property.GetValue(obj1);
                var val2 = property.GetValue(obj2);

                if (ReferenceEquals(val1, val2))
                    continue;

                if (ReferenceEquals(val1, null) || ReferenceEquals(val2, null))
                    return false;

                var valType = property.GetPropertyType(val1, val2);

                var collectionInfo = CollectionInfo.ForType(valType);
                if (!property.DiffProperties)
                {
                    if (collectionInfo != null)
                    {
                        if (!Reflector.CollectionEquals(collectionInfo, val1, val2,
                            (type, elem1, elem2) => Equals(elem1, elem2)))
                            return false;
                    }
                    else if (!Equals(val1, val2))
                        return false;
                }
                else
                {
                    if (collectionInfo != null)
                    {
                        if (!Reflector.CollectionEquals(collectionInfo, val1, val2, Reflector.Equals))
                            return false;
                    }
                    else if (!Reflector.Equals(valType, val1, val2))
                        return false;
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
    public class Reflector
    {
        // Gets a pseudo Property for the given type and property name
        public static Property GetRootProperty(Type parentType, string propertyName)
        {
            return new Property(parentType.GetProperty(propertyName),
                new TrackChildrenAttribute());
        }

        public static bool HasToString(object obj)
        {
            if (obj == null)
                return false;

            return obj is IFormattable || obj.GetType().Namespace == "System"; // Not L10N
        }

        public static string ToString(Property property, IAuditLogComparable obj)
        {
            var diffNode = BuildDiffTree(property.GetPropertyType(obj), property, obj.DefaultObject, obj, PropertyPath.Root, null, false);
            return diffNode == null ? null : ToString(new DiffNodePropertyObjectSelector(diffNode), true, true, 0).Trim();
        }

        public static string ToString(object obj)
        {
            return ToString(new PropertyObjectSelector(Property.ROOT_PROPERTY, obj), true, false, -1).Trim();
        }

        private static string ToString(Type type, ObjectSelector propertyObjectSelector, bool wrapProperties, bool addwhitespace, int indentLevel)
        {
            if (propertyObjectSelector.Object == null)
                return LogMessage.MISSING;

            var auditLogObj = propertyObjectSelector.Object as IAuditLogObject;
            if (auditLogObj != null && !auditLogObj.IsName)
                return LogMessage.Quote(auditLogObj.AuditLogText);

            return AuditLogToStringHelper.InvariantToString(propertyObjectSelector.Object) ?? AuditLogToStringHelper.KnownTypeToString(propertyObjectSelector.Object) ??
                    ToString(propertyObjectSelector, wrapProperties, addwhitespace, indentLevel);
        }

        private abstract class ObjectSelector
        {
            protected ObjectSelector(Property property, object objectContainer)
            {
                Property = property;
                ObjectContainer = objectContainer;
            }

            public abstract IEnumerable<ObjectSelector> SubSelectors { get; }
            public abstract object Object { get; }

            public Property Property { get; private set; }
            public object ObjectContainer { get; private set; }
        }

        private class PropertyObjectSelector : ObjectSelector
        {
            public PropertyObjectSelector(Property property, object objectContainer) : base(property, objectContainer)
            {
            }

            public override IEnumerable<ObjectSelector> SubSelectors
            {
                get
                {
                    var type = Property.GetPropertyType(Object);

                    if(!Property.IsCollectionType(Object))
                        return GetProperties(type).Select(p => new PropertyObjectSelector(p, p.GetValue(Object)));

                    object oldObj = null;
                    var newObj = Object;
                    var collectionInfo = GetCollectionInfo(ref type, ref oldObj, ref newObj);
                    return collectionInfo.GetItems(newObj).OfType<object>().Select(obj =>
                        new PropertyObjectSelector(Property.ChangeTypeOverride(collectionInfo.ElementValueType), obj));
                }
            }

            public override object Object { get { return ObjectContainer; } }
        }

        private class DiffNodePropertyObjectSelector : PropertyObjectSelector
        {
            public DiffNodePropertyObjectSelector(DiffNode node) :
                base(node.Property, node)
            {
            }

            public override IEnumerable<ObjectSelector> SubSelectors
            {
                get { return ((DiffNode) ObjectContainer).Nodes.Select(n => new DiffNodePropertyObjectSelector(n)); }
            }

            public override object Object
            {
                get { return ((DiffNode) ObjectContainer).Objects.FirstOrDefault(); }
            }
        }

        private static string Indent(bool indent, string s, int indentLevel)
        {
            if (!indent)
                return s;

            var indentation = new StringBuilder(4 * indentLevel).Insert(0, "    ", indentLevel).ToString(); // Not L10N
            s = indentation + s;
            return s.Replace("\r\n", "\r\n" + indentation); // Not L10N
        }

        private static string ToString(ObjectSelector selector, bool wrapProperties, bool addwhitespace, int indentLevel)
        {
            var obj = selector.Object;
            if (obj == null)
                return LogMessage.MISSING;

            var propType = selector.Property.GetPropertyType(obj);
            object nullObj = null;
            var collectionInfo = GetCollectionInfo(ref propType, ref nullObj, ref obj);

            List<string> strings;
            string format;

            var auditLogObj = AuditLogObject.GetAuditLogObject(obj);

            var subSelectors = selector.SubSelectors.ToArray();
            string result;
            if (auditLogObj.IsName)
            {
                var name = LogMessage.Quote(auditLogObj.AuditLogText);
                if (collectionInfo != null || subSelectors.Length != 0)
                    result = string.Format("{0}: {{0}}", name); // Not L10N
                else
                    return name;
            }
            else
            {
                result = "{0}"; // Not L10N
            }

            if (collectionInfo != null)
            {
                var keys = collectionInfo.GetKeys(obj).Cast<object>().ToArray();

                strings = new List<string>(keys.Length);
                Assume.AreEqual(subSelectors.Length, keys.Length);
                for (var i = 0; i < keys.Length; ++i)
                    strings.Add(Indent(addwhitespace, ToString(collectionInfo.ElementValueType, subSelectors[i], wrapProperties, addwhitespace, indentLevel + 1), 1));

                format = addwhitespace ? ("\r\n[\r\n{0}\r\n]") : "[ {0} ]"; // Not L10N
            }
            else
            {
                if (wrapProperties)
                    ++indentLevel;

                strings = new List<string>();
                foreach (var subSelector in subSelectors)
                {
                    var value = subSelector.Object;
                    var type = subSelector.Property.GetPropertyType(value);

                    var defaultValues = subSelector.Property.DefaultValues;
                    if (defaultValues != null && defaultValues.IsDefault(value, obj))
                        continue;

                    if (subSelector.Property.IgnoreName)
                    {
                        strings.Add(ToString(type, subSelector, false, addwhitespace, indentLevel));
                    }
                    else
                    {
                        strings.Add(Indent(addwhitespace, subSelector.Property.GetName(null, obj) + " = " + // Not L10N
                                    ToString(type, subSelector, true, addwhitespace, indentLevel + 1), 1));
                    }
                }

                format =(wrapProperties ? (addwhitespace ? "\r\n{{\r\n{0}\r\n}}" : "{{ {0} }}") : "{0}"); // Not L10N
            }

            var separator = addwhitespace ? ",\r\n" : ", "; // Not L10N
            return string.Format(result, string.Format(format, string.Join(separator, strings))); // Not L10N
        }

        // Determines whether two IAuditLogObjects (most likely from two different
        // collections) match, meaning either their name or global index are the same
        public static bool Matches(Type type, IAuditLogObject obj1, IAuditLogObject obj2, bool diffProperties)
        {
            if (ReferenceEquals(obj1, obj2))
                return true;

            if (obj1 == null || obj2 == null)
                return false;

            var auditObj1 = AuditLogObject.GetObject(obj1);
            var auditObj2 = AuditLogObject.GetObject(obj2);

            if (ReferenceEquals(auditObj1, auditObj2))
                return true;
            if (ReferenceEquals(auditObj1, null) || ReferenceEquals(auditObj2, null))
                return false;

            type = Property.GetPropertyType(type, auditObj1, auditObj2);
            if (typeof(DocNode).IsAssignableFrom(type))
            {

                // ReSharper disable once PossibleNullReferenceException
                return (auditObj1 as DocNode).Id.GlobalIndex
                    // ReSharper disable once PossibleNullReferenceException
                       == (auditObj2 as DocNode).Id.GlobalIndex;
            }

            if (obj1.IsName)
            {
                return obj1.AuditLogText == obj2.AuditLogText;
            }
            else
            {
                return diffProperties
                    ? Equals(type, auditObj1, auditObj2)
                    : Equals(auditObj1, auditObj2);
            }
        }

        public static List<DiffNode> CompareCollections(ICollectionInfo collectionInfo, Property property, PropertyPath propertyPath, object oldVal, object newVal, bool expand, Func<Type, Property, object, object, PropertyPath, object, bool, DiffNode> func)
        {
            var result = new List<DiffNode>();
            
            var oldKeys = collectionInfo.GetKeys(oldVal).OfType<object>().ToArray();
            var newKeys = collectionInfo.GetKeys(newVal).OfType<object>().ToList();  
            var removeIndices = new List<int>();

            propertyPath = property.AddProperty(propertyPath);
            property = property.ChangeTypeOverride(collectionInfo.ElementValueType);

            // If we assume elements are unequal, we assume the old collection was empty
            // and all elements were added
            if (expand)
            {
                return newKeys.Select(k =>
                {
                    var value = collectionInfo.GetItemValueFromKey(newVal, k);
                    // We also want to know what properties were set in the new elements,
                    // So we call the diff function here too
                    var node = func(property.GetPropertyType(value), property, null,
                        value, propertyPath, k, true);

                    return (DiffNode)new ElementDiffNode(property,
                        propertyPath.LookupByKey(k.ToString()),
                        value, k, false,
                        node == null ? null : node.Nodes, true);
                }).ToList();
            }

            var newElemObjs = newKeys
                .Select(k => AuditLogObject.GetAuditLogObject(collectionInfo.GetItemValueFromKey(newVal, k))).ToList();

            foreach (var key in oldKeys)
            {
                var oldKey = key;
                var oldElem = collectionInfo.GetItemValueFromKey(oldVal, oldKey);

                var oldElemObj = AuditLogObject.GetAuditLogObject(oldElem);

                // Try to find a matching object
                var index = newElemObjs.FindIndex(o => Matches(property.GetPropertyType(oldElemObj, o), o, oldElemObj, property.DiffProperties));

                if (index >= 0)
                {
                    var newElem = collectionInfo.GetItemValueFromKey(newVal, newKeys[index]);
                    var node = func(property.GetPropertyType(oldElem, newElem), property, oldElem, newElem, propertyPath, newKeys[index], false);

                    // Make sure elements are unequal
                    if (node != null)
                        result.Add(node);

                    // This item has been dealt with, so we mark it for removal
                    removeIndices.Add(index);
                }
                else
                {
                    // If the element can't be found, we assume it was removed
                    result.Add(new ElementDiffNode(property, propertyPath.LookupByKey(key.ToString()), oldElem, key, true));
                }
            }

            // We order by descending so that the later indices are still valid
            // after removing one
            foreach (var i in removeIndices.OrderByDescending(i => i))
                newKeys.RemoveAt(i);

            // The keys that are left over are most likely elements that were added to the colletion
            var added = newKeys.Select(k =>
                new ElementDiffNode(property, propertyPath.LookupByKey(k.ToString()),
                    collectionInfo.GetItemValueFromKey(newVal, k), k, false)).ToList();

            foreach (var node in added.Where(n => AuditLogObject.GetAuditLogObject(n.Element).IsName))
            {
                node.Nodes.AddRange(func(node.Property.GetPropertyType(node.Element), node.Property, null, node.Element, propertyPath, node.ElementKey, true).Nodes);
                node.IsFirstExpansionNode = true;
            }
                
            result.AddRange(added);

            return result;
        }

        // Checks whether two collections are exactly equal, meaning all elements are the same and at the same
        // indices/keys.
        public static bool CollectionEquals(ICollectionInfo collectionInfo, object coll1, object coll2, Func<Type, object, object, bool> func)
        {
            var oldKeys = collectionInfo.GetKeys(coll1).OfType<object>().ToArray();
            var newKeys = collectionInfo.GetKeys(coll2).OfType<object>().ToArray();

            if (oldKeys.Length != newKeys.Length)
                return false;

            for (var i = 0; i < oldKeys.Length; ++i)
            {
                if (oldKeys[i] != newKeys[i])
                    return false;

                var val1 = collectionInfo.GetItemValueFromKey(coll1, oldKeys[i]);
                var val2 = collectionInfo.GetItemValueFromKey(coll2, newKeys[i]);

                if (!func(collectionInfo.ElementValueType, val1, val2))
                    return false;
            }

            return true;
        }

        public static ICollectionInfo GetCollectionInfo(ref Type type, ref object oldVal, ref object newVal)
        {
            var collectionInfo = CollectionInfo.ForType(type);
            if (collectionInfo != null)
                return collectionInfo;

            // See if we can convert to an array
            if (type.DeclaringType == typeof(Enumerable) || (type.IsGenericType && typeof(IEnumerable<>).IsAssignableFrom(type.GetGenericTypeDefinition())))
            {
                var genericType = type.GenericTypeArguments.Last();
                if (oldVal != null)
                    oldVal = ToArray(genericType, oldVal);
                if (newVal != null)
                    newVal = ToArray(genericType, newVal);

                return CollectionInfo.ForType(genericType.MakeArrayType());
            }

            return null;
        }

        // ReSharper disable PossibleNullReferenceException
        public static object ToArray(Type type, object obj)
        {
            var enumerableType = typeof(Enumerable);
            var toArray = enumerableType.GetMethod("ToArray", BindingFlags.Public | BindingFlags.Static);
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

        public static DiffTree BuildDiffTree(Type objectType, Property thisProperty, object oldObj, object newObj, DateTime? timeStamp)
        {
            var type = typeof(Reflector<>).MakeGenericType(objectType);
            var buildDiffTree = type.GetMethod("BuildDiffTree", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Property), objectType, objectType, typeof(DateTime?) }, null); // Not L10N

            Assume.IsNotNull(buildDiffTree); 

            var reflector = Activator.CreateInstance(type);
            
            return (DiffTree) buildDiffTree.Invoke(reflector, new[] { thisProperty, oldObj, newObj, timeStamp });
        }

        public static DiffNode BuildDiffTree(Type objectType, Property thisProperty, object oldObj, object newObj, PropertyPath propertyPath, object elementKey, bool expand)
        {
            var type = typeof(Reflector<>).MakeGenericType(objectType);
            var buildDiffTree = type.GetMethod("BuildDiffTree", BindingFlags.NonPublic | BindingFlags.Static, null,
                new[] {typeof(Property), objectType, objectType, typeof(PropertyPath), typeof(object), typeof(bool)},
                null); // Not L10N

            Assume.IsNotNull(buildDiffTree);

            var reflector = Activator.CreateInstance(type);
            return (DiffNode) buildDiffTree.Invoke(reflector, new[] {thisProperty, oldObj, newObj, propertyPath, elementKey, expand});
        }

        public static bool Equals(Type objectType, object obj1, object obj2)
        {
            var type = typeof(Reflector<>).MakeGenericType(objectType);
            var equals = type.GetMethod("Equals", BindingFlags.Public | BindingFlags.Static, null, new[] { objectType, objectType }, null); // Not L10N

            Assume.IsNotNull(equals);

            var reflector = Activator.CreateInstance(type);
            return (bool)equals.Invoke(reflector, new[] { obj1, obj2 });
        }
        #endregion
        // ReSharper restore PossibleNullReferenceException
    }
}