using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.AuditLog
{
    public partial class Reflector<T>
    {
        private static string ToString(ObjectPair<object> rootPair, T obj, bool wrapProperties = true, bool formatWhitespace = false, int indentLevel = 0, int stackDepth = 0)
        {
            var objectInfo = new ObjectInfo<object>().ChangeNewObject(obj)
                .ChangeRootObjectPair(rootPair ?? ObjectPair<object>.Create(null, null));
            var rootProp = RootProperty.Create(typeof(T));

            var enumerator = EnumerateDiffNodes(objectInfo, rootProp, true);

            return ToString(objectInfo.ParentObjectPair, DiffTree.FromEnumerator(enumerator, DateTime.Now).Root, wrapProperties,
                formatWhitespace, indentLevel, stackDepth);
        }

        /// <summary>
        /// Converts the given object to a string, showing each of its properties values.
        /// </summary>
        /// <param name="rootPair">old and new document, can be null</param>
        /// <param name="rootNode">diff node describing root object change</param>
        /// <param name="wrapProperties">true if properties should be surrounded by curly braces/brackets</param>
        /// <param name="formatWhitespace">Whether to use tabs and new lines or not</param>
        /// <param name="indentLevel">level of indentation</param>
        /// <param name="stackDepth">depth of stack, used for stack overflow detection</param>
        /// <returns>String representation</returns>
        public static string ToString(ObjectPair<object> rootPair, DiffNode rootNode, bool wrapProperties, bool formatWhitespace, int indentLevel = 0, int stackDepth = 0)
        {
            return Reflector.ToString(rootPair, rootNode, wrapProperties, formatWhitespace, null, indentLevel, stackDepth).Trim();
        }

        /// <summary>
        /// Converts the given object to a string, showing each of its properties values.
        /// Note that default values of properties might not work with this function
        /// since no parent or root objects are provided
        /// </summary>
        public static string ToString(T obj, bool formatWhitespace = false)
        {
            return ToString(null, obj, true, formatWhitespace);
        }
    }

    public partial class Reflector
    {
        private const int MAX_STACK_DEPTH = 64;

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

        private static string GetIndentation(int indentLevel)
        {
            if (indentLevel <= 0)
                return string.Empty;

            return new StringBuilder(4 * indentLevel).Insert(0, "    ", indentLevel).ToString(); // Not L10N
        }

        private static string Indent(bool indent, string s, int indentLevel)
        {
            if (!indent || s == null || indentLevel <= 0)
                return s;

            s = GetIndentation(indentLevel) + s;
            return s;
        }

        public static string ToString(ObjectPair<object> rootPair, DiffNode node, bool wrapProperties, bool formatWhitespace, DiffNode parentNode,
            int indentLevel, int stackDepth)
        {
            if (node == null)
                return string.Empty;

            var property = node.Property;

            // If the name is not getting ignored there will be an equal sign in front of this text,
            // so dont indent, unless the object is a collection element, in which case it has no equal sign
            var indent = (property.IgnoreName || property.IsCollectionElement) && formatWhitespace;

            if (node.Nodes.Count == 0)
            {
                var obj = node.Objects.First();
                var auditLogObj = obj as IAuditLogObject;
                if (auditLogObj != null && auditLogObj.IsName)
                {
                    return Indent(indent, LogMessage.Quote(auditLogObj.AuditLogText), indentLevel);
                } 
                else
                {
                    if (node is CollectionPropertyDiffNode)
                        return LogMessage.EMPTY;

                    if (obj == null)
                        return LogMessage.MISSING;

                    return Indent(indent,
                        AuditLogToStringHelper.InvariantToString(obj) ??
                        AuditLogToStringHelper.KnownTypeToString(obj) ?? ToString(
                            node.Property.GetPropertyType(obj), rootPair, obj, wrapProperties, formatWhitespace, indentLevel, stackDepth), indentLevel - 1);
                }
            }

            return ToStringInternal(rootPair, node, wrapProperties, formatWhitespace, indentLevel, parentNode, stackDepth);
        }

        private static string ToStringInternal(ObjectPair<object> rootPair, DiffNode node, bool wrapProperties, bool formatWhiteSpace, int indentLevel, DiffNode parentNode, int stackDepth)
        {
            if (node == null)
                return string.Empty;

            if (++stackDepth > MAX_STACK_DEPTH)
                throw new StackOverflowException();

            var obj = node.Objects.First();
            var property = node.Property;
            var auditLogObj = AuditLogObject.GetAuditLogObject(obj);

            var result = "{0}"; // Not L10N
            string format;

            if (!property.IgnoreName && auditLogObj.IsName)
            {
                var name = LogMessage.Quote(auditLogObj.AuditLogText);
                var indent = formatWhiteSpace && (node.Property.IgnoreName || parentNode is CollectionPropertyDiffNode);
                if (node.Nodes.Count != 0)
                    result = Indent(indent, string.Format("{0}: {{0}}", name), indentLevel - 1); // Not L10N
                else
                    return name;
            }

            string start, end;
            var isCollection = node is CollectionPropertyDiffNode;
            if (isCollection)
            {
                if (node.Nodes.Count == 0)
                    return LogMessage.EMPTY;

                start = "["; // Not L10N
                end = "]"; // Not L10N
            }
            else
            {
                start = "{{"; // Not L10N
                end = "}}"; // Not L10N
            }

            // If we don't want to wrap properties or this is the "root" text, we don't
            // show curly braces
            if (wrapProperties && (!formatWhiteSpace || indentLevel != 0))
            {
                var prepend = parentNode is CollectionPropertyDiffNode ? string.Empty : "\r\n"; // Not L10N
                var indentation = GetIndentation(indentLevel - 1);
                var openingIndent = auditLogObj.IsName ? string.Empty : indentation;
                format = formatWhiteSpace
                    ? string.Format("{0}{3}{1}\r\n{{0}}\r\n{4}{2}", prepend, start, end, openingIndent, indentation) // Not L10N
                    : string.Format("{0} {{0}} {1}", start, end); // Not L10N
            }
            else
            {
                format = "{0}"; // Not L10N
            }

            var strings = new List<string>();
            foreach (var subNode in node.Nodes)
            {
                if (subNode.Property.IgnoreName || isCollection)
                {
                    var str = ToString(rootPair, subNode, wrapProperties && !subNode.Property.IgnoreName, formatWhiteSpace, node, indentLevel + 1, stackDepth);
                    if (!string.IsNullOrEmpty(str))
                        strings.Add(str);
                }
                else
                {
                    var str = ToString(rootPair, subNode, true, formatWhiteSpace, node, indentLevel + 1, stackDepth);
                    if (!string.IsNullOrEmpty(str))
                        strings.Add(Indent(formatWhiteSpace,
                            subNode.Property.GetName(rootPair, subNode, node) + " = " + str, indentLevel)); // Not L10N
                }
            }


            if (strings.Count == 0)
                return string.Empty;

            var separator = formatWhiteSpace ? ",\r\n" : ", "; // Not L10N
            return string.Format(result, string.Format(format, string.Join(separator, strings))); // Not L10N
        }

        #region Wrapper functions

        public static string ToString(Type objectType, ObjectPair<object> rootPair, object obj, bool wrapProperties, bool formatWhitespace, int indentLevel, int stackDepth)
        {
            var type = typeof(Reflector<>).MakeGenericType(objectType);
            var toString = type.GetMethod("ToString", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(ObjectPair<object>), objectType, typeof(bool), typeof(bool), typeof(int), typeof(int) },
                null);

            Assume.IsNotNull(toString);

            var reflector = Activator.CreateInstance(type);

            // ReSharper disable once PossibleNullReferenceException
            return (string)toString.Invoke(reflector, new[] { rootPair, obj, wrapProperties, formatWhitespace, indentLevel, stackDepth });
        }

        #endregion
    }
}