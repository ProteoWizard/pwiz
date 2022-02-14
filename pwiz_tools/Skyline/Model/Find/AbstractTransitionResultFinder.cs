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
using System.Runtime.CompilerServices;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Base implementation of an IFinder that applies to TransitionChromInfo's.
    /// If each of the Transitions in the TransitionGroup satifies the find
    /// criteria, then only one FindResult for the TransitionGroup is displayed.
    /// </summary>
    public abstract class AbstractTransitionResultFinder : IFinder
    {
        public abstract string Name { get;}

        public abstract string DisplayName {get;}

        public FindMatch Match(BookmarkEnumerator bookmarkEnumerator)
        {
            var transitionGroupChromInfo = bookmarkEnumerator.CurrentChromInfo as TransitionGroupChromInfo;
            if (transitionGroupChromInfo != null)
            {
                return MatchTransitionGroup(transitionGroupChromInfo);
            }
            var transitionChromInfo = bookmarkEnumerator.CurrentChromInfo as TransitionChromInfo;
            if (transitionChromInfo != null)
            {
                return MatchTransition(transitionChromInfo);
            }
            return null;
        }

        protected abstract FindMatch MatchTransition(TransitionChromInfo transitionChromInfo);
        protected abstract FindMatch MatchTransitionGroup(TransitionGroupChromInfo transitionGroupChromInfo);

        public FindMatch NextMatch(BookmarkEnumerator bookmarkEnumerator)
        {
            var allBookmarks = new HashSet<Bookmark>(FindAll(bookmarkEnumerator.Document));
            if (allBookmarks.Count == 0)
            {
                return null;
            }
            do
            {
                bookmarkEnumerator.MoveNext();
                if (allBookmarks.Contains(bookmarkEnumerator.Current))
                {
                    var findMatch = Match(bookmarkEnumerator);
                    if (findMatch != null)
                    {
                        return findMatch;
                    }
                }
            } while (!bookmarkEnumerator.AtStart);
            return null;
        }

        public IEnumerable<Bookmark> FindAll(SrmDocument document)
        {
            return FindAll(IdentityPath.ROOT, document);
        }
        private IEnumerable<Bookmark> FindAll(IdentityPath identityPath, DocNode docNode)
        {
            var results = new List<Bookmark>();
            var transitionGroupDocNode = docNode as TransitionGroupDocNode;
            if (transitionGroupDocNode == null)
            {
                var docNodeParent = docNode as DocNodeParent;
                if (docNodeParent == null)
                {
                    return results;
                }
                foreach (var child in docNodeParent.Children)
                {
                    results.AddRange(FindAll(new IdentityPath(identityPath, child.Id), child));
                }
                return results;
            }
            if (!transitionGroupDocNode.HasResults)
            {
                return results;
            }
            for (int iReplicate = 0; iReplicate < transitionGroupDocNode.Results.Count; iReplicate++)
            {
                var replicate = transitionGroupDocNode.Results[iReplicate];
                if (replicate.IsEmpty)
                {
                    continue;
                }
                var fileDatas = new Dictionary<FinderChromFileKey, FinderChromFileData>();
                foreach (var transitionGroupChromInfo in replicate)
                {
                    FinderChromFileData chromFileData;
                    var chromFileKey = new FinderChromFileKey(transitionGroupChromInfo);
                    if (!fileDatas.TryGetValue(chromFileKey, out chromFileData))
                    {
                        chromFileData = new FinderChromFileData
                                            {
                                                TransitionGroupChromInfo = transitionGroupChromInfo,
                                                MatchingTransitionBookmarks = new List<Bookmark>(),
                                                AllTransitionsMatch = true,
                                            };
                        fileDatas.Add(chromFileKey, chromFileData);
                    }
                }
                foreach (var transitionDocNode in transitionGroupDocNode.Transitions)
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
                    var transitionId = new IdentityPath(identityPath, transitionDocNode.Id);
                    foreach (var chromInfo in transitionResults)
                    {
                        FinderChromFileData chromFileData;
                        bool match = MatchTransition(chromInfo) != null;
                        var transitionBookmark = new Bookmark(transitionId, iReplicate, chromInfo.FileId,
                                                              chromInfo.OptimizationStep);
                        var chromFileKey = new FinderChromFileKey(chromInfo);
                        if (!fileDatas.TryGetValue(chromFileKey, out chromFileData))
                        {
                            if (match)
                            {
                                results.Add(transitionBookmark);
                            }
                        }
                        else
                        {
                            if (match)
                            {
                                chromFileData.MatchingTransitionBookmarks.Add(transitionBookmark);
                            }
                            else
                            {
                                chromFileData.AllTransitionsMatch = false;
                            }
                        }
                    }
                }
                foreach (var fileDataEntry in fileDatas)
                {
                    if (fileDataEntry.Value.AllTransitionsMatch && fileDataEntry.Value.MatchingTransitionBookmarks.Count > 0)
                    {
                        var transitionGroupMatch = MatchTransitionGroup(fileDataEntry.Value.TransitionGroupChromInfo);
                        if (transitionGroupMatch != null)
                        {
                            results.Add(new Bookmark(identityPath, iReplicate, fileDataEntry.Value.TransitionGroupChromInfo.FileId, fileDataEntry.Value.TransitionGroupChromInfo.OptimizationStep));
                            continue;
                        }
                    }
                    results.AddRange(fileDataEntry.Value.MatchingTransitionBookmarks);
                }
            }
            return results;
        }
        class FinderChromFileData
        {
            public TransitionGroupChromInfo TransitionGroupChromInfo { get; set;}
            public List<Bookmark> MatchingTransitionBookmarks { get; set;}
            public bool AllTransitionsMatch { get; set;}
        }
        class FinderChromFileKey
        {
            public FinderChromFileKey(TransitionChromInfo transitionChromInfo)
            {
                ChromFileInfoId = transitionChromInfo.FileId;
                OptStep = transitionChromInfo.OptimizationStep;
            }
            public FinderChromFileKey(TransitionGroupChromInfo transitionGroupChromInfo)
            {
                ChromFileInfoId = transitionGroupChromInfo.FileId;
                OptStep = transitionGroupChromInfo.OptimizationStep;
            }

            private ChromFileInfoId ChromFileInfoId { get; set; }
            private int OptStep { get; set; }

            public override int GetHashCode()
            {
                return RuntimeHelpers.GetHashCode(ChromFileInfoId)*397 ^ OptStep.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                var that = obj as FinderChromFileKey;
                if (this == that)
                {
                    return true;
                }
                if (null == that)
                {
                    return false;
                }
                return ReferenceEquals(ChromFileInfoId, that.ChromFileInfoId) 
                    && Equals(OptStep, that.OptStep);
            }
        }
    }
}
