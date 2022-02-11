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
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Handles iterating through all possible locations in a Skyline Document.
    /// 
    /// </summary>
    public class BookmarkEnumerator : IEnumerator<Bookmark>, IEnumerable<Bookmark>
    {
        /// <summary>
        /// Constructor for a BookmarkEnumerator initially positioned 
        /// at a particular Bookmark.
        /// </summary>
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
        /// Move to the next (or previous if !Forward) location in the document.
        /// Wraps around if moving beyond the end or beginning of the document.
        /// </summary>
        public bool MoveNext()
        {
            if (Forward)
            {
                if (!MoveForward())
                {
                    return false;
                }
            }
            else
            {
                if (!MoveBackward())
                {
                    return false;
                }
            }

            return !Equals(Current, Start);
        }

        /// <summary>
        /// Move the enumerator forward to the next location in the document.
        /// DocNodes are enumerated in depth-first preorder (parent before children).
        /// Within DocNodes, the BookmarkEnumerator iterates over the ChromInfo's.
        /// </summary>
        bool MoveForward()
        {
            var current = Current;
            if (current == null)
            {
                return false;
            }
            var currentDocNode = Document.FindNode(current.IdentityPath);
            var currentFileIdOptStep = GetFileIdOptStep(current);
            for (int replicateIndex = ResultsIndex;
                 replicateIndex < (Document.Settings.MeasuredResults?.Chromatograms.Count ?? 0);
                 replicateIndex++)
            {
                foreach (var fileIdOpt in GetFileIdOptSteps(currentDocNode, replicateIndex).OrderBy(fo => fo))
                {
                    if (currentFileIdOptStep == null)
                    {
                        Current = current.ChangeResult(replicateIndex, fileIdOpt.ChromFileInfoId,
                            fileIdOpt.OptimizationStep);
                        return true;
                    }
                    else if (Equals(currentFileIdOptStep, fileIdOpt))
                    {
                        currentFileIdOptStep = null;
                    }
                }

                currentFileIdOptStep = null;
            }

            current = current.ClearResult();
            var identityPath = current.IdentityPath;
            var node = Document.FindNode(identityPath);
            if (node is DocNodeParent docNodeParent && docNodeParent.Children.Count > 0)
            {
                Current = current.ChangeIdentityPath(new IdentityPath(identityPath, docNodeParent.Children[0].Id));
                return true;
            }

            while (identityPath.Length > 0)
            {
                var parentIdentityPath = identityPath.Parent;
                docNodeParent = (DocNodeParent) Document.FindNode(parentIdentityPath);
                var childIndex = docNodeParent.FindNodeIndex(identityPath.Child);
                if (childIndex + 1 < docNodeParent.Children.Count)
                {
                    Current = current.ChangeIdentityPath(new IdentityPath(parentIdentityPath,
                        docNodeParent.Children[childIndex + 1].Id));
                    return true;
                }

                identityPath = identityPath.Parent;
            }
            Current = Bookmark.ROOT;
            return true;
        }

        bool MoveBackward()
        {
            var current = Current;
            if (current == null)
            {
                return false;
            }

            var currentDocNode = Document.FindNode(current.IdentityPath);
            var currentFileIdOptStep = GetFileIdOptStep(current);
            bool onResult = currentFileIdOptStep != null;
            for (int resultsIndex = ResultsIndex; resultsIndex >= 0; resultsIndex--)
            {
                foreach (var fileIdOpt in GetFileIdOptSteps(currentDocNode, resultsIndex).OrderByDescending(fo => fo))
                {
                    if (currentFileIdOptStep == null)
                    {
                        Current = current.ChangeResult(resultsIndex, fileIdOpt.ChromFileInfoId, fileIdOpt.OptimizationStep);
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
                Current = current.ClearResult();
                return true;
            }

            var identityPath = current.IdentityPath;
            if (IdentityPath.ROOT.Equals(identityPath))
            {
                Current = FindLastDescendentAndResult(IdentityPath.ROOT);
                return true;
            }
            while (identityPath.Length > 0)
            {
                var identityPathParent = identityPath.Parent;
                var docNodeParent = (DocNodeParent) Document.FindNode(identityPathParent);
                int childIndex = docNodeParent.FindNodeIndex(identityPath.Child);
                if (childIndex == 0)
                {
                    Current = FindLastResult(docNodeParent, identityPathParent);
                    return true;
                }
                Current = FindLastDescendentAndResult(new IdentityPath(identityPathParent,
                    docNodeParent.Children[childIndex - 1].Id));
                return true;
            }

            Current = Bookmark.ROOT;
            return true;
        }

        private Bookmark FindLastDescendentAndResult(IdentityPath identityPath)
        {
            var node = Document.FindNode(identityPath);
            while (node is DocNodeParent docNodeParent && docNodeParent.Children.Count > 0)
            {
                node = docNodeParent.Children[docNodeParent.Children.Count - 1];
                identityPath = new IdentityPath(identityPath, node.Id);
            }

            return FindLastResult(node, identityPath);
        }

        private Bookmark FindLastResult(DocNode node, IdentityPath identityPath)
        {
            for (int replicateIndex = GetReplicateCount(node) - 1; replicateIndex >= 0; replicateIndex--)
            {
                var fileIdOpt = GetFileIdOptSteps(node, replicateIndex).OrderByDescending(fo => fo).FirstOrDefault();
                if (fileIdOpt != null)
                {
                    return new Bookmark(identityPath, replicateIndex, fileIdOpt.ChromFileInfoId,
                        fileIdOpt.OptimizationStep);
                }
            }

            return new Bookmark(identityPath);
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

        public bool Forward { get; set; }

        public Bookmark Current
        {
            get; private set;
        }

        public ChromInfo CurrentChromInfo
        {
            get
            {
                var bookmark = Current;
                if (bookmark?.ChromFileInfoId == null)
                {
                    return null;
                }

                var docNode = Document.FindNode(bookmark.IdentityPath);
                if (docNode is TransitionDocNode transitionDocNode)
                {
                    return transitionDocNode.GetSafeChromInfo(ResultsIndex).FirstOrDefault(chromInfo =>
                        ReferenceEquals(chromInfo.FileId, bookmark.ChromFileInfoId) &&
                        chromInfo.OptimizationStep == bookmark.OptStep);
                }

                if (docNode is TransitionGroupDocNode transitionGroupDocNode)
                {
                    return transitionGroupDocNode.GetSafeChromInfo(ResultsIndex).FirstOrDefault(chromInfo =>
                        ReferenceEquals(chromInfo.FileId, bookmark.ChromFileInfoId) &&
                        chromInfo.OptimizationStep == bookmark.OptStep);
                }

                if (docNode is PeptideDocNode peptideDocNode)
                {
                    return peptideDocNode.GetSafeChromInfo(ResultsIndex).FirstOrDefault(chromInfo =>
                        ReferenceEquals(chromInfo.FileId, bookmark.ChromFileInfoId));
                }

                return null;
            }
        }

        public void Dispose()
        {
        }

        object IEnumerator.Current
        {
            get { return Current; }
        }
        public SrmDocument Document { get; private set; }
        public Bookmark Start { get; private set; }
        public ChromFileInfoId ChromFileInfoId
        {
            get
            {
                return Current?.ChromFileInfoId;
            }
        }
        public int OptStep
        {
            get
            {
                return Current?.OptStep ?? 0;
            }
        }
        public DocNode CurrentDocNode
        {
            get
            {
                return Document.FindNode(Current?.IdentityPath ?? IdentityPath.ROOT);
            }
        }

        public ChromatogramSet GetChromatogramSet(Bookmark bookmark)
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
            if (resultsIndex <= 0 || resultsIndex >= measuredResults.Chromatograms.Count)
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
                return Current?.ReplicateIndex ?? -1;
            }
        }

        public IdentityPath IdentityPath
        {
            get
            {
                return Current?.IdentityPath ?? IdentityPath.ROOT;
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
            return new BookmarkEnumerator(document, bookmark);
        }

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

            // if (docNode is SrmDocument document)
            // {
            //     return document.MeasuredResults?.Chromatograms.Count ?? 0;
            // }

            return 0;
        }

        private IEnumerable<FileIdOptStep> GetFileIdOptSteps(DocNode docNode, int replicateIndex)
        {
            var measuredResults = Document.Settings.MeasuredResults;
            if (measuredResults == null || replicateIndex < 0 || replicateIndex >= measuredResults.Chromatograms.Count)
            {
                return Array.Empty<FileIdOptStep>();
            }

            var chromatogramSet = measuredResults.Chromatograms[replicateIndex];
            if (docNode is TransitionDocNode transitionDocNode)
            {
                return transitionDocNode.GetSafeChromInfo(replicateIndex).Select(chromInfo =>
                    new FileIdOptStep(chromatogramSet, chromInfo.FileId, chromInfo.OptimizationStep));
            }

            if (docNode is TransitionGroupDocNode transitionGroupDocNode)
            {
                return transitionGroupDocNode.GetSafeChromInfo(replicateIndex).Select(chromInfo =>
                    new FileIdOptStep(chromatogramSet, chromInfo.FileId, chromInfo.OptimizationStep));
            }

            if (docNode is PeptideDocNode peptideDocNode)
            {
                return peptideDocNode.GetSafeChromInfo(replicateIndex).Select(chromInfo =>
                    new FileIdOptStep(chromatogramSet, chromInfo.FileId, 0));
            }

            return Array.Empty<FileIdOptStep>();
        }

        private class FileIdOptStep
        {
            public FileIdOptStep(ChromatogramSet chromatogramSet, ChromFileInfoId fileId,
                int optimizationStep)
            {
                ChromFileInfoId = fileId;
                FileIndex = chromatogramSet.FileCount == 1 ? 0 : chromatogramSet.IndexOfId(fileId);
                OptimizationStep = optimizationStep;
            }

            public ChromFileInfoId ChromFileInfoId { get; }
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
                return Equals((FileIdOptStep) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (FileIndex * 397) ^ OptimizationStep;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<Bookmark> GetEnumerator()
        {
            return (BookmarkEnumerator) MemberwiseClone();
        }
    }
}
