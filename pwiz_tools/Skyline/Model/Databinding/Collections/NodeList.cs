/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Databinding.Collections
{
    public abstract class NodeList<TNode> : SkylineObjectList<IdentityPath, TNode> where TNode : SkylineDocNode
    {
        private IList<IdentityPath> _ancestorIdentityPaths = ImmutableList.Empty<IdentityPath>();

        protected NodeList(SkylineDataSchema dataSchema, IList<IdentityPath> ancestorIdentityPaths) : base(dataSchema)
        {
            AncestorIdentityPaths = ancestorIdentityPaths;
        }
        protected NodeList(SkylineDataSchema dataSchema, IdentityPath ancestorIdentityPath)
            : this(dataSchema, ImmutableList.Singleton(ancestorIdentityPath))
        {
        }

        public IList<IdentityPath> AncestorIdentityPaths
        {
            get { return _ancestorIdentityPaths; }
            set 
            { 
                var newValue = ImmutableList.ValueOf(value); 
                if (Equals(AncestorIdentityPaths, newValue))
                {
                    return;
                }
                _ancestorIdentityPaths = newValue;
                OnDocumentChanged();
            }
        }
        public void SetAncestorIdentityPaths(IEnumerable<IdentityPath> identityPaths)
        {
            var newPaths = ImmutableList.ValueOf(identityPaths);
            if (Equals(newPaths, _ancestorIdentityPaths))
            {
                return;
            }
            _ancestorIdentityPaths = newPaths;
            OnDocumentChanged();
        }


        protected override IList<IdentityPath> ListKeys()
        {
            return RecurseListingKeys(IdentityPath.ROOT, SrmDocument).ToArray();
        }

        protected IEnumerable<IdentityPath> RecurseListingKeys(IdentityPath identityPath, DocNode docNode)
        {
            bool includeThis = AncestorIdentityPaths.Any(ancestoryIdPath => StartsWith(identityPath, ancestoryIdPath));
            if (identityPath.Length == NodeDepth)
            {
                if (includeThis)
                {
                    return new[] {identityPath};
                }
            }
            var docNodeParent = docNode as DocNodeParent;
            if (null == docNodeParent || identityPath.Length >= NodeDepth)
            {
                return new IdentityPath[0];
            }

            var result = new List<IdentityPath>();
            foreach (var child in docNodeParent.Children)
            {
                var childIdentityPath = new IdentityPath(identityPath, child.Id);
                if (!includeThis &&
                    !AncestorIdentityPaths.Any(ancestorIdPath => StartsWith(ancestorIdPath, childIdentityPath)))
                {
                    continue;
                }
                result.AddRange(RecurseListingKeys(childIdentityPath, child));
            }
            return result;
        }

        protected abstract int NodeDepth { get; }

        protected static bool StartsWith(IdentityPath child, IdentityPath ancestor)
        {
            if (child.Length < ancestor.Length)
            {
                return false;
            }
            for (int i = 0; i < ancestor.Length; i++)
            {
                if (!Equals(ancestor.GetIdentity(i), child.GetIdentity(i)))
                {
                    return false;
                }
            }
            return true;
        }
        public override IdentityPath GetKey(TNode item)
        {
            return item.IdentityPath;
        }
    }
}
