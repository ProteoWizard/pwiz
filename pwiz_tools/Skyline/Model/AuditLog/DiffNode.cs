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
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.AuditLog
{
    public enum LogLevel { undo_redo, summary, all_info };

    public enum MessageType
    {
        is_,
        changed_from_to,
        changed_to,
        changed,
        removed,
        added,
        contains,
        removed_from,
        added_to,
        
        log_disabled,
        log_enabled,
        log_unlogged_changes,
        log_cleared
    }

    // Base class for all nodes
    public abstract class DiffNode
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
            return isName ? LogMessage.Quote(text) : text;
        }

        public DiffNodeNamePair FindFirstMultiChildParent(DiffTree tree, PropertyName name, bool shortenName, bool allowReflection, DiffNode parentNode = null)
        {
            var oneNode = Nodes.Count == 1;

            // The root property has no name, so we immediately go to the first child node
            if (Property.IsRoot)
                return oneNode ? Nodes[0].FindFirstMultiChildParent(tree, null, shortenName, allowReflection, this) : null;

            var parentObj = parentNode != null ? parentNode.Objects.FirstOrDefault() : null;

            var propertyNameString = Property.GetName(tree.Root.Objects.FirstOrDefault(), parentObj);

            // Collection elements should be referred to by their name or string representation
            var propName = IsCollectionElement
                ? new PropertyElementName(ObjectToString(allowReflection))
                : (Property.IsTab ? new PropertyTabName(propertyNameString) : new PropertyName(propertyNameString));

            var canIgnoreName = Property.IgnoreName && !IsCollectionElement;

            // If the node can't be displayed and its name cant be ignored,
            // we can't go further down the tree
            if (propName.Name == null && !canIgnoreName)
                return new DiffNodeNamePair(parentNode, name, allowReflection);

            var newName = name != null ? name.SubProperty(propName) : propName;

            var elemName = Property.GetElementName(parentObj);
            if (shortenName && elemName != null && !IsCollectionElement)
            {
                if (oneNode)
                {
                    // Remove everything from the path and replace it with the element name, e.g "Settings > DataSettings > GroupComparisons"
                    // becomes "GroupComparison:"
                    newName = PropertyName.Root.SubProperty(new PropertyElementTypeName(elemName));
                }
                else
                {
                    // Multiple changes have been made to the collection
                    newName = propName;
                }
            }

            if (canIgnoreName)
                newName = name;

            var objects = Objects.Select(AuditLogObject.GetAuditLogObject)
                .Where(o => o == null || o.IsName).ToArray();
            if (objects.Length == 2)
                if ((objects[0] != null) != (objects[1] != null) ||
                    (objects[0] != null && objects[1] != null && objects[0].AuditLogText != objects[1].AuditLogText) &&
                    Property.PropertyInfo.PropertyType != typeof(SrmSettings))
                        oneNode = false; // Stop recursion, since in the undo-redo/summary log we don't want to go deeper for objects where the name changed

            return oneNode ? Nodes[0].FindFirstMultiChildParent(tree, newName, shortenName, allowReflection, this) : new DiffNodeNamePair(this, newName, allowReflection);
        }

        public List<DiffNodeNamePair> FindAllLeafNodes(DiffTree tree, PropertyName name, bool allowReflection, DiffNode parentNode = null)
        {
            var result = new List<DiffNodeNamePair>();
            var noNodes = Nodes.Count == 0;

            // The root node has no name
            if (Property.IsRoot)
            {
                if (noNodes)
                    return null;

                for (var i = 0; i < Nodes.Count; ++i)
                    result.AddRange(Nodes[i].FindAllLeafNodes(tree, null, allowReflection, this));

                return result;
            }
                

            var propertyNameString = Property.GetName(tree.Root.Objects.FirstOrDefault(),
                parentNode != null ? parentNode.Objects.FirstOrDefault() : null);

            var isName = false;
            // Collection elements should be referred to by their name or string representation
            var propName = IsCollectionElement
                ? new PropertyElementName(ObjectToString(allowReflection, Objects.FirstOrDefault(o => o != null), out isName))
                : (Property.IsTab ? new PropertyTabName(propertyNameString) : new PropertyName(propertyNameString));

            // The name can not be ignored if the node is a collection change (since then the child nodes of the collection change
            // have the same attribute as the collection change node)
            var canIgnoreName = (Property.IgnoreName && !IsCollectionElement);

            var objs = Objects.ToArray();
            var isUnnamedNewObjChange = objs.Length == 2 && objs[1] == null && Nodes.Count > 0 &&
                                        !AuditLogObject.GetAuditLogObject(objs[0]).IsName;

            var newName = name != null ? name.SubProperty(propName) : propName;

            if (canIgnoreName)
                newName = name;

            // We can't display sub changes of an element if it's unnamed, so we display its 
            // string representation as a change
            if (IsCollectionElement && (!isName || isUnnamedNewObjChange || canIgnoreName))
            {
                result.Add(new DiffNodeNamePair(this, newName, allowReflection));
                return result;
            }

            var currentNode = this;
            var auditLogObjects = Objects.Select(AuditLogObject.GetAuditLogObject)
                .Where(o => AuditLogObject.GetObject(o) == null || o.IsName).ToArray();

            var objects = auditLogObjects.Select(AuditLogObject.GetObject).ToArray();

            if (Property.DiffProperties && Property.PropertyInfo.PropertyType != typeof(SrmSettings))
            {
                if (objects.Length == 2)
                {
                    // If this is a node where the name changed, we "expand" the diff tree to include ALL
                    // sub properties of the new named object (i.e treating the old and new object as unequal, even if some properties
                    // are unchanged
                    if ((objects[0] != null) != (objects[1] != null) ||
                        (objects[0] != null && objects[1] != null &&
                         auditLogObjects[0].AuditLogText != auditLogObjects[1].AuditLogText))
                    {
                        // If the new object is null ("Missing"), we don't want to display all properties having changed to null ("Missing")
                        if (objects[0] != null)
                        {
                            result.Add(new DiffNodeNamePair(this, newName, allowReflection));
                            currentNode = Reflector.ExpandDiffTree(tree, currentNode);
                        }
                    }
                }
                else if (objects.Length == 1 && objects[0] != null && this is ElementDiffNode)
                {
                    var elemNode = (ElementDiffNode)this;
                    if (!elemNode.Removed)
                    {
                        var properties = Reflector.GetProperties(objects[0].GetType());
                        if (properties.Any())
                        {
                            result.Add(new DiffNodeNamePair(this, newName, allowReflection));
                            if (Property.DiffProperties)
                                currentNode = Reflector.ExpandDiffTree(tree, currentNode, elemNode.ElementKey);
                        }
                    }
                }
            }

            noNodes = currentNode.Nodes.Count == 0;

            if (noNodes)
            {
                result.Add(new DiffNodeNamePair(currentNode, newName, allowReflection));
            }
            else
            {
                // This can't be converted into a foreach loop, since currentNode.Nodes might be modified in the recursive call,
                // which would break foreach

                // ReSharper disable once ForCanBeConvertedToForeach
                for (var i = 0; i < currentNode.Nodes.Count; i++)
                {
                    var n = currentNode.Nodes[i];
                    result.AddRange(n.FindAllLeafNodes(tree, newName, allowReflection, currentNode));
                }
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

            var newValue = NewValue == null ? "{2:Missing}" : ObjectToString(allowReflection, NewValue, out newIsName); // Not L10N
            var oldValue = OldValue == null ? "{2:Missing}" : ObjectToString(allowReflection, OldValue, out oldIsName); // Not L10N

            var sameValue = Equals(oldValue, newValue);

            // If the string representations are the same, we don't want to show either of them
            if (!sameValue)
            {
                // If one of them is a name, the other also has to be a name or its value is null, in which
                // case it gets set to "Missing"
                if (oldIsName || newIsName || (level == LogLevel.all_info && !Expanded))
                {
                    //if (!oldIsName && !newIsName && IsCollectionElement)
                    //    return new LogMessage(MessageType.coll_changed_to, Expanded, name.ToString(), oldValue, newValue);

                    return new LogMessage(level, MessageType.changed_from_to, string.Empty, Expanded, name.ToString(), oldValue, newValue);
                }
            }

            if (!sameValue || Expanded)
            {
                if (newValue != null)
                    return new LogMessage(level, Expanded ? MessageType.is_ : MessageType.changed_to, string.Empty, Expanded, name.ToString(), newValue);
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
            if (level == LogLevel.undo_redo && name.Parent.OverrideChildSeparator != null)
            {
                return new LogMessage(level, Removed ? MessageType.removed : MessageType.added, string.Empty, Expanded, name.ToString());
            }
            else // summary, all_info
            {
                var value = ObjectToString(allowReflection);

                if (name.IsElement != (name.Name == value))
                    System.Diagnostics.Debugger.Break();

                if (name.Name == value)
                    name = name.Parent;

                return new LogMessage(level, Removed ? MessageType.removed_from : Expanded ? MessageType.contains : MessageType.added_to, string.Empty, Expanded, name.ToString(), value);
            }
        }
    }

    public class DiffNodeNamePair
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