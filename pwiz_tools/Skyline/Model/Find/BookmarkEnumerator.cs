/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Handles iterating through all possible locations in a Skyline Document.
    /// 
    /// </summary>
    public class BookmarkEnumerator : IEnumerator<Bookmark>, IEnumerable<Bookmark>
    {
        readonly List<int> _nodeIndexPath = new List<int>();
        readonly List<ChromInfo> _chromInfos = new List<ChromInfo>();
        int _chromInfoIndex;

        /// <summary>
        /// Constructor for a BookmarkEnumerator initially positioned 
        /// at a particular Bookmark.
        /// </summary>
        /// <exception cref="InvalidBookmarkLocation">If the bookmark
        /// location does not exist in the document.</exception>
        public BookmarkEnumerator(SrmDocument document, Bookmark bookmark)
        {
            Document = document;
            Start = bookmark;
            Current = bookmark;
            Forward = true;
        }
        public BookmarkEnumerator(SrmDocument document)
            : this(document, Bookmark.ROOT)
        {
        }
        public BookmarkEnumerator(BookmarkEnumerator bookmarkEnumerator)
        {
            Document = bookmarkEnumerator.Document;
            Start = bookmarkEnumerator.Start;
            Current = bookmarkEnumerator.Current;
            Forward = bookmarkEnumerator.Forward;
        }

        /// <summary>
        /// Called after the BookmarkEnumerator has moved to a different DocNode. 
        /// If enumerating nodes in the document forwards, the BookmarkEnumerator will
        /// be positioned before the first ChromInfo.
        /// If enumerating nodes backwards, the BookmarkEnumerator will be positioned
        /// on the last ChromInfo (if any).
        /// </summary>
        void MovedToNewDocNode(bool forward)
        {
            var docNode = CurrentDocNode;
            _chromInfos.Clear();
            _chromInfos.AddRange(ListChromInfos(docNode));
            if (forward)
            {
                _chromInfoIndex = -1;
            }
            else
            {
                _chromInfoIndex = _chromInfos.Count - 1;
            }
        }

        public virtual void Dispose()
        {
        }

        /// <summary>
        /// Move to the next (or previous if !Forward) location in the document.
        /// Wraps around if moving beyond the end or beginning of the document.
        /// </summary>
        /// <returns>Returns true unless the new position is the Start</returns>
        public bool MoveNext()
        {
            if (Forward)
            {
                MoveForward();
            }
            else
            {
                MoveBackward();
            }
            return !AtStart;
        }

        /// <summary>
        /// Move the enumerator forward to the next location in the document.
        /// DocNodes are enumerated in depth-first preorder (parent before children).
        /// Within DocNodes, the BookmarkEnumerator iterates over the ChromInfo's.
        /// </summary>
        void MoveForward()
        {
            if (_chromInfoIndex + 1 < _chromInfos.Count())
            {
                _chromInfoIndex++;
                return;
            }
            _chromInfoIndex = -1;
            var docNodeParent = CurrentDocNode as DocNodeParent;
            if (docNodeParent != null && docNodeParent.Children.Count > 0)
            {
                _nodeIndexPath.Add(0);
            }
            else
            {
                while (_nodeIndexPath.Count > 0)
                {
                    int index = _nodeIndexPath[_nodeIndexPath.Count - 1];
                    _nodeIndexPath.RemoveAt(_nodeIndexPath.Count - 1);
                    if (index + 1 < ((DocNodeParent)CurrentDocNode).Children.Count)
                    {
                        _nodeIndexPath.Add(index + 1);
                        break;
                    }
                }
            }
            MovedToNewDocNode(true);
            return;
        }

        void MoveBackward()
        {
            if (_chromInfoIndex >= 0)
            {
                _chromInfoIndex--;
                return;
            }
            if (_nodeIndexPath.Count > 0)
            {
                int index = _nodeIndexPath[_nodeIndexPath.Count - 1];
                if (index == 0)
                {
                    // We are visiting the parent
                    _nodeIndexPath.RemoveAt(_nodeIndexPath.Count - 1);
                    MovedToNewDocNode(false);
                    return;
                }
                _nodeIndexPath[_nodeIndexPath.Count - 1] = index - 1;
            }
            // We are now going to drill down into the very last descendant of the node we are now on
            var docNode = CurrentDocNode;
            while (docNode is DocNodeParent)
            {
                var docNodeParent = (DocNodeParent)CurrentDocNode;
                int childCount = docNodeParent.Children.Count();
                if (childCount == 0)
                {
                    break;
                }
                _nodeIndexPath.Add(childCount - 1);
                docNode = docNodeParent.Children[childCount - 1];
                Debug.Assert(docNode == CurrentDocNode);
            }
            MovedToNewDocNode(false);
        }

        /// <summary>
        /// Returns true if the BookmarkEnumerator is positioned at its
        /// starting location.
        /// </summary>
        public bool AtStart { get { return Equals(Start, Current); } }

        public void Reset()
        {
            Current = Start;
        }

        object IEnumerator.Current
        {
            get { return Current; }
        }

        public bool Forward { get; set; }

        public Bookmark Current
        {
            get
            {
                var chromInfo = CurrentChromInfo;
                if (chromInfo == null)
                {
                    return new Bookmark(IdentityPath);
                }
                return new Bookmark(IdentityPath, chromInfo.FileId, GetOptStep(chromInfo));
            }
            set
            {
                _nodeIndexPath.Clear();
                DocNode docNode = Document;
                for (int i = 0; i < value.IdentityPath.Length; i++)
                {
                    var docNodeParent = (DocNodeParent)docNode;
                    int index = docNodeParent.FindNodeIndex(value.IdentityPath.GetIdentity(i));
                    if (index < 0)
                    {
                        throw new InvalidBookmarkLocation("No such node:" + value.IdentityPath.GetPathTo(i));
                    }
                    _nodeIndexPath.Add(index);
                    docNode = docNodeParent.Children[index];
                }
                MovedToNewDocNode(true);
                if (value.ChromFileInfoId != null)
                {
                    _chromInfoIndex = ArrayUtil.IndexOf(_chromInfos, ci => ReferenceEquals(value.ChromFileInfoId, ci.FileId) && value.OptStep == GetOptStep(ci));
                    if (_chromInfoIndex < 0)
                    {
                        throw new InvalidBookmarkLocation("No such chrominfo:" + value.ChromFileInfoId);
                    }
                }
            }
        }
        public SrmDocument Document { get; private set; }
        public Bookmark Start { get; private set; }
        public IdentityPath IdentityPath
        {
            get
            {
                return new IdentityPath(NodePath.Select(n => n.Id));
            }
        }
        public ChromInfo CurrentChromInfo
        {
            get
            {
                return _chromInfoIndex == -1 ? null : _chromInfos[_chromInfoIndex];
            }
        }

        public ChromFileInfoId ChromFileInfoId
        {
            get
            {
                var chromInfo = CurrentChromInfo;
                return chromInfo == null ? null : chromInfo.FileId;
            }
        }
        public int OptStep
        {
            get
            {
                return GetOptStep(CurrentChromInfo);
            }
        }
        public DocNode CurrentDocNode
        {
            get
            {
                DocNode result = Document;
                foreach (DocNode docNode in NodePath)
                {
                    result = docNode;
                }
                return result;
            }
        }
        IEnumerable<DocNode> NodePath
        {
            get
            {
                DocNode docNode = Document;
                foreach (var index in _nodeIndexPath)
                {
                    docNode = ((DocNodeParent)docNode).Children[index];
                    yield return docNode;
                }
            }
        }

        static IEnumerable<ChromInfo> ListChromInfos(DocNode docNode)
        {
            var peptideDocNode = docNode as PeptideDocNode;
            if (peptideDocNode != null)
            {
                return ListChromInfos(peptideDocNode.Results);
            }
            var transitionGroupDocNode = docNode as TransitionGroupDocNode;
            if (transitionGroupDocNode != null)
            {
                return ListChromInfos(transitionGroupDocNode.Results);
            }
            var transitionDocNode = docNode as TransitionDocNode;
            if (transitionDocNode != null)
            {
                return ListChromInfos(transitionDocNode.Results);
            }
            return new ChromInfo[0];
        }
        static IEnumerable<ChromInfo> ListChromInfos<TChromInfo>(Results<TChromInfo> results) where TChromInfo : ChromInfo
        {
            if (results == null)
            {
                return new ChromInfo[0];
            }
            var list = new List<ChromInfo>();
            foreach (var chromInfoList in results)
            {
                if (chromInfoList == null)
                {
                    continue;
                }
                foreach (var chromInfo in chromInfoList)
                {
                    list.Add(chromInfo);
                }
            }
            return list;
        }
        static int GetOptStep(ChromInfo chromInfo)
        {
            var transitionChromInfo = chromInfo as TransitionChromInfo;
            if (transitionChromInfo != null)
            {
                return transitionChromInfo.OptimizationStep;
            }
            var transitionGroupChromInfo = chromInfo as TransitionGroupChromInfo;
            if (transitionGroupChromInfo != null)
            {
                return transitionGroupChromInfo.OptimizationStep;
            }
            return 0;
        }
        public IEnumerator<Bookmark> GetEnumerator()
        {
            return new BookmarkEnumerator(this);
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public string GetLocationName(DisplaySettings displaySettings)
        {
            if (_chromInfoIndex > 0)
            {
                var chromInfo = _chromInfos[_chromInfoIndex];
                // TODO(nicksh): Is this the right way to be searching for the name of a chromInfo?
                foreach (var chromatogramSet in Document.Settings.MeasuredResults.Chromatograms)
                {
                    var foundMsDataFileInfo = chromatogramSet.MSDataFileInfos.FirstOrDefault(
                        msDataFileInfo => chromInfo.FileId == msDataFileInfo.FileId);
                    if (foundMsDataFileInfo != null)
                    {
                        return CurrentDocNode.GetDisplayText(displaySettings) + " (" + chromatogramSet.Name + ")";
                    }
                }
                return "<UnknownFile>";
            }
            return CurrentDocNode.GetDisplayText(displaySettings);
        }

        public string GetLocationType()
        {
            string nodeType = GetNodeTypeName(CurrentDocNode);
            if (_chromInfoIndex >= 0)
            {
                return nodeType + " Results";
            }
            return nodeType;
        }

        private static string GetNodeTypeName(DocNode docNode)
        {
            if (docNode is TransitionDocNode)
            {
                return "Transition";
            }
            if (docNode is TransitionGroupDocNode)
            {
                return "Transition Group";
            }
            if (docNode is PeptideDocNode)
            {
                return "Peptide";
            }
            if (docNode is PeptideGroupDocNode)
            {
                return "Protein";
            }
            return "UnknownNode";
        }

        public static BookmarkEnumerator TryGet(SrmDocument document, Bookmark bookmark)
        {
            try
            {
                return new BookmarkEnumerator(document, bookmark);
            }
            catch (InvalidBookmarkLocation)
            {
                return null;
            }
        }

        private class InvalidBookmarkLocation : ApplicationException
        {
            public InvalidBookmarkLocation(string message) : base(message)
            {
            }
        }
    }
}
