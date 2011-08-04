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
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.Find
{
    public class FindPredicate
    {
        public FindPredicate(FindOptions findOptions, DisplaySettings displaySettings)
        {
            FindOptions = findOptions;
            DisplaySettings = displaySettings;
            NormalizedFindText = findOptions.CaseSensitive ? findOptions.Text : findOptions.Text.ToLower();
        }

        public DisplaySettings DisplaySettings { get; private set; }
        public FindOptions FindOptions { get; private set; }
        string NormalizedFindText { get; set; }
        FindMatch MatchText(string text)
        {
            if (text == null)
            {
                return null;
            }
            string normalizedText = FindOptions.CaseSensitive ? text : text.ToLower();
            int offset = FindOptions.Forward
                             ? normalizedText.IndexOf(NormalizedFindText)
                             : normalizedText.LastIndexOf(NormalizedFindText);
            if (offset < 0)
            {
                return null;
            }
            return new FindMatch(text).ChangeRange(offset, offset + FindOptions.Text.Length);
        }
        FindMatch MatchAnnotations(Annotations annotations)
        {
            if (annotations == null)
            {
                return null;
            }
            var match = MatchText(annotations.Note);
            if (match != null)
            {
                return match.ChangeNote(true);
            }
            foreach (var keyValuePair in annotations.ListAnnotations())
            {
                match = MatchText(keyValuePair.Value);
                if (match != null)
                {
                    return match.ChangeAnnotationName(keyValuePair.Key);
                }
            }
            return null;
        }
        
        public FindMatch Match(BookmarkEnumerator bookmarkEnumerator)
        {
            var chromInfo = bookmarkEnumerator.CurrentChromInfo;
            if (chromInfo != null)
            {
                return MatchAnnotations(GetAnnotations(chromInfo));
            }
            var docNode = bookmarkEnumerator.CurrentDocNode;
            if (docNode == null)
            {
                return null;
            }
            return MatchText(docNode.GetDisplayText(DisplaySettings))
                ?? MatchAnnotations(docNode.Annotations);
        }

        static Annotations GetAnnotations(ChromInfo chromInfo)
        {
            var transitionGroupChromInfo = chromInfo as TransitionGroupChromInfo;
            if (transitionGroupChromInfo != null)
            {
                return transitionGroupChromInfo.Annotations;
            }
            var transitionChromInfo = chromInfo as TransitionChromInfo;
            if (transitionChromInfo != null)
            {
                return transitionChromInfo.Annotations;
            }
            return null;
        }
    }
}
