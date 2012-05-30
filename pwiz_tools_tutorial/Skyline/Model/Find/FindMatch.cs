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

namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Holds a snippet of text to be displayed, and a region to be
    /// highlighted.
    /// </summary>
    public class FindMatch
    {
        public FindMatch(String displayText)
        {
            DisplayText = displayText;
            RangeStart = 0;
            RangeEnd = displayText.Length;
        }
        public FindMatch(FindMatch findMatch)
        {
            DisplayText = findMatch.DisplayText;
            RangeStart = findMatch.RangeStart;
            RangeEnd = findMatch.RangeEnd;
        }
        public string DisplayText { get; private set; }
        public int RangeStart { get; private set; }
        public int RangeEnd { get; private set; }
        public int Length { get { return RangeEnd - RangeStart; } }
        public bool Note { get; private set; }
        public string AnnotationName { get; private set; }

        public FindMatch ChangeRange(int start, int end)
        {
            return new FindMatch(this) {RangeStart = start, RangeEnd = end};
        }
        public FindMatch ChangeAnnotationName(string annotationName)
        {
            return new FindMatch(this) {AnnotationName = annotationName};
        }
        public FindMatch ChangeNote(bool note)
        {
            return new FindMatch(this) {Note = note};
        }

        #region object overrides
        public bool Equals(FindMatch other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.DisplayText, DisplayText) 
                && other.RangeStart == RangeStart 
                && other.RangeEnd == RangeEnd
                && other.AnnotationName == AnnotationName
                && other.Note == Note;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (FindMatch)) return false;
            return Equals((FindMatch) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = DisplayText.GetHashCode();
                result = (result*397) ^ RangeStart;
                result = (result*397) ^ RangeEnd;
                result = (result*397) ^ Note.GetHashCode();
                result = (result*397) ^ (AnnotationName == null ? 0 : AnnotationName.GetHashCode());
                return result;
            }
        }
        #endregion
    }
}
