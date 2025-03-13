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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Base implementation of an IFinder that applies to TransitionChromInfo's.
    /// If each of the Transitions in the TransitionGroup satisfies the find
    /// criteria, then only one FindResult for the TransitionGroup is displayed.
    /// </summary>
    public abstract class AbstractTransitionResultFinder : IFinder
    {
        public abstract string Name { get; }

        public abstract string DisplayName { get; }

        public FindMatch Match(BookmarkEnumerator bookmarkEnumerator)
        {
            var transitionGroupChromInfo = bookmarkEnumerator.CurrentChromInfo as TransitionGroupChromInfo;
            if (transitionGroupChromInfo != null)
            {
                return MatchTransitionGroup(bookmarkEnumerator.Current, transitionGroupChromInfo);
            }

            var transitionChromInfo = bookmarkEnumerator.CurrentChromInfo as TransitionChromInfo;
            if (transitionChromInfo != null)
            {
                return MatchTransition(bookmarkEnumerator.Current, transitionChromInfo);
            }

            return null;
        }

        protected abstract FindMatch MatchTransition(Bookmark bookmark, TransitionChromInfo transitionChromInfo);

        protected abstract FindMatch MatchTransitionGroup(Bookmark bookmark,
            TransitionGroupChromInfo transitionGroupChromInfo);

        protected virtual bool InspectTransitionGroupsOnly
        {
            get { return false; }
        }

        public FindMatch NextMatch(BookmarkStartPosition start, IProgressMonitor progressMonitor,
            ref IProgressStatus status)
        {
            var nextBookmark = FindAll(start.Document, progressMonitor, ref status).OrderBy(x => x, start)
                .FirstOrDefault();
            if (nextBookmark == null)
            {
                return null;
            }

            var bookmarkEnumerator = BookmarkEnumerator.TryGet(start.Document, nextBookmark);
            if (bookmarkEnumerator == null)
            {
                return null;
            }

            var findMatch = Match(bookmarkEnumerator);
            if (findMatch != null)
            {
                return findMatch;
            }

            return null;
        }

        public IEnumerable<Bookmark> FindAll(SrmDocument document, IProgressMonitor progressMonitor,
            ref IProgressStatus status)
        {
            var results = new List<Bookmark>();
            foreach (var moleculeGroup in document.MoleculeGroups)
            {
                foreach (var molecule in moleculeGroup.Molecules)
                {
                    var moleculeIdentityPath = new IdentityPath(moleculeGroup.Id, molecule.Id);
                    foreach (var transitionGroup in molecule.TransitionGroups)
                    {
                        if (progressMonitor.IsCanceled)
                        {
                            return results;
                        }
                        results.AddRange(FindAllInTransitionGroup(document, moleculeIdentityPath, transitionGroup));
                    }
                }
            }

            return results;
        }

        private IEnumerable<Bookmark> FindAllInTransitionGroup(SrmDocument document, IdentityPath peptideIdentityPath,
            TransitionGroupDocNode transitionGroup)
        {
            List<Bookmark> results = new List<Bookmark>();
            if (!transitionGroup.HasResults)
            {
                return results;
            }

            var transitionGroupId =
                new IdentityPath(peptideIdentityPath, transitionGroup.TransitionGroup);
            for (int iReplicate = 0; iReplicate < transitionGroup.Results.Count; iReplicate++)
            {
                foreach (var transitionGroupChromInfo in transitionGroup.GetSafeChromInfo(iReplicate))
                {
                    var transitionMatches = new List<Bookmark>();
                    bool allTransitionsMatch = true;
                    if (!InspectTransitionGroupsOnly)
                    {
                        foreach (var transitionDocNode in transitionGroup.Transitions)
                        {
                            if (!transitionDocNode.HasResults || iReplicate >= transitionDocNode.Results.Count)
                            {
                                continue;
                            }

                            var transitionResults = transitionDocNode.Results[iReplicate];
                            if (transitionResults.IsEmpty)
                            {
                                continue;
                            }
                            var transitionId = new IdentityPath(transitionGroupId, transitionDocNode.Id);
                            foreach (var chromInfo in transitionResults)
                            {
                                var transitionBookmark = new Bookmark(transitionId, iReplicate, chromInfo.FileId,
                                    chromInfo.OptimizationStep);
                                bool match = MatchTransition(transitionBookmark, chromInfo) != null;
                                if (match)
                                {
                                    transitionMatches.Add(transitionBookmark);
                                }
                                else
                                {
                                    allTransitionsMatch = false;
                                }
                            }
                        }
                    }

                    if (allTransitionsMatch)
                    {
                        var bookmark = new Bookmark(transitionGroupId, iReplicate, transitionGroupChromInfo.FileId, transitionGroupChromInfo.OptimizationStep);
                        var transitionGroupMatch = MatchTransitionGroup(bookmark, transitionGroupChromInfo);
                        if (transitionGroupMatch != null)
                        {
                            results.Add(bookmark);
                            continue;
                        }
                    }
                    results.AddRange(transitionMatches);
                }
            }

            return results;
        }
    }
}
