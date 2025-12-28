/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Threading;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class ElementReorderer
    {
        private ILookup<IdentityPath, IdentityPath> _pathsByParent;
        private HashSet<IdentityPath> _allAncestors;
        private IList<string> _replicateOrder;
        
        public ElementReorderer(CancellationToken cancellationToken, SrmDocument document)
        {
            CancellationToken = cancellationToken;
            Document = document;
        }
        
        public CancellationToken CancellationToken { get; }

        public SrmDocument Document { get; }

        public SrmDocument SetNewOrder(IEnumerable<ElementRef> newOrdering)
        {
            var identityPaths = new List<IdentityPath>();
            var replicateNames = new List<string>();
            foreach (var elementRef in newOrdering)
            {
                if (elementRef is NodeRef nodeRef)
                {
                    try
                    {
                        identityPaths.Add(nodeRef.ToIdentityPath(Document));
                    }
                    catch (IdentityNotFoundException)
                    {
                        // ignore
                    }
                }

                if (elementRef is ReplicateRef replicateRef)
                {
                    replicateNames.Add(replicateRef.Name);
                }
            }

            _replicateOrder = replicateNames.Distinct().ToList();

            _pathsByParent = identityPaths.Distinct().ToLookup(path => path.Parent);
            _allAncestors = new HashSet<IdentityPath>();
            foreach (var grouping in _pathsByParent)
            {
                for (var ancestor = grouping.Key; ancestor != null && !ancestor.IsRoot; ancestor = ancestor.Parent)
                {
                    if (!_allAncestors.Add(ancestor))
                    {
                        break;
                    }
                }
            }

            return ReorderDocument();
        }

        private SrmDocument ReorderDocument()
        {
            var newDocument = (SrmDocument) ReorderChildren(IdentityPath.ROOT, Document);
            newDocument = ReorderReplicates(newDocument);
            return newDocument;
        }

        private DocNodeParent ReorderChildren(IdentityPath parentIdentityPath, DocNodeParent docNodeParent)
        {
            CancellationToken.ThrowIfCancellationRequested();
            var newOrder = new Dictionary<IdentityPath, int>();
            foreach (var identityPath in _pathsByParent[parentIdentityPath])
            {
                newOrder.Add(identityPath, newOrder.Count);
            }

            var orderedChildren = new DocNode[newOrder.Count];
            var unorderedChildren = new List<DocNode>();
            foreach (var child in docNodeParent.Children)
            {
                var newChild = child;
                var identityPath = new IdentityPath(parentIdentityPath, child.Id);
                if (newChild is DocNodeParent childParent && _allAncestors.Contains(identityPath))
                {
                    newChild = ReorderChildren(identityPath, childParent);
                }
                if (newOrder.TryGetValue(identityPath, out var orderIndex))
                {
                    orderedChildren[orderIndex] = newChild;
                }
                else
                {
                    unorderedChildren.Add(newChild);
                }
            }

            var newChildren = orderedChildren.Concat(unorderedChildren).Where(child => null != child).ToList();
            if (ArrayUtil.ReferencesEqual(docNodeParent.Children, newChildren))
            {
                return docNodeParent;
            }
            return docNodeParent.ChangeChildren(newChildren);
        }

        private SrmDocument ReorderReplicates(SrmDocument document)
        {
            if (document.MeasuredResults == null || _replicateOrder.Count == 0)
            {
                return document;
            }

            var newOrder = new Dictionary<string, int>();
            foreach (var replicateName in _replicateOrder)
            {
                newOrder.Add(replicateName, newOrder.Count);
            }

            var orderedReplicates = new ChromatogramSet[newOrder.Count];
            var unorderedReplicates = new List<ChromatogramSet>();
            foreach (var chromatogramSet in document.MeasuredResults.Chromatograms)
            {
                if (newOrder.TryGetValue(chromatogramSet.Name, out var orderIndex))
                {
                    orderedReplicates[orderIndex] = chromatogramSet;
                }
                else
                {
                    unorderedReplicates.Add(chromatogramSet);
                }
            }

            var newChromatogramSets =
                orderedReplicates.Concat(unorderedReplicates).Where(chrom => null != chrom).ToList();
            if (ArrayUtil.ReferencesEqual(document.MeasuredResults.Chromatograms, newChromatogramSets))
            {
                return document;
            }

            var newMeasuredResults = document.Settings.MeasuredResults.ChangeChromatograms(newChromatogramSets);
            using var changeMonitor = new SrmSettingsChangeMonitor(new SilentProgressMonitor(CancellationToken), string.Empty);
            return document.ChangeMeasuredResults(newMeasuredResults);
        }
    }
}
