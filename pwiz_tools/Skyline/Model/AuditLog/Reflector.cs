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

        public static DiffTree BuildDiffTree(SrmDocumentPair documentPair, Property thisProperty, T oldObj, T newObj, DateTime? timeStamp)
        {
            return new DiffTree(BuildDiffTree(documentPair, thisProperty, oldObj, newObj), timeStamp ?? DateTime.Now);
        }

        public static DiffNode BuildDiffTree(SrmDocumentPair documentPair, Property thisProperty, T oldObj, T newObj)
        {
            return BuildDiffTree(documentPair, thisProperty, oldObj, newObj, PropertyPath.Root.Property(thisProperty.PropertyName), null);
        }

        public static DiffTree BuildDiffTree(SrmDocumentPair documentPair, Property thisProperty, IAuditLogComparable comparable, DateTime? timeStamp = null)
        {
            return new DiffTree(BuildDiffTree(documentPair, thisProperty, null, (T)comparable, PropertyPath.Root.Property(thisProperty.PropertyName), null, true),
                timeStamp ?? DateTime.Now);
        }

        private static DiffNode BuildDiffTree(SrmDocumentPair documentPair, Property thisProperty, T oldObj, T newObj, PropertyPath propertyPath, object elementKey, bool expand = false, IList<object> defaults = null)
        {
            if (ReferenceEquals(oldObj, newObj) && !expand)
                return null;

            // This allows us to deal with collections being passed into BuildDiffTree and collections of collections
            var parentType = thisProperty.GetPropertyType(oldObj, newObj);
            var oldObjectRef = (object) oldObj;
            var newObjectRef = (object) newObj;
            var parentCollectionInfo = Reflector.GetCollectionInfo(ref parentType, ref oldObjectRef, ref newObjectRef);

            if (parentCollectionInfo != null)
            {
                var node = Reflector.CompareCollections(parentCollectionInfo, documentPair,
                    thisProperty, propertyPath, oldObjectRef,
                    newObjectRef, expand, defaults, null);

                if (node != null && (node.Nodes.Any() || expand))
                    return node;
                return null;
            }

            DiffNode resultNode = thisProperty.IsCollectionElement
                ? new ElementPropertyDiffNode(thisProperty, propertyPath, oldObj, newObj, elementKey, null, expand)
                : new PropertyDiffNode(thisProperty, propertyPath, oldObj, newObj, null, expand);

            if (!expand && !thisProperty.IsRoot)
            {
                var oldAuditObj = AuditLogObject.GetAuditLogObject(oldObj);
                var newAuditObj = AuditLogObject.GetAuditLogObject(newObj);

                if (ReferenceEquals(oldObj, null) && !ReferenceEquals(newObj, null) && newAuditObj.IsName ||
                    oldAuditObj.IsName && newAuditObj.IsName && oldAuditObj.AuditLogText != newAuditObj.AuditLogText &&
                     !typeof(DocNode).IsAssignableFrom(thisProperty.GetPropertyType(oldObj, newObj)))
                {
                    expand = true;
                    resultNode.IsFirstExpansionNode = true;
                }
            }

            defaults = defaults ?? new object[0];

            object defaultObj = null;
            if (expand)
            {
                var comparable = newObj as IAuditLogComparable;
                if (comparable != null)
                {
                    defaultObj = comparable.DefaultObject;
                    if (ReferenceEquals(defaultObj, newObj))
                        return null;
                }

                if (defaults.Any(d => ReferenceEquals(d, newObj)))
                    return null;
            }

            // We only look at properties if both objects are non null, unless we're expanding
            // and only the old object is null
            if (ReferenceEquals(oldObj, null) && !expand || ReferenceEquals(newObj, null)) 
                return resultNode;

            foreach (var property in _properties)
            {
                var newPropertyPath = property.AddProperty(propertyPath);
                var oldVal = ReferenceEquals(oldObj, null) ? null : property.GetValue(oldObj);
                var newVal = property.GetValue(newObj);

                var propertyDefaults = new List<object>();
                Func<object, object, bool> defaultFunc = null;
                if (expand)
                {
                    var localProperty = property;
                    // For non-null default values, find the default value for the sub property
                    propertyDefaults = defaults.Where(d => d != null).Select(d => localProperty.GetValue(d)).ToList();

                    // The default values has higher priority than the comparable object
                    var defaultValues = property.DefaultValues;
                    if (defaultValues != null)
                    {
                        propertyDefaults.AddRange(defaultValues.Values);
                        defaultFunc = defaultValues.IsDefault;
                    }
                    
                    if (defaultObj != null && !property.IgnoreDefaultParent)
                    {
                        var defaultValue = property.GetValue(defaultObj);
                        propertyDefaults.Add(defaultValue);
                    }
                }

                var valType = property.GetPropertyType(oldVal, newVal);

                // If the objects should be treated as unequal, all comparisons are ignored
                if (ReferenceEquals(oldVal, newVal) && !expand)
                    continue;

                var propertyType = valType;
                var collectionInfo = Reflector.GetCollectionInfo(ref propertyType, ref oldVal, ref newVal);

                if (collectionInfo != null)
                {
                    var node = Reflector.CompareCollections(collectionInfo, documentPair,
                        property, newPropertyPath, oldVal, newVal, expand,
                        propertyDefaults, obj =>
                        {
                            if (defaultFunc != null)
                                defaultFunc(obj, newVal);
                            return false;
                        });

                    // Only if any elements are different we add a new diff node to the tree,
                    if (node != null)
                        resultNode.Nodes.Add(node);
                }
                else
                {
                    if (!property.DiffProperties)
                    {
                        // Properties that are not DiffParent's and not collections are simply compared
                        // Also make sure that new val doesn't equal any of the default objects and that
                        // the default function (if exists) return false
                        if ((!Equals(oldVal, newVal) || expand) &&
                            (!expand || (!propertyDefaults.Any(d => Equals(d, newVal)) &&
                                         (defaultFunc == null || !defaultFunc(newVal, newObj)))))
                        {
                            resultNode.Nodes.Add(new PropertyDiffNode(property,
                                newPropertyPath, oldVal, newVal, null, expand));
                        }
                    }
                    else
                    {
                        var node = Reflector.BuildDiffTree(propertyType, documentPair, property, oldVal, newVal, newPropertyPath, null,
                            expand, propertyDefaults);
                        // Only add the node if the elements are unequal
                        if (node != null)
                            resultNode.Nodes.Add(node);
                    }
                }
            }

            // If there are child nodes, we always want to return the parent node,
            // but if it's just the root node or a diffparent (meaning only the object reference changed)
            // we return null (meaning the objects are equivalent)
            if (resultNode.Nodes.Count == 0 && thisProperty.DiffProperties && !expand)
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

                var auditLogObj1 = AuditLogObject.GetAuditLogObject(val1);
                var auditLogObj2 = AuditLogObject.GetAuditLogObject(val2);

                if (auditLogObj1.IsName && auditLogObj2.IsName)
                {
                    if (auditLogObj1.AuditLogText != auditLogObj2.AuditLogText)
                        return false;
                }

                var valType = property.GetPropertyType(val1, val2);

                var collectionInfo = Reflector.GetCollectionInfo(ref valType, ref val1, ref val2);
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
        public static bool HasToString(object obj)
        {
            if (obj == null)
                return false;

            return obj is IFormattable || obj.GetType().Namespace == "System"; // Not L10N
        }

        /// <summary>
        /// Used by the ToString function to select properties to include in the string representation.
        /// Each selector contains an <see cref="ObjectContainer"/> from which the actual <see cref="Object"/> can be retrieved,
        /// the <see cref="AuditLog.Property"/> corresponding to the <see cref="Object"/> and a list of <see cref="SubSelectors"/>
        /// that are used to iterate over the objects properties or sub elements.
        /// </summary>
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
            public PropertyObjectSelector(Property property, object objectContainer)
                : base(property, objectContainer)
            {
            }

            public override IEnumerable<ObjectSelector> SubSelectors
            {
                get
                {
                    var type = Property.GetPropertyType(Object);

                    if (!Property.IsCollectionType(Object))
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

        private sealed class DiffNodePropertyObjectSelector : PropertyObjectSelector
        {
            public DiffNodePropertyObjectSelector(DiffNode node) :
                base(node.Property, node)
            {
            }

            public override IEnumerable<ObjectSelector> SubSelectors
            {
                get { return ((DiffNode)ObjectContainer).Nodes.Select(n => new DiffNodePropertyObjectSelector(n)); }
            }

            public override object Object
            {
                get { return ((DiffNode)ObjectContainer).Objects.FirstOrDefault(); }
            }
        }

        public static string ToString(SrmDocumentPair documentPair, Property property, IAuditLogComparable obj)
        {
            var diffTree = BuildDiffTree(property.GetPropertyType(obj), documentPair, property, obj);
            return diffTree.Root == null ? null : ToString(new DiffNodePropertyObjectSelector(diffTree.Root), true, true, 0);
        }

        // TODO: add SrmDocumentPair for these
        public static string ToString(object obj)
        {
            return ToString(new PropertyObjectSelector(RootProperty.Create(obj.GetType()), obj), true, false, -1).Trim();
        }

        private static string ToString(Type type, ObjectSelector propertyObjectSelector, bool wrapProperties, bool formatWhitespace, int indentLevel)
        {
            if (propertyObjectSelector.Object == null)
                return LogMessage.MISSING;

            // If the name is not getting ignored there will be an equal sign in front of this text,
            // so dont indent, unless the object is a collection element, in which case it has no equal sign
            var indent =
                (propertyObjectSelector.Property.IgnoreName || propertyObjectSelector.Property.IsCollectionElement) &&
                formatWhitespace;

            var auditLogObj = propertyObjectSelector.Object as IAuditLogObject;
            if (auditLogObj != null && !auditLogObj.IsName)
                return Indent(indent, LogMessage.Quote(auditLogObj.AuditLogText), indentLevel);

            return Indent(indent,
                       AuditLogToStringHelper.InvariantToString(propertyObjectSelector.Object) ??
                       AuditLogToStringHelper.KnownTypeToString(propertyObjectSelector.Object), indentLevel) ??
                   ToString(propertyObjectSelector, wrapProperties, formatWhitespace, indentLevel + 1);
        }

        private static string GetIndentation(int indentLevel)
        {
            return new StringBuilder(4 * indentLevel).Insert(0, "    ", indentLevel).ToString(); // Not L10N
        }

        private static string Indent(bool indent, string s, int indentLevel)
        {
            if (!indent || s == null || indentLevel < 0)
                return s;

            s = GetIndentation(indentLevel) + s;
            return s;
        }

        private static string ToString(ObjectSelector selector, bool wrapProperties, bool formatWhiteSpace, int indentLevel)
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

            if (!selector.Property.IgnoreName && auditLogObj.IsName)
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
                {
                    strings.Add(ToString(collectionInfo.ElementValueType, subSelectors[i], wrapProperties,
                        formatWhiteSpace, indentLevel));
                }

                if (formatWhiteSpace)
                {
                    if (indentLevel == 0)
                        format = "{0}"; // Not L10N
                    else
                        format = string.Format("\r\n{0}[\r\n{{0}}\r\n{0}]", GetIndentation(indentLevel - 1)); // Not L10N
                }
                else
                {
                    format = "[ {0} ]"; // Not L10N
                }
            }
            else
            {
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
                        strings.Add(ToString(type, subSelector, false, formatWhiteSpace, indentLevel - 1));
                    }
                    else
                    {
                        // TODO: non relative custom localizer won't (and currently can't) work in ToString
                        strings.Add(Indent(formatWhiteSpace, subSelector.Property.GetName(null, new ObjectGroup(value, obj, null)) + " = " + // Not L10N
                            ToString(type, subSelector, true, formatWhiteSpace, indentLevel), indentLevel));
                    }
                }

                // If we don't want to wrap properties or this is the "root" text, we don't
                // show curly braces
                if (wrapProperties && (!formatWhiteSpace || indentLevel != 0))
                {
                    if (formatWhiteSpace)
                    {
                        format = string.Format("\r\n{0}{{{{\r\n{{0}}\r\n{0}}}}}", // Not L10N 
                            GetIndentation(indentLevel - 1));
                    }
                    else
                    {
                        format = "{{ {0} }}"; // Not L10N
                    }
                }
                else
                {
                    format = "{0}"; // Not L10N
                }
            }

            var separator = formatWhiteSpace ? ",\r\n" : ", "; // Not L10N
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

        public static DiffNode CompareCollections(ICollectionInfo collectionInfo, SrmDocumentPair documentPair, Property property,
            PropertyPath propertyPath, object oldVal, object newVal, bool expand, IList<object> defaults,
            Func<object, bool> defaultFunc,
            Func<Type, SrmDocumentPair, Property, object, object, PropertyPath, object, bool, IList<object>, DiffNode> func)
        {
            var oldKeys = collectionInfo.GetKeys(oldVal).OfType<object>().ToArray();
            var newKeys = collectionInfo.GetKeys(newVal).OfType<object>().ToList();

            var resultNode = new PropertyDiffNode(property, propertyPath, oldVal, newVal, null,
                expand);
            
            if (expand && (defaults.Any(
                    d => ReferenceEquals(d, newVal)) || defaultFunc(newVal)))
                return null;

            if (ReferenceEquals(oldVal, null) && !expand || ReferenceEquals(newVal, null))
                return resultNode;

            // All nodes added from here on are element nodes and their property type
            // will be overriden
            property = property.ChangeTypeOverride(collectionInfo.ElementValueType);

            // If we assume elements are unequal, we assume the old collection was empty
            // and all elements were added
            if (expand)
            {
                // For collections, we want every key and element to match, it would be odd to just exclude
                // elements that are in the default collection but include the ones that aren't
                var isDefault = defaults.Any(defaultCollection =>
                {
                    var keys = collectionInfo.GetKeys(defaultCollection).OfType<object>().ToArray();
                    if (!ArrayUtil.EqualsDeep(newKeys, keys))
                        return false;

                    return keys.All(key =>
                    {
                        var defaultValue = collectionInfo.GetItemValueFromKey(defaultCollection, key);
                        var newValue = collectionInfo.GetItemValueFromKey(newVal, key);

                        if (property.DiffProperties)
                            return Equals(property.GetPropertyType(newValue), defaultValue, newValue);
                        return Equals(defaultValue, newValue);
                    });
                });

                if (isDefault)
                    return null;

                resultNode.Nodes.AddRange(newKeys.Select(k =>
                {
                    var value = collectionInfo.GetItemValueFromKey(newVal, k);
                    // We also want to know what properties were set in the new elements,
                    // So we call the diff function here too
                    var path = propertyPath.LookupByKey(k.ToString());
                    var node = func(property.GetPropertyType(value), documentPair, property, null,
                        value, path, k, true, defaults);

                    return (DiffNode) new ElementDiffNode(property,
                        path,
                        value, k, false,
                        node == null ? new List<DiffNode>() : node.Nodes, true);
                }).ToList());

                return resultNode;
            }

            var newElemObjs = newKeys
                .Select(k => AuditLogObject.GetAuditLogObject(collectionInfo.GetItemValueFromKey(newVal, k))).ToList();

            foreach (var key in oldKeys)
            {
                var oldKey = key;
                var oldElem = collectionInfo.GetItemValueFromKey(oldVal, oldKey);

                var oldElemObj = AuditLogObject.GetAuditLogObject(oldElem);

                // Try to find a matching object
                var index = newElemObjs.FindIndex(o =>
                    Matches(property.GetPropertyType(oldElemObj, o), o, oldElemObj, property.DiffProperties));

                if (index >= 0)
                {
                    var newElem = collectionInfo.GetItemValueFromKey(newVal, newKeys[index]);
                    var node = func(property.GetPropertyType(oldElem, newElem), documentPair, property, oldElem, newElem,
                        propertyPath.LookupByKey(newKeys[index].ToString()), newKeys[index], false, defaults);

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
                        oldElem, key, true));
                }
            }

            // The keys that are left over are most likely elements that were added to the colletion
            var added = newKeys.Select(k =>
                new ElementDiffNode(property, propertyPath.LookupByKey(k.ToString()),
                    collectionInfo.GetItemValueFromKey(newVal, k), k, false)).ToList();

            foreach (var node in added.Where(n => AuditLogObject.GetAuditLogObject(n.Element).IsName))
            {
                var newNode = func(node.Property.GetPropertyType(node.Element), documentPair, node.Property, null, node.Element,
                    node.PropertyPath, node.ElementKey, true, defaults);
                if (newNode != null)
                    node.Nodes.AddRange(newNode.Nodes);
                node.IsFirstExpansionNode = true;
            }

            resultNode.Nodes.AddRange(added);
            if (resultNode.Nodes.Count == 0)
                return null;
            return resultNode;
        }

        public static DiffNode CompareCollections(ICollectionInfo collectionInfo, SrmDocumentPair documentPair, Property property,
            PropertyPath propertyPath, object oldVal, object newVal, bool expand, IList<object> defaults,
            Func<object, bool> defaultFunc)
        {
            if (!property.DiffProperties)
            {
                // Here we just want to diff the collection elements and dont recurse further,
                // so the callback simply checks for equality
                return CompareCollections(collectionInfo, documentPair, property, propertyPath, oldVal, newVal,
                    expand, defaults, defaultFunc,
                    (type, docPair, prop, oldElem, newElem, propPath, key, exp, def) =>
                    {
                        if (!Equals(oldElem, newElem) || exp)
                        {
                            return new ElementPropertyDiffNode(prop, propPath,
                                oldElem, newElem, key, null, expand);
                        }

                        return null;
                    });
            }
            else
            {
                // Since the collection is DiffParent, we have to go into the elements and compare their properties
                return CompareCollections(collectionInfo, documentPair, property, propertyPath, oldVal, newVal,
                    expand, defaults, defaultFunc, BuildDiffTree);
            }
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
            if (type.DeclaringType == typeof(Enumerable) ||
                (type.IsGenericType && typeof(IEnumerable<>).IsAssignableFrom(type.GetGenericTypeDefinition())))
            {
                var genericType = type.GenericTypeArguments.Last();
                if (oldVal != null)
                    oldVal = ToArray(genericType, oldVal);
                if (newVal != null)
                    newVal = ToArray(genericType, newVal);

                type = genericType;
                return CollectionInfo.ForType(genericType.MakeArrayType());
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

        public static DiffTree BuildDiffTree(Type objectType, SrmDocumentPair documentPair, Property thisProperty, object oldObj, object newObj, DateTime? timeStamp)
        {
            var type = typeof(Reflector<>).MakeGenericType(objectType);
            var buildDiffTree = type.GetMethod("BuildDiffTree", BindingFlags.Public | BindingFlags.Static, null, // Not L10N
                new[] { typeof(SrmDocumentPair), typeof(Property), objectType, objectType, typeof(DateTime?) }, null);

            Assume.IsNotNull(buildDiffTree); 

            var reflector = Activator.CreateInstance(type);

            return (DiffTree) buildDiffTree.Invoke(reflector, new[] { documentPair, thisProperty, oldObj, newObj, timeStamp });
        }

        public static DiffNode BuildDiffTree(Type objectType, SrmDocumentPair documentPair, Property thisProperty, object oldObj, object newObj, PropertyPath propertyPath, object elementKey, bool expand, IList<object> defaults)
        {
            var type = typeof(Reflector<>).MakeGenericType(objectType);
            var buildDiffTree = type.GetMethod("BuildDiffTree", BindingFlags.NonPublic | BindingFlags.Static,
                null, // Not L10N
                new[]
                {
                    typeof(SrmDocumentPair), typeof(Property), objectType, objectType, typeof(PropertyPath),
                    typeof(object), typeof(bool), typeof(IList<object>)
                },
                null);

            Assume.IsNotNull(buildDiffTree);

            var reflector = Activator.CreateInstance(type);

            return (DiffNode) buildDiffTree.Invoke(reflector, new[] { documentPair, thisProperty, oldObj, newObj, propertyPath, elementKey, expand, defaults });
        }

        public static DiffTree BuildDiffTree(Type objectType, SrmDocumentPair documentPair, Property thisProperty, IAuditLogComparable comparable, DateTime? timeStamp = null)
        {
            var type = typeof(Reflector<>).MakeGenericType(objectType);
            var buildDiffTree = type.GetMethod("BuildDiffTree", BindingFlags.Public | BindingFlags.Static, null, // Not L10N
                new[] { typeof(SrmDocumentPair), typeof(Property), typeof(IAuditLogComparable), typeof(DateTime?) }, null);

            Assume.IsNotNull(buildDiffTree);

            var reflector = Activator.CreateInstance(type);

            return (DiffTree) buildDiffTree.Invoke(reflector, new object[] { documentPair, thisProperty, comparable, timeStamp });
        }

        public static bool Equals(Type objectType, object obj1, object obj2)
        {
            var type = typeof(Reflector<>).MakeGenericType(objectType);
            var equals = type.GetMethod("Equals", BindingFlags.Public | BindingFlags.Static, null, // Not L10N
                new[] { objectType, objectType }, null);

            Assume.IsNotNull(equals);

            var reflector = Activator.CreateInstance(type);
            return (bool) equals.Invoke(reflector, new[] { obj1, obj2 });
        }
        #endregion
        // ReSharper restore PossibleNullReferenceException
    }
}