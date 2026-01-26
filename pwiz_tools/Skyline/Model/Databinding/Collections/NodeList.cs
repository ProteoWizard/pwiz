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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Databinding.Collections
{
    public abstract class NodeList<TNode> : SkylineObjectList<TNode> where TNode : SkylineDocNode
    {
        protected static readonly ImmutableList<IdentityPath> ROOT_IDENTITY_PATH = ImmutableList.Singleton(IdentityPath.ROOT);
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
                FireListChanged();
            }
        }
        
        protected IEnumerable<IdentityPath> ListKeys()
        {
            return RecurseListingKeys(IdentityPath.ROOT, SrmDocument);
        }

        protected IEnumerable<IdentityPath> RecurseListingKeys(IdentityPath identityPath, DocNode docNode)
        {
            CancellationToken.ThrowIfCancellationRequested();
            bool includeThis = AncestorIdentityPaths.Any(ancestorIdPath => StartsWith(identityPath, ancestorIdPath));
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
                return Array.Empty<IdentityPath>();
            }

            var result = new List<IdentityPath>();
            foreach (var child in docNodeParent.Children)
            {
                CancellationToken.ThrowIfCancellationRequested();
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
                if (!ReferenceEquals(ancestor.GetIdentity(i), child.GetIdentity(i)))
                {
                    return false;
                }
            }
            return true;
        }

        public override IEnumerable GetItems()
        {
            return ConstructItems(ListKeys());
        }

        protected IEnumerable ConstructItems(IEnumerable<IdentityPath> keys)
        {
            var proteins = new Dictionary<IdentityPath, Protein>();
            var peptides = new Dictionary<IdentityPath, Entities.Peptide>();
            var precursors = new Dictionary<IdentityPath, Precursor>();
            foreach (var key in keys)
            {
                Protein protein;
                var proteinPath = key.Depth == 0 ? key : key.GetPathTo(0);
                if (key.Depth == 0 || !proteins.TryGetValue(proteinPath, out protein))
                {
                    protein = new Protein(DataSchema, proteinPath);
                    if (key.Depth == 0)
                    {
                        yield return protein;
                        continue;
                    }
                    proteins[protein.IdentityPath] = protein;
                }

                Entities.Peptide peptide;
                if (key.Depth == 1 || !peptides.TryGetValue(key.GetPathTo(1), out peptide))
                {
                    peptide = new Entities.Peptide(protein, key.GetIdentity(1));
                    if (key.Depth == 1)
                    {
                        yield return peptide;
                        continue;
                    }
                    peptides[peptide.IdentityPath] = peptide;
                }

                Precursor precursor;
                if (key.Depth == 2 || !precursors.TryGetValue(key.GetPathTo(2), out precursor))
                {
                    precursor = new Precursor(peptide, key.GetIdentity(2));
                    if (key.Depth == 2)
                    {
                        yield return precursor;
                        continue;
                    }

                    precursors[precursor.IdentityPath] = precursor;
                }

                if (key.Depth == 3)
                {
                    yield return new Entities.Transition(precursor, key.GetIdentity(3));
                }
            }
        }

        public override long? GetItemCount()
        {
            if (ROOT_IDENTITY_PATH.Equals(AncestorIdentityPaths))
            {
                return DataSchema.Document.GetCount(NodeDepth);
            }

            return base.GetItemCount();
        }
    }
}
