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
using System.Linq;
using JetBrains.Annotations;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Handles iterating through all possible locations in a Skyline Document.
    /// 
    /// </summary>
    public class BookmarkEnumerator : IEnumerable<Bookmark>
    {
        /// <summary>
        /// Constructor for a BookmarkEnumerator initially positioned 
        /// at a particular Bookmark.
        /// </summary>
        public BookmarkEnumerator(SrmDocument document, Bookmark bookmark)
        {
            Document = document;
            Start = bookmark;
            Forward = true;
            IsValid = FindAndSetPosition(bookmark);
            Assume.IsNotNull(Current);
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
            CurrentDocNode = bookmarkEnumerator.CurrentDocNode;
            CurrentChromInfo = bookmarkEnumerator.CurrentChromInfo;
            IsValid = bookmarkEnumerator.IsValid;
        }
        public SrmDocument Document { get; private set; }
        [NotNull]
        public Bookmark Current
        {
            get; private set;
        }
        public bool Forward { get; set; }
        public DocNode CurrentDocNode
        {
            get; private set;
        }
        public ChromInfo CurrentChromInfo
        {
            get; private set;
        }
        public Bookmark Start { get; private set; }
        
        /// <summary>
        /// Move to the next (or previous if !Forward) location in the document.
        /// Wraps around if moving beyond the end or beginning of the document.
        /// </summary>
        public void MoveNext()
        {
            if (Forward)
            {
                MoveForward();
            }
            else
            {
                MoveBackward();
            }
        }

        /// <summary>
        /// Move the enumerator forward to the next location in the document.
        /// DocNodes are enumerated in depth-first preorder (parent before children).
        /// Within DocNodes, the BookmarkEnumerator iterates over the ChromInfo's.
        /// </summary>
        /// <returns>false if the current position of this enumerator cannot be found in the document</returns>
        // ReSharper disable once UnusedMethodReturnValue.Local
        private bool MoveForward()
        {
            var current = Current;
            var currentDocNode = CurrentDocNode;
            if (currentDocNode == null)
            {
                return false;
            }
            var currentFileIdOptStep = GetFileIdOptStep(current);
            for (int replicateIndex = ResultsIndex;
                 replicateIndex < (Document.Settings.MeasuredResults?.Chromatograms.Count ?? 0);
                 replicateIndex++)
            {
                foreach (var fileIdOpt in GetFileIdOptSteps(currentDocNode, replicateIndex))
                {
                    if (currentFileIdOptStep == null)
                    {
                        SetPosition(current.ChangeResult(replicateIndex, fileIdOpt.Item1.ChromFileInfoId,
                            fileIdOpt.Item1.OptimizationStep), currentDocNode, fileIdOpt.Item2);
                        return true;
                    }
                    else if (Equals(currentFileIdOptStep, fileIdOpt.Item1))
                    {
                        currentFileIdOptStep = null;
                    }
                }

                currentFileIdOptStep = null;
            }

            current = current.ClearResult();
            var identityPath = current.IdentityPath;
            if (currentDocNode is DocNodeParent docNodeParent && docNodeParent.Children.Count > 0)
            {
                var child = docNodeParent.Children[0];
                return SetPosition(current.ChangeIdentityPath(new IdentityPath(identityPath, child.Id)), child, null);
            }

            while (identityPath.Length > 0)
            {
                var parentIdentityPath = identityPath.Parent;
                docNodeParent = (DocNodeParent) Document.FindNode(parentIdentityPath);
                var childIndex = docNodeParent.FindNodeIndex(identityPath.Child);
                if (childIndex + 1 < docNodeParent.Children.Count)
                {
                    var child = docNodeParent.Children[childIndex + 1];
                    return SetPosition(current.ChangeIdentityPath(new IdentityPath(parentIdentityPath,
                        child.Id)), child, null);
                }

                identityPath = identityPath.Parent;
            }

            return SetPosition(Bookmark.ROOT, Document, null);
        }

        // ReSharper disable once UnusedMethodReturnValue.Local
        private bool MoveBackward()
        {
            var current = Current;
            var currentDocNode = CurrentDocNode;
            if (currentDocNode == null)
            {
                return false;
            }
            var currentFileIdOptStep = GetFileIdOptStep(current);
            bool onResult = currentFileIdOptStep != null;
            for (int resultsIndex = ResultsIndex; resultsIndex >= 0; resultsIndex--)
            {
                foreach (var fileIdOptTuple in GetFileIdOptSteps(currentDocNode, resultsIndex).Reverse())
                {
                    var fileIdOpt = fileIdOptTuple.Item1;
                    if (currentFileIdOptStep == null)
                    {
                        SetPosition(current.ChangeResult(resultsIndex, fileIdOpt.ChromFileInfoId, fileIdOpt.OptimizationStep), currentDocNode, fileIdOptTuple.Item2);
                        return true;
                    }
                    else if (Equals(currentFileIdOptStep, fileIdOpt))
                    {
                        currentFileIdOptStep = null;
                    }
                }
            }

            if (onResult)
            {
                SetPosition(current.ClearResult(), currentDocNode, null);
                return true;
            }

            var identityPath = current.IdentityPath;
            if (IdentityPath.ROOT.Equals(identityPath))
            {
                return MoveToLastDescendentAndResult(IdentityPath.ROOT, Document);
            }
            while (identityPath.Length > 0)
            {
                var identityPathParent = identityPath.Parent;
                var docNodeParent = (DocNodeParent) Document.FindNode(identityPathParent);
                int childIndex = docNodeParent.FindNodeIndex(identityPath.Child);
                if (childIndex == 0)
                {
                    return MoveToLastResult(docNodeParent, identityPathParent);
                }

                var child = docNodeParent.Children[childIndex - 1];
                return MoveToLastDescendentAndResult(new IdentityPath(identityPathParent, child.Id), child);
            }

            return SetPosition(Bookmark.ROOT, Document, null);
        }

        private bool MoveToLastDescendentAndResult(IdentityPath identityPath, DocNode node)
        {
            while (node is DocNodeParent docNodeParent && docNodeParent.Children.Count > 0)
            {
                node = docNodeParent.Children[docNodeParent.Children.Count - 1];
                identityPath = new IdentityPath(identityPath, node.Id);
            }

            return MoveToLastResult(node, identityPath);
        }

        private bool MoveToLastResult(DocNode node, IdentityPath identityPath)
        {
            for (int replicateIndex = GetReplicateCount(node) - 1; replicateIndex >= 0; replicateIndex--)
            {
                var fileIdOptTuple = GetFileIdOptSteps(node, replicateIndex).Last();
                if (fileIdOptTuple != null)
                {
                    var fileIdOpt = fileIdOptTuple.Item1;
                    SetPosition(new Bookmark(identityPath, replicateIndex, fileIdOpt.ChromFileInfoId, fileIdOpt.OptimizationStep), node, fileIdOptTuple.Item2);
                }
            }

            return SetPosition(new Bookmark(identityPath), node, null);
        }

        private bool SetPosition(Bookmark bookmark, DocNode docNode, ChromInfo chromInfo)
        {
            Current = bookmark;
            CurrentDocNode = docNode;
            CurrentChromInfo = chromInfo;
            return true;
        }

        private bool FindAndSetPosition(Bookmark bookmark)
        {
            var node = Document.FindNode(bookmark.IdentityPath);
            ChromInfo currentChromInfo = null;
            if (bookmark.ReplicateIndex.HasValue)
            {
                if (node is TransitionDocNode transitionDocNode)
                {
                    currentChromInfo = transitionDocNode.GetSafeChromInfo(bookmark.ReplicateIndex.Value).FirstOrDefault(chromInfo =>
                        ReferenceEquals(chromInfo.FileId, bookmark.ChromFileInfoId) &&
                        chromInfo.OptimizationStep == bookmark.OptStep);
                }
                else if (node is TransitionGroupDocNode transitionGroupDocNode)
                {
                    currentChromInfo = transitionGroupDocNode.GetSafeChromInfo(bookmark.ReplicateIndex.Value).FirstOrDefault(chromInfo =>
                        ReferenceEquals(chromInfo.FileId, bookmark.ChromFileInfoId) &&
                        chromInfo.OptimizationStep == bookmark.OptStep);
                }
                else if (node is PeptideDocNode peptideDocNode)
                {
                    currentChromInfo = peptideDocNode.GetSafeChromInfo(bookmark.ReplicateIndex.Value).FirstOrDefault(chromInfo =>
                        ReferenceEquals(chromInfo.FileId, bookmark.ChromFileInfoId));
                }
            }

            return SetPosition(bookmark, node, currentChromInfo);
        }

        public bool IsValid { get; private set; }

        public bool AtStart
        {
            get
            {
                if (!IsValid)
                {
                    return true;
                }
                if (Start.IsRoot)
                {
                    return Current.IsRoot;
                }
                return Equals(Start, Current);
            }
        }

        private ChromatogramSet GetChromatogramSet(Bookmark bookmark)
        {
            if (null == bookmark?.ReplicateIndex)
            {
                return null;
            }
            var measuredResults = Document.Settings.MeasuredResults;
            if (measuredResults == null)
            {
                return null;
            }
            
            int resultsIndex = bookmark.ReplicateIndex.Value;
            if (resultsIndex < 0 || resultsIndex >= measuredResults.Chromatograms.Count)
            {
                return null;
            }
            return measuredResults.Chromatograms[resultsIndex];
        }

        private FileIdOptStep GetFileIdOptStep(Bookmark bookmark)
        {
            if (bookmark?.ChromFileInfoId == null)
            {
                return null;
            }
            var chromatogramSet = GetChromatogramSet(bookmark);
            if (chromatogramSet == null)
            {
                return null;
            }

            return new FileIdOptStep(chromatogramSet, bookmark.ChromFileInfoId, bookmark.OptStep);
        }

        public int ResultsIndex
        {
            get
            {
                return Current.ReplicateIndex ?? -1;
            }
        }

        public IdentityPath IdentityPath
        {
            get
            {
                return Current.IdentityPath;
            }
        }
        IEnumerable<DocNode> NodePath
        {
            get
            {
                DocNode docNode = Document;
                var identityPath = IdentityPath;
                for (int i = 0; i < identityPath.Length; i++)
                {
                    docNode = ((DocNodeParent) docNode).FindNode(identityPath.GetIdentity(i));
                    if (docNode == null)
                    {
                        yield break;
                    }

                    yield return docNode;
                }
            }
        }

        public string GetLocationName(DisplaySettings displaySettings)
        {
            if (ResultsIndex >= 0)
            {
                int resultsIndex = ResultsIndex;
                var peptideDocNode = NodePath.OfType<PeptideDocNode>().FirstOrDefault();
                if (resultsIndex < 0)
                {
                    return Resources.BookmarkEnumerator_GetLocationName_UnknownFile;
                }
                if (peptideDocNode == null)
                {
                    return Resources.BookmarkEnumerator_GetLocationName_NoPeptide;
                }
                var chromatogramSets = Document.Settings.MeasuredResults.Chromatograms;
                var chromatogramSet = chromatogramSets[resultsIndex];
                var resultDisplaySettings = new DisplaySettings(displaySettings.NormalizedValueCalculator, 
                    peptideDocNode, false, resultsIndex, displaySettings.NormalizeOption);
                return CurrentDocNode.GetDisplayText(resultDisplaySettings) + @" (" + chromatogramSet.Name + @")";
            }
            return CurrentDocNode.GetDisplayText(displaySettings);
        }

        public string GetLocationType()
        {
            string nodeType = GetNodeTypeName(CurrentDocNode);
            if (ResultsIndex >= 0)
            {
                return nodeType + @" " + Resources.BookmarkEnumerator_GetLocationType_Results;
            }
            return nodeType;
        }

        private static string GetNodeTypeName(DocNode docNode)
        {
            if (docNode is TransitionDocNode)
            {
                return TransitionTreeNode.TITLE;
            }
            if (docNode is TransitionGroupDocNode)
            {
                return TransitionGroupTreeNode.TITLE;
            }
            if (docNode is PeptideDocNode)
            {
                return PeptideTreeNode.TITLE;
            }
            if (docNode is PeptideGroupDocNode)
            {
                return Resources.BookmarkEnumerator_GetNodeTypeName_Protein;
            }
            return Resources.BookmarkEnumerator_GetNodeTypeName_Unknown;
        }

        public static BookmarkEnumerator TryGet(SrmDocument document, Bookmark bookmark)
        {
            var bookmarkEnumerator = new BookmarkEnumerator(document, bookmark);
            if (bookmarkEnumerator.IsValid)
            {
                return bookmarkEnumerator;
            }
            return null;
        }

        /// <summary>
        /// Returns then number of replicates for the particular DocNode
        /// which might have results (i.e. ChromInfo's).
        /// Currently, only TransitionDocNode, TransitionGroupDocNode, and PeptideDocNode
        /// have results.
        /// One could imagine wanting Bookmark's to be able to represent ChromatogramSet's, but
        /// that is not supported yet.
        /// </summary>
        private int GetReplicateCount(DocNode docNode)
        {
            if (docNode is TransitionDocNode transitionDocNode)
            {
                return transitionDocNode.Results?.Count ?? 0;
            }

            if (docNode is TransitionGroupDocNode transitionGroupDocNode)
            {
                return transitionGroupDocNode.Results?.Count ?? 0;
            }

            if (docNode is PeptideDocNode peptideDocNode)
            {
                return peptideDocNode.Results?.Count ?? 0;
            }

            return 0;
        }

        private IEnumerable<Tuple<FileIdOptStep, ChromInfo>> GetFileIdOptSteps(DocNode docNode, int replicateIndex)
        {
            var measuredResults = Document.Settings.MeasuredResults;
            if (measuredResults == null || replicateIndex < 0 || replicateIndex >= measuredResults.Chromatograms.Count)
            {
                return Array.Empty<Tuple<FileIdOptStep, ChromInfo>>();
            }

            var chromatogramSet = measuredResults.Chromatograms[replicateIndex];
            if (docNode is TransitionDocNode transitionDocNode)
            {
                return MakeFileIdOptSteps(transitionDocNode.GetSafeChromInfo(replicateIndex),
                    chromInfo => new FileIdOptStep(chromatogramSet, chromInfo.FileId, chromInfo.OptimizationStep));
            }

            if (docNode is TransitionGroupDocNode transitionGroupDocNode)
            {
                return MakeFileIdOptSteps(transitionGroupDocNode.GetSafeChromInfo(replicateIndex),
                    chromInfo => new FileIdOptStep(chromatogramSet, chromInfo.FileId, chromInfo.OptimizationStep));
            }

            if (docNode is PeptideDocNode peptideDocNode)
            {
                return MakeFileIdOptSteps(peptideDocNode.GetSafeChromInfo(replicateIndex),
                    chromInfo => new FileIdOptStep(chromatogramSet, chromInfo.FileId, 0));
            }

            return Array.Empty<Tuple<FileIdOptStep, ChromInfo>>();
        }

        private IEnumerable<Tuple<FileIdOptStep, ChromInfo>> MakeFileIdOptSteps<T>(ChromInfoList<T> chromInfoList,
            Func<T, FileIdOptStep> converter) where T : ChromInfo
        {
            if (chromInfoList.Count == 0)
            {
                return Array.Empty<Tuple<FileIdOptStep, ChromInfo>>();
            }

            if (chromInfoList.Count == 1)
            {
                return new[] {Tuple.Create(converter(chromInfoList[0]), (ChromInfo) chromInfoList[0])};
            }

            return chromInfoList.Select(chromInfo=>Tuple.Create(converter(chromInfo), (ChromInfo) chromInfo)).OrderBy(tuple=>tuple.Item1);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<Bookmark> GetEnumerator()
        {
            var enumerator = (BookmarkEnumerator) MemberwiseClone();
            do
            {
                enumerator.MoveNext();
                yield return enumerator.Current;
            } while (!enumerator.AtStart);
        }
        private class FileIdOptStep : IComparable<FileIdOptStep>
        {
            public FileIdOptStep(ChromatogramSet chromatogramSet, ChromFileInfoId fileId, int optimizationStep)
            {
                ChromFileInfoId = fileId;
                FileIndex = chromatogramSet.FileCount == 1 ? 0 : chromatogramSet.IndexOfId(fileId);
                OptimizationStep = optimizationStep;
            }

            public ChromFileInfoId ChromFileInfoId { get; }
            /// <summary>
            /// The index of the ChromFileInfoId within the <see cref="ChromatogramSet.MSDataFileInfos"/>.
            /// </summary>
            public int FileIndex { get; }
            public int OptimizationStep { get; }

            protected bool Equals(FileIdOptStep other)
            {
                return FileIndex == other.FileIndex && OptimizationStep == other.OptimizationStep;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((FileIdOptStep)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (FileIndex * 397) ^ OptimizationStep;
                }
            }

            public int CompareTo(FileIdOptStep other)
            {
                int result = FileIndex.CompareTo(other.FileIndex);
                if (result == 0)
                {
                    result = OptimizationStep.CompareTo(other.OptimizationStep);
                }
                return result;
            }
        }
    }
}
