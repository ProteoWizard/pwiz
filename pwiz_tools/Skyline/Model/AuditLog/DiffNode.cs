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
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.AuditLog
{
    public enum LogLevel { undo_redo, summary, all_info };

    // Base class for all nodes
    public abstract class DiffNode : Immutable
    {
        protected DiffNode(Property property, PropertyPath propertyPath, IEnumerable<DiffNode> nodes = null, bool expanded = false)
        {
            Property = property;
            PropertyPath = propertyPath;
            Nodes = nodes != null ? new List<DiffNode>(nodes) : new List<DiffNode>();
            Expanded = expanded;
        }

        public Property Property { get; protected set; }
        public PropertyPath PropertyPath { get; protected set; }
        public IList<DiffNode> Nodes { get; protected set; }

        // The current object should be the first
        public abstract IEnumerable<object> Objects { get; }

        public abstract bool IsCollectionElement { get; }

        public bool Expanded { get; private set; }
        public bool IsFirstExpansionNode { get; set; }

        public virtual DiffNode ChangeExpanded(bool expanded)
        {
            return ChangeProp(ImClone(this), im => im.Expanded = expanded);
        }

        public abstract LogMessage ToMessage(PropertyName name, LogLevel level, bool allowReflection);

        // For debugging
        public override string ToString()
        {
            return ToMessage(new PropertyName(PropertyPath.ToString()), LogLevel.all_info, true).ToString();
        }

        protected virtual string ObjectToString(bool allowReflection)
        {
            bool isName;
            return ObjectToString(allowReflection, Objects.FirstOrDefault(o => o != null), out isName);
        }

        protected virtual string ObjectToString(bool allowReflection, object obj, out bool isName)
        {
            bool usesReflection;
            var auditLogObj = AuditLogObject.GetAuditLogObject(obj, out usesReflection);
            isName = auditLogObj.IsName;

            if (usesReflection)
                return !allowReflection ? null : auditLogObj.AuditLogText;

            var text = auditLogObj.AuditLogText;
            return isName && !(obj is DocNode) ? LogMessage.Quote(text) : text; // DocNodes shouldn't have quotes around them
        }

        public DiffNodeNamePair FindFirstMultiChildParent(DiffTree tree, PropertyName name, bool shortenName, bool allowReflection, DiffNode parentNode = null)
        {
            var oneNode = Nodes.Count == 1;

            var propertyNameString = Property.GetName(tree.Root, this, parentNode);

            // Collection elements should be referred to by their name or string representation
            var propName = IsCollectionElement
                ? new PropertyElementName(ObjectToString(allowReflection))
                : (Property.IsTab ? new PropertyTabName(propertyNameString) : new PropertyName(propertyNameString));

            // If the node can't be displayed and its name cant be ignored,
            // we can't go further down the tree. This can happen theoretically but hasn't occured anywhere yet
            if (propName.Name == null && !Property.IgnoreName)
                return new DiffNodeNamePair(parentNode == null ? null : parentNode.ChangeExpanded(false), name, allowReflection);

            var newName = name.SubProperty(propName);

            var elemName = Property.GetElementName();
            if (shortenName && Property.GetElementName() != null && !IsCollectionElement)
            {
                if (oneNode)
                {
                    // Remove everything from the path and replace it with the element name, e.g "Settings > DataSettings > GroupComparisons"
                    // becomes "GroupComparison:"
                    newName = PropertyName.ROOT.SubProperty(new PropertyElementName(elemName));
                }
                else
                {
                    // Multiple changes have been made to the collection
                    newName = propName;
                }
            }

            if (Property.IgnoreName && !IsCollectionElement)
                newName = name;

            var objects = Objects.Select(AuditLogObject.GetAuditLogObject)
                .Where(o => o == null || o.IsName).ToArray();
            
            if (objects.Length == 2)
            {
                var type = Property.GetPropertyType(objects[1], objects[0]);
                if (((objects[0] != null) != (objects[1] != null) ||
                    (objects[0] != null && objects[1] != null && objects[0].AuditLogText != objects[1].AuditLogText && !typeof(DocNode).IsAssignableFrom(type))) &&
                    !Property.IsRoot)
                        oneNode = false; // Stop recursion, since in the undo-redo/summary log we don't want to go deeper for objects where the name changed
            }

            return oneNode && !IsFirstExpansionNode
                ? Nodes[0].FindFirstMultiChildParent(tree, newName, shortenName, allowReflection, this)
                : new DiffNodeNamePair(ChangeExpanded(false), newName, allowReflection);
        }

        public List<DiffNodeNamePair> FindAllLeafNodes(DiffTree tree, PropertyName name, bool allowReflection, DiffNode parentNode = null)
        {
            var result = new List<DiffNodeNamePair>();

            var propertyNameString = Property.GetName(tree.Root, this, parentNode);

            var isName = false;
            // Collection elements should be referred to by their name or string representation
            var propName = IsCollectionElement
                ? new PropertyElementName(ObjectToString(allowReflection, Objects.FirstOrDefault(o => o != null), out isName))
                : (Property.IsTab ? new PropertyTabName(propertyNameString) : new PropertyName(propertyNameString));

            // The name can not be ignored if the node is a collection change (since then the child nodes of the collection change
            // have the same attribute as the collection change node)
            var canIgnoreName = (Property.IgnoreName && !IsCollectionElement);

            if (!canIgnoreName)
                name = name != null ? name.SubProperty(propName) : propName;

            // We can't display sub changes of an element if it's unnamed, so we display its 
            // string representation as a change
            if (IsCollectionElement && !isName && !canIgnoreName)
            {
                result.Add(new DiffNodeNamePair(this, name, allowReflection));
                return result;
            }
            
            var obj = Objects.FirstOrDefault();
            var isNamedChange = IsFirstExpansionNode || (obj != null && AuditLogObject.GetAuditLogObject(obj).IsName) &&
                                    Expanded && !canIgnoreName;

            if (isNamedChange)
                result.Add(new DiffNodeNamePair(this, name, allowReflection));

            if (Nodes.Count == 0)
            {
                if (!isNamedChange)
                    result.Add(new DiffNodeNamePair(this, name, allowReflection));
            }
            else
            {
                foreach (var n in Nodes)
                    result.AddRange(n.FindAllLeafNodes(tree, name, allowReflection, this));
            }

            return result;
        }
    }

    // Property change
    public class PropertyDiffNode : DiffNode
    {
        public PropertyDiffNode(Property property, PropertyPath propertyPath, object oldValue, object newValue, IEnumerable<DiffNode> nodes = null, bool expanded = false)
            : base(property, propertyPath, nodes, expanded)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }

        public override IEnumerable<object> Objects
        {
            get
            {
                yield return NewValue;
                yield return OldValue;
            }
        }

        public override bool IsCollectionElement { get { return false; } }

        public object OldValue { get; private set; }
        public object NewValue { get; private set; }

        public override LogMessage ToMessage(PropertyName name, LogLevel level, bool allowReflection)
        {
            var newIsName = false;
            var oldIsName = false;

            // new-/oldValue can are only null if reflection is not allowed and their AuditLogText uses reflection
            var newValue = NewValue == null ? LogMessage.MISSING : ObjectToString(allowReflection, NewValue, out newIsName);
            var oldValue = OldValue == null ? LogMessage.MISSING : ObjectToString(allowReflection, OldValue, out oldIsName);

            var sameValue = Equals(oldValue, newValue);

            if (Expanded && level == LogLevel.all_info)
            {
                return new LogMessage(level, Property.IsCollectionType(NewValue) ? MessageType.contains : MessageType.is_,
                    string.Empty, Expanded, name.ToString(), newValue);
            }

            // If the string representations are the same, we don't want to show either of them
            if (!sameValue)
            {
                // If one of them is a name, the other also has to be a name or its value is null, in which
                // case it gets set to "Missing"
                if (oldIsName || newIsName || (level == LogLevel.all_info && !Expanded))
                {
                    return new LogMessage(level, MessageType.changed_from_to, string.Empty, Expanded, name.ToString(), oldValue, newValue);
                }
                else if (newValue != null)
                {
                    return new LogMessage(level, MessageType.changed_to, string.Empty, Expanded, name.ToString(), newValue);
                }
            }

            return new LogMessage(level, MessageType.changed, string.Empty, Expanded, name.ToString());
        }
    }

    // Collection element was changed
    public class ElementPropertyDiffNode : PropertyDiffNode
    {
        public ElementPropertyDiffNode(Property property, PropertyPath propertyPath, object oldValue, object newValue, object elementKey, IEnumerable<DiffNode> nodes = null, bool treatedUnequal = false)
            : base(property, propertyPath, oldValue, newValue, nodes, treatedUnequal)
        {
            ElementKey = elementKey;
        }

        public object ElementKey { get; private set; }
        public override bool IsCollectionElement { get { return true; } }
    }

    // Collection element was added or removed
    public class ElementDiffNode : DiffNode
    {
        public ElementDiffNode(Property property, PropertyPath propertyPath, object element, object elementKey, bool removed, IEnumerable<DiffNode> nodes = null, bool expanded = false)
            : base(property, propertyPath, nodes, expanded)
        {
            Element = element;
            ElementKey = elementKey;
            Removed = removed;
        }

        public object Element { get; private set; }
        public object ElementKey { get; private set; }
        public bool Removed { get; private set; }

        public override IEnumerable<object> Objects { get { yield return Element; } }

        public override bool IsCollectionElement {  get { return true; } }

        public override LogMessage ToMessage(PropertyName name, LogLevel level, bool allowReflection)
        {
            var value = ObjectToString(allowReflection);

            if (IsCollectionElement)
                name = name.Parent;

            return new LogMessage(level, Removed ? MessageType.removed_from : Expanded ? MessageType.contains : MessageType.added_to, string.Empty, Expanded, name.ToString(), value);
        }
    }

    public class DiffNodeNamePair : Immutable
    {
        public DiffNodeNamePair(DiffNode node, PropertyName name, bool allowReflection)
        {
            Node = node;
            Name = name;
            AllowReflection = allowReflection;
        }

        public LogMessage ToMessage(LogLevel level)
        {
            return ToMessage(Node, Name, level, AllowReflection);
        }

        public static LogMessage ToMessage(DiffNode node, PropertyName name, LogLevel level, bool allowReflection)
        {
            return node.ToMessage(name, level, allowReflection);
        }

        public DiffNodeNamePair ChangeName(PropertyName name)
        {
            return ChangeProp(ImClone(this), im => im.Name = name);
        }

        public DiffNode Node { get; private set; }
        public PropertyName Name { get; private set; }
        public bool AllowReflection { get; private set; }
    }

    public class DiffTree
    {
        public DiffTree(DiffNode root, DateTime timeStamp)
        {
            Root = root;
            TimeStamp = timeStamp;
        }

        public DiffNode Root { get; private set; }
        public DateTime TimeStamp { get; private set; }
    }
}