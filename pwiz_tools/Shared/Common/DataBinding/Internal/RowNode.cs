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

using System.Collections.Generic;
using System.Linq;

namespace pwiz.Common.DataBinding.Internal
{
    internal class RowNode
    {
        private readonly IDictionary<PropertyPath, ICollection<RowNode>> _children;
        public RowNode(RowItem rowItem)
        {
            RowItem = rowItem;
            _children = new Dictionary<PropertyPath, ICollection<RowNode>>();
        }
        public RowNode(IEnumerable<RowNode> children) : this(new RowItem(null, null))
        {
            foreach (var child in children)
            {
                AddChild(child);
            }
        }
        public bool IsLeaf { get { return _children.Count == 0; } }
        public RowItem RowItem { get; private set; }
        public PropertyPath PropertyPath { get { return RowItem.SublistId; } }
        public IEnumerable<RowNode> GetChildren(PropertyPath propertyPath)
        {
            ICollection<RowNode> children;
            if (_children.TryGetValue(propertyPath, out children))
            {
                return children;
            }
            return new RowNode[0];
        }
        public IEnumerable<PropertyPath> GetChildIds()
        {
            return _children.Keys;
        }
        public void AddChild(RowNode child)
        {
            ICollection<RowNode> children;
            if (!_children.TryGetValue(child.PropertyPath, out children))
            {
                children = new List<RowNode>();
                _children.Add(child.PropertyPath, children);
            }
            children.Add(child);
        }
        public IEnumerable<RowNode> GetDescendants(PropertyPath propertyPath)
        {
            var result = new List<RowNode>();
            foreach (var entry in _children)
            {
                if (!propertyPath.StartsWith(entry.Key))
                {
                    continue;
                }

                if (entry.Key.Equals(propertyPath))
                {
                    result.AddRange(entry.Value);
                    continue;
                }
                foreach (var child in entry.Value)
                {
                    result.AddRange(child.GetDescendants(propertyPath));
                }
            }
            return result;
        }
        public IEnumerable<RowNode> GetLeaves()
        {
            if (IsLeaf)
            {
                return new[] {this};
            }
            return _children.Values.SelectMany(children => children.SelectMany(child => child.GetLeaves()));
        }
    }
}
