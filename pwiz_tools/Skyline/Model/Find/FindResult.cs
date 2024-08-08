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

using System.Collections.Generic;
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Util.Extensions;
using static pwiz.Skyline.Util.Helpers;

namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Remembers a location in a document that text was searched for and found.
    /// </summary>
    public class FindResult : Immutable
    {
        public FindResult(FindPredicate findPredicate, SrmDocument document, FindMatch match)
        {
            FindPredicate = findPredicate;
            Document = document;
            FindMatch = match;
            LocationName = GetLocationName();
            LocationType = GetLocationType();
            IsValid = true;
        }

        public FindResult ChangeDocument(SrmDocument document)
        {
            var bookMarkEnumerator = BookmarkEnumerator.TryGet(document, Bookmark);
            FindMatch findMatch = null;
            if (bookMarkEnumerator != null)
            {
                findMatch = FindPredicate.Match(bookMarkEnumerator);
            }
            if (findMatch == null)
            {
                return ChangeProp(ImClone(this), im => im.IsValid = false);
            }

            return new FindResult(FindPredicate, document, findMatch);
        }

        public FindPredicate FindPredicate { get; private set; }
        public SrmDocument Document { get; private set; }
        public Bookmark Bookmark
        {
            get { return FindMatch.Bookmark; }
        }
        public FindMatch FindMatch { get; private set; }
        public bool IsValid { get; private set; }
        public string LocationName { get; private set; }
        public string LocationType { get; private set; }
#region object overrides
        public bool Equals(FindResult other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.FindPredicate, FindPredicate) && Equals(other.Document, Document) &&
                   Equals(other.Bookmark, Bookmark) && Equals(other.FindMatch, FindMatch) &&
                   other.IsValid.Equals(IsValid) && Equals(other.LocationName, LocationName) &&
                   Equals(other.LocationType, LocationType);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (FindResult)) return false;
            return Equals((FindResult) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = FindPredicate.GetHashCode();
                result = (result*397) ^ Document.GetHashCode();
                result = (result*397) ^ Bookmark.GetHashCode();
                result = (result*397) ^ FindMatch.GetHashCode();
                result = (result*397) ^ IsValid.GetHashCode();
                result = (result*397) ^ LocationName.GetHashCode();
                result = (result*397) ^ LocationType.GetHashCode();
                return result;
            }
        }

        private static List<DocNode> GetNodePath(SrmDocument document, IdentityPath identityPath)
        {
            var nodePath = new List<DocNode> { document };
            for (int i = 0; i < identityPath.Length; i++)
            {
                var next = (nodePath[nodePath.Count - 1] as DocNodeParent)?.FindNode(identityPath.GetIdentity(i));
                if (next == null)
                {
                    return null;
                }
                nodePath.Add(next);
            }
            return nodePath;
        }

        private string GetLocationName()
        {
            var nodePath = GetNodePath(Document, Bookmark.IdentityPath);
            if (nodePath == null)
            {
                return null;
            }

            var currentDocNode = nodePath.Last();
            if (!Bookmark.ReplicateIndex.HasValue)
            {
                return currentDocNode.GetDisplayText(FindPredicate.DisplaySettings);
            }
            int resultsIndex = Bookmark.ReplicateIndex.Value;
            if (!Document.Settings.HasResults || resultsIndex < 0 || resultsIndex >= Document.Settings.MeasuredResults.Chromatograms.Count)
            {
                return null;
            }
            var peptideDocNode = nodePath.OfType<PeptideDocNode>().FirstOrDefault();
            if (peptideDocNode == null)
            {
                return null;
            }
            var chromatogramSets = Document.Settings.MeasuredResults.Chromatograms;
            var chromatogramSet = chromatogramSets[resultsIndex];
            var resultDisplaySettings = new DisplaySettings(FindPredicate.DisplaySettings.NormalizedValueCalculator,
                peptideDocNode, false, resultsIndex, FindPredicate.DisplaySettings.NormalizeOption);
            return currentDocNode.GetDisplayText(resultDisplaySettings) + @" (" + chromatogramSet.Name + @")";

        }
        #endregion

        /// <summary>
        /// Returns the string to display in the "Type" column of the Find Results window.
        /// </summary>
        public string GetLocationType()
        {
            string nodeType;
            switch (Bookmark.IdentityPath.Depth)
            {
                case (int)SrmDocument.Level.MoleculeGroups:
                    nodeType = FindResources.BookmarkEnumerator_GetNodeTypeName_Protein;
                    break;
                case (int)SrmDocument.Level.Molecules:
                    nodeType = PeptideTreeNode.TITLE;
                    break;
                case (int)SrmDocument.Level.TransitionGroups:
                    nodeType = TransitionGroupTreeNode.TITLE;
                    break;
                case (int)SrmDocument.Level.Transitions:
                    nodeType = TransitionTreeNode.TITLE;
                    break;
                default:
                    nodeType = FindResources.BookmarkEnumerator_GetNodeTypeName_Unknown;
                    break;
            }

            nodeType = PeptideToMoleculeTextMapper.Translate(nodeType, Document?.DocumentType ?? SrmDocument.DOCUMENT_TYPE.none); // Translate "peptide"=>"molecule" etc as needed

            if (Bookmark.ReplicateIndex.HasValue)
            {
                return TextUtil.SpaceSeparate(nodeType + @" " + FindResources.BookmarkEnumerator_GetLocationType_Results);
            }
            return nodeType;
        }
    }
}
