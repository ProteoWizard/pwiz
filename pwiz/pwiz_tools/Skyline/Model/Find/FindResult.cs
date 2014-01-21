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
namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Remembers a location in a document that text was searched for and found.
    /// </summary>
    public class FindResult
    {
        public FindResult(FindPredicate findPredicate, BookmarkEnumerator bookmarkEnumerator, FindMatch match)
        {
            FindPredicate = findPredicate;
            Bookmark = bookmarkEnumerator.Current;
            Document = bookmarkEnumerator.Document;
            LocationName = bookmarkEnumerator.GetLocationName(findPredicate.DisplaySettings);
            LocationType = bookmarkEnumerator.GetLocationType();
            FindMatch = match;
            IsValid = true;
        }

        public FindResult(FindResult findResult)
        {
            FindPredicate = findResult.FindPredicate;
            Bookmark = findResult.Bookmark;
            Document = findResult.Document;
            FindMatch = findResult.FindMatch;
            LocationName = findResult.LocationName;
            LocationType = findResult.LocationType;
            IsValid = findResult.IsValid;
        }

        public FindResult ChangeDocument(SrmDocument document)
        {
            var result = new FindResult(this) {Document = document};
            var bookMarkEnumerator = BookmarkEnumerator.TryGet(document, Bookmark);
            FindMatch findMatch = null;
            if (bookMarkEnumerator != null)
            {
                findMatch = FindPredicate.Match(bookMarkEnumerator);
            }
            if (findMatch == null)
            {
                result.IsValid = false;
            }
            else
            {
                result.IsValid = true;
                result.FindMatch = findMatch;
            }
            return result;
        }

        public FindPredicate FindPredicate { get; private set; }
        public SrmDocument Document { get; private set; }
        public Bookmark Bookmark { get; private set; }
        public FindMatch FindMatch { get; private set; }
        public bool IsValid { get; private set; }
        public string LocationName { get; private set; }
        public string LocationType { get; private set; }
#region object overrides
        public bool Equals(FindResult other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.FindPredicate, FindPredicate) && Equals(other.Document, Document) && Equals(other.Bookmark, Bookmark) && Equals(other.FindMatch, FindMatch) && other.IsValid.Equals(IsValid) && Equals(other.LocationName, LocationName) && Equals(other.LocationType, LocationType);
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
#endregion
    }
}
