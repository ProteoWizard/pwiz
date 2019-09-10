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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.AuditLog
{
    public partial class Reflector<T>
    {
        public static string ToString(ObjectPair<object> rootPair, SrmDocument.DOCUMENT_TYPE docType, T obj, ToStringState state)
        {
            var objectInfo = new ObjectInfo<object>().ChangeNewObject(obj)
                .ChangeRootObjectPair(rootPair ?? ObjectPair<object>.Create(null, null));
            var rootProp = RootProperty.Create(typeof(T));

            var enumerator = EnumerateDiffNodes(objectInfo, rootProp, docType, true);

            return ToString(objectInfo.ParentObjectPair, docType, DiffTree.FromEnumerator(enumerator, DateTime.UtcNow).Root, state);
        }

        /// <summary>
        /// Converts the given object to a string, showing each of its properties values.
        /// </summary>
        /// <param name="rootPair">old and new document, can be null</param>
        /// <param name="docType">May determine whether human readable version requires "peptide"->"molecule" translation</param> // CONSIDER: does this belong in ToStringState?
        /// <param name="rootNode">diff node describing root object change</param>
        /// <param name="state">describes how to format the string</param>
        /// <returns>String representation</returns>
        public static string ToString(ObjectPair<object> rootPair, SrmDocument.DOCUMENT_TYPE docType, DiffNode rootNode, ToStringState state)
        {
            return Reflector.ToString(rootPair, docType, rootNode, null, state).Trim();
        }

        public static string ToString(T obj)
        {
            return ToString(null, SrmDocument.DOCUMENT_TYPE.none, obj, null);
        }
    }

    public static partial class Reflector
    {
        private const int MAX_STACK_DEPTH = 64;
        private const int TAB_SIZE = 4;

        /// <summary>
        /// Checks whether the given object can safely be converted to a string by simply calling ToString
        /// </summary>
        public static bool HasToString(object obj)
        {
            if (obj == null)
                return false;

            return obj is IFormattable ||
                   (obj.GetType().Namespace == @"System" && !IsCollectionType(obj.GetType()));
        }

        private static string GetIndentation(int indentLevel)
        {
            if (indentLevel <= 0)
                return string.Empty;

            return new StringBuilder(TAB_SIZE * indentLevel).Insert(0, new string(' ', TAB_SIZE), indentLevel)
                .ToString();
        }

        private static string Indent(bool indent, string s, int indentLevel)
        {
            if (!indent || s == null || indentLevel <= 0)
                return s;

            s = GetIndentation(indentLevel) + s;
            return s;
        }

        public static string ToString(ObjectPair<object> rootPair, SrmDocument.DOCUMENT_TYPE docType, DiffNode node, DiffNode parentNode, ToStringState state)
        {
            state = state ?? ToStringState.DEFAULT;

            if (node == null)
                return string.Empty;

            var property = node.Property;

            // If the name is not getting ignored there will be an equal sign in front of this text,
            // so dont indent, unless the object is a collection element, in which case it has no equal sign
            var indent = (property.IgnoreName || property.IsCollectionElement) && state.FormatWhitespace;

            if (node.Nodes.Count == 0)
            {
                var obj = node.Objects.First();
                if (obj is IAuditLogObject auditLogObj && (auditLogObj.IsName || GetProperties(obj.GetType()).Count == 0))
                {
                    var text = auditLogObj.IsName && !(obj is DocNode)
                        ? LogMessage.Quote(auditLogObj.AuditLogText)
                        : auditLogObj.AuditLogText;

                    return Indent(indent, text, state.IndentLevel - 1);
                } 
                else
                {
                    if (node is CollectionPropertyDiffNode)
                        return LogMessage.EMPTY;

                    if (obj == null)
                        return LogMessage.MISSING;

                    return Indent(indent,
                        AuditLogToStringHelper.ToString(obj, property.DecimalPlaces, o => ToString(
                            node.Property.GetPropertyType(o), rootPair, docType, o, state)), state.IndentLevel - 1);
                }
            }

            return ToStringInternal(rootPair, docType, node, parentNode, state);
        }

        private static string ToStringInternal(ObjectPair<object> rootPair, SrmDocument.DOCUMENT_TYPE docType, DiffNode node, DiffNode parentNode, ToStringState state)
        {
            if (node == null)
                return string.Empty;

            state = state.IncreaseStackDepth();
            if (state.StackDepth > MAX_STACK_DEPTH)
                throw new StackOverflowException();

            var obj = node.Objects.First();
            var property = node.Property;
            var auditLogObj = AuditLogObject.GetAuditLogObject(obj, property.DecimalPlaces, out _);

            var result = @"{0}";
            string format;

            // If the parent has ignore name set to true but it's a list, we still want to show
            // the name for the elements
            if ((!property.IgnoreName || parentNode is CollectionPropertyDiffNode)  && auditLogObj.IsName)
            {
                var name = LogMessage.Quote(auditLogObj.AuditLogText);
                var indent = state.FormatWhitespace && (node.Property.IgnoreName || parentNode is CollectionPropertyDiffNode);
                if (node.Nodes.Count != 0)
                    result = Indent(indent, string.Format(@"{0}: {{0}}", name), state.IndentLevel - 1);
                else
                    return name;
            }

            string start, end;
            var isCollection = node is CollectionPropertyDiffNode;
            if (isCollection)
            {
                if (node.Nodes.Count == 0)
                    return LogMessage.EMPTY;

                start = @"[";
                end = @"]";
            }
            else
            {
                start = @"{{";
                end = @"}}";
            }

            // If we don't want to wrap properties or this is the "root" text, we don't
            // show curly braces
            if (state.WrapProperties && (!state.FormatWhitespace || state.IndentLevel != 0))
            {
                var prepend = parentNode is CollectionPropertyDiffNode ? string.Empty : Environment.NewLine;
                var indentation = GetIndentation(state.IndentLevel - 1);
                var openingIndent = auditLogObj.IsName ? string.Empty : indentation;
                format = state.FormatWhitespace
                    ? string.Format(@"{0}{3}{1}{5}{{0}}{5}{4}{2}", prepend, start, end, openingIndent, indentation, Environment.NewLine)
                    : string.Format(@"{0} {{0}} {1}", start, end);
            }
            else
            {
                format = @"{0}";
            }

            var strings = new List<string>();
            foreach (var subNode in node.Nodes)
            {   
                if (subNode.Property.IgnoreName || isCollection)
                {
                    var newState = state.ChangeWrapProperties(state.WrapProperties && !subNode.Property.IgnoreName);
                    if (!subNode.Property.IgnoreName)
                        newState = newState.ChangeIndentLevel(state.IndentLevel + 1);
                    var str = ToString(rootPair, docType, subNode, node, newState);
                    if (!string.IsNullOrEmpty(str))
                        strings.Add(str);
                }
                else
                {

                    var str = ToString(rootPair, docType, subNode, node,
                        state.ChangeWrapProperties(true).ChangeIndentLevel(state.IndentLevel + 1));
                    if (!string.IsNullOrEmpty(str))
                        strings.Add(Indent(state.FormatWhitespace,
                            subNode.Property.GetName(rootPair, subNode, node) + @" = " + str, state.IndentLevel));
                }
            }


            if (strings.Count == 0)
                return string.Empty;

            var separator = string.Format(@",{0}", state.FormatWhitespace ? Environment.NewLine : @" ");
            return string.Format(result, string.Format(format, string.Join(separator, strings)));
        }

        #region Wrapper functions

        public static string ToString(Type objectType, ObjectPair<object> rootPair, SrmDocument.DOCUMENT_TYPE docType, object obj, ToStringState state)
        {
            var type = typeof(Reflector<>).MakeGenericType(objectType);
            var toString = type.GetMethod("ToString", BindingFlags.Public | BindingFlags.Static, null,
                new[] {typeof(ObjectPair<object>), typeof(SrmDocument.DOCUMENT_TYPE), objectType, typeof(ToStringState) },
                null);

            Assume.IsNotNull(toString);

            var reflector = Activator.CreateInstance(type);

            // ReSharper disable once PossibleNullReferenceException
            return (string)toString.Invoke(reflector, new[] { rootPair, docType, obj, state });
        }

        #endregion
    }

    public class ToStringState : Immutable
    {
        public static ToStringState DEFAULT = new ToStringState();

        public ToStringState(bool wrapProperties = true, bool formatWhitespace = false, int indentLevel = 0, int stackDepth = 0)
        {
            WrapProperties = wrapProperties;
            FormatWhitespace = formatWhitespace;
            IndentLevel = indentLevel;
            StackDepth = stackDepth;
        }

        public ToStringState ChangeWrapProperties(bool wrapProperties)
        {
            return ChangeProp(ImClone(this), im => im.WrapProperties = wrapProperties);
        }

        public ToStringState ChangeFormatWhitespace(bool formatWhitespace)
        {
            return ChangeProp(ImClone(this), im => im.FormatWhitespace = formatWhitespace);
        }

        public ToStringState ChangeIndentLevel(int indentLevel)
        {
            return ChangeProp(ImClone(this), im => im.IndentLevel = indentLevel);
        }

        public ToStringState IncreaseStackDepth()
        {
            return ChangeProp(ImClone(this), im => ++im.StackDepth);
        }

        public bool WrapProperties { get; private set; }
        public bool FormatWhitespace { get; private set; }
        public int IndentLevel { get; private set; }
        public int StackDepth { get; private set; }
    }
}
