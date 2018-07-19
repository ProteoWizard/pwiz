using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.AuditLog
{
    public partial class Reflector
    {
        /// <summary>
        /// Checks whether the given object can safely be converted to a string by simply calling ToString
        /// </summary>
        public static bool HasToString(object obj)
        {
            if (obj == null)
                return false;

            return obj is IFormattable ||
                   (obj.GetType().Namespace == "System" && !IsCollectionType(obj.GetType())); // Not L10N
        }

        /*
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

                    var collection = GetCollectionInfo(type, ObjectPair.Create(null, Object));
                    return collection.Info.GetItems(collection.Collections.NewObject).OfType<object>().Select(obj =>
                        new PropertyObjectSelector(
                            Property.ChangeTypeOverride(collection.Info.ElementValueType), obj));
                }
            }

            public override object Object
            {
                get { return ObjectContainer; }
            }
        }

        private sealed class DiffNodePropertyObjectSelector : PropertyObjectSelector
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

        public static string ToString(ObjectPair<object> rootPair, Property property, IAuditLogComparable obj)
        {
            var diffTree = BuildDiffTree(property.GetPropertyType(obj), rootPair, property, obj);
            return diffTree.Root == null
                ? null
                : ToString(new DiffNodePropertyObjectSelector(diffTree.Root), true, true, 0);
        }*/


        /// <summary>
        /// Converts the given object to a string, showing each of its properties values.
        /// Note that default values of properties might not work with this function
        /// since no parent or root objects are provided
        /// </summary>
        /// <param name="obj">Object to convert</param>
        /// <param name="formatWhitespace">Whether to use tabs and new lines or not</param>
        /// <returns>String representation</returns>
        public static string ToString(object obj, bool formatWhitespace = false)
        {
            var objectInfo = new ObjectInfo<object>().ChangeNewObject(obj);
            var rootProp = RootProperty.Create(obj.GetType());
            return ToString(objectInfo, rootProp, formatWhitespace);
        }

        /// <summary>
        /// Converts the given object to a string, showing each of its properties values 
        /// </summary>
        /// <param name="objectInfo">Object to convert and related objects</param>
        /// <param name="rootProperty">Property of the initial <see cref="ObjectInfo&lt;T&gt;.ObjectPair"/></param>
        /// <param name="formatWhitespace"></param>
        /// <returns>String representation of <see cref="ObjectInfo&lt;T&gt;.NewObject"/></returns>
        public static string ToString(ObjectInfo<object> objectInfo, Property rootProperty, bool formatWhitespace = false)
        {
            var result = ToString(objectInfo, rootProperty, true, formatWhitespace, 0);
            return result == null ? null : result.Trim();
        }

        private static string GetIndentation(int indentLevel)
        {
            return new StringBuilder(4 * indentLevel).Insert(0, "    ", indentLevel).ToString(); // Not L10N
        }

        private static string Indent(bool indent, string s, int indentLevel)
        {
            if (!indent || s == null || indentLevel <= 0)
                return s;

            s = GetIndentation(indentLevel) + s;
            return s;
        }

        private static string ToStringInternal(ObjectInfo<object> objectInfo, Property property, bool wrapProperties, bool formatWhitespace,
            int indentLevel, List<object> defaults)
        {
            if (objectInfo.NewObject == null)
                return LogMessage.MISSING;

            // If the name is not getting ignored there will be an equal sign in front of this text,
            // so dont indent, unless the object is a collection element, in which case it has no equal sign
            var indent =
                (property.IgnoreName || property.IsCollectionElement) &&
                formatWhitespace;

            var auditLogObj = objectInfo.NewObject as IAuditLogObject;
            if (auditLogObj != null && !(auditLogObj.IsName && property.DiffProperties))
                return Indent(indent, LogMessage.Quote(auditLogObj.AuditLogText), indentLevel);

            if (!property.DiffProperties &&
                !IsCollectionType(property.GetPropertyType(objectInfo.NewObject)))
            {
                return Indent(indent,
                    AuditLogToStringHelper.InvariantToString(objectInfo.NewObject) ??
                    AuditLogToStringHelper.KnownTypeToString(objectInfo.NewObject), indentLevel);
            }

            return ToString(objectInfo, property, wrapProperties, formatWhitespace, indentLevel + 1, defaults);
        }

        // TODO: make this more similar to BuildDiffTree
        private static string ToString(ObjectInfo<object> objectInfo, Property property, bool wrapProperties, bool formatWhiteSpace, int indentLevel, List<object> defaults = null)
        {
            if (objectInfo.NewObject == null)
                return LogMessage.MISSING;

            defaults = defaults ?? new List<object>();
            var comparable = objectInfo.NewObject as IAuditLogComparable;
            if (comparable != null)
            {
                var defaultObj = comparable.GetDefaultObject(objectInfo);
                defaults.Add(defaultObj);
            }

            var propType = property.GetPropertyType(objectInfo.NewObject);
            var collection = GetCollectionInfo(propType, objectInfo.ObjectPair);

            IList<Property> properties = new Property[0];
            var collectionKeys = new object[0];

            if (collection != null)
            {
                objectInfo = objectInfo.ChangeObjectPair(collection.Collections);
                collectionKeys = collection.Info.GetKeys(objectInfo.NewObject).Cast<object>().ToArray();
            }
            else
            {
                properties = GetProperties(propType);
            }   

            var auditLogObj = AuditLogObject.GetAuditLogObject(objectInfo.NewObject);

            string result;

            if (!property.IgnoreName && auditLogObj.IsName)
            {
                var name = LogMessage.Quote(auditLogObj.AuditLogText);
                if (collection != null)
                    System.Diagnostics.Debugger.Break();

                if (properties.Count != 0 || collectionKeys.Length != 0)
                    result = Indent(formatWhiteSpace, string.Format("{0}: {{0}}", name), indentLevel - 1); // Not L10N
                else
                    return name;
            }
            else
            {
                result = "{0}"; // Not L10N
            }

            var strings = new List<string>();
            string format;

            objectInfo = objectInfo.ChangeParentPair(objectInfo.ObjectPair);

            if (collection != null)
            {
                if (collectionKeys.Length == 0)
                    return LogMessage.EMPTY;

                strings.Capacity = collectionKeys.Length;

                foreach (var key in collectionKeys)
                {
                    var newInfo = objectInfo.ChangeObjectPair(ObjectPair.Create(null,
                        collection.Info.GetItemValueFromKey(objectInfo.NewObject, key)));
                    strings.Add(ToStringInternal(newInfo, property.ChangeTypeOverride(collection.Info.ElementValueType), wrapProperties,
                        formatWhiteSpace, indentLevel, null));
                }


                if (formatWhiteSpace)
                {
                    format = indentLevel == 0
                        ? "{0}" // Not L10N
                        : string.Format("\r\n{0}[\r\n{{0}}\r\n{0}]", GetIndentation(indentLevel - 1)); // Not L10N
                }
                else
                {
                    format = "[ {0} ]"; // Not L10N
                }
            }
            else
            {
                foreach (var subProperty in properties)
                {
                    var value = subProperty.GetValue(objectInfo.NewParentObject);
                    var newInfo = objectInfo.ChangeNewObject(value);

                    // For non-null default values, find the default value for the sub property
                    var propertyDefaults = defaults.Where(d => d != null).Select(d => subProperty.GetValue(d)).ToList();

                    var defaultValues = subProperty.DefaultValues;
                    if (defaultValues != null)
                    {
                        if (defaultValues.IsDefault(value, newInfo.NewParentObject))
                            continue;

                        propertyDefaults.AddRange(defaultValues.Values);
                    }

                    if (!subProperty.IgnoreDefaultParent)
                    {
                        if (subProperty.DiffProperties)
                        {
                            if (propertyDefaults.Any(d => ReferenceEquals(d, newInfo.NewObject)))
                                continue;
                        }
                        else
                        {
                            if (propertyDefaults.Any(d => Equals(d, newInfo.NewObject)))
                                continue;
                        }
                    }

                    if (subProperty.IgnoreName)
                    {
                        var str = ToStringInternal(newInfo, subProperty, false, formatWhiteSpace, indentLevel - 1, propertyDefaults);
                        if (!string.IsNullOrEmpty(str))
                            strings.Add(str);
                    }
                    else
                    {
                        var str = ToStringInternal(newInfo, subProperty, true, formatWhiteSpace, indentLevel, propertyDefaults);
                        if (!string.IsNullOrEmpty(str))
                            strings.Add(Indent(formatWhiteSpace, subProperty.GetName(newInfo) + " = " + str, indentLevel)); // Not L10N
                    }
                }

                // If we don't want to wrap properties or this is the "root" text, we don't
                // show curly braces
                if (wrapProperties && (!formatWhiteSpace || indentLevel != 0))
                {
                    format = formatWhiteSpace
                        ? string.Format("\r\n{0}{{{{\r\n{{0}}\r\n{0}}}}}", GetIndentation(indentLevel - 1)) // Not L10N
                        : "{{ {0} }}"; // Not L10N
                }
                else
                {
                    format = "{0}"; // Not L10N
                }
            }

            if (strings.Count == 0)
                return string.Empty;

            var separator = formatWhiteSpace ? ",\r\n" : ", "; // Not L10N
            return string.Format(result, string.Format(format, string.Join(separator, strings))); // Not L10N
        }
    }
}