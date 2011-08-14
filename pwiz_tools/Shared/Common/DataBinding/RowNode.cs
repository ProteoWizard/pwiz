using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Common.DataBinding
{
    public class RowNode
    {
        private IDictionary<IdentifierPath, ICollection<RowNode>> _children;
        public RowNode(RowItem rowItem)
        {
            RowItem = rowItem;
            _children = new Dictionary<IdentifierPath, ICollection<RowNode>>();
        }

        public RowItem RowItem { get; private set; }
        public IdentifierPath IdentifierPath { get { return RowItem.SublistId; } }
        public IEnumerable<RowNode> GetChildren(IdentifierPath identifierPath)
        {
            ICollection<RowNode> children;
            if (_children.TryGetValue(identifierPath, out children))
            {
                return children;
            }
            return new RowNode[0];
        }
        public void AddChild(RowNode child)
        {
            ICollection<RowNode> children;
            if (!_children.TryGetValue(child.IdentifierPath, out children))
            {
                children = new List<RowNode>();
                _children.Add(child.IdentifierPath, children);
            }
            children.Add(child);
        }
        public IEnumerable<RowNode> GetDescendants(IdentifierPath identifierPath)
        {
            var result = new List<RowNode>();
            foreach (var entry in _children)
            {
                if (!identifierPath.StartsWith(entry.Key))
                {
                    continue;
                }

                if (entry.Key.Equals(identifierPath))
                {
                    result.AddRange(entry.Value);
                    continue;
                }
                foreach (var child in entry.Value)
                {
                    result.AddRange(child.GetDescendants(identifierPath));
                }
            }
            return result;
        }
    }
}
