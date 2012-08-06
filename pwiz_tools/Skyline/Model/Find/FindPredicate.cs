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
using System.Collections.Generic;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Decides whether a particular location in the document matches what the user is searching for.
    /// The user can search for text and/or one or more custom finders.
    /// </summary>
    public class FindPredicate
    {
        public FindPredicate(FindOptions findOptions, DisplaySettings displaySettings)
        {
            FindOptions = findOptions;
            DisplaySettings = displaySettings;
            NormalizedFindText = findOptions.CaseSensitive ? findOptions.Text : findOptions.Text.ToLower();
        }

        public override string ToString()
        {
            return FindOptions.GetDescription();
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
                             ? normalizedText.IndexOf(NormalizedFindText, StringComparison.CurrentCulture)
                             : normalizedText.LastIndexOf(NormalizedFindText, StringComparison.CurrentCulture);
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
                string annotationText = keyValuePair.Key == keyValuePair.Value
                                            ? keyValuePair.Value
                                            : keyValuePair.Key + "=" + keyValuePair.Value; // Not L10N
                match = MatchText(annotationText);
                if (match != null)
                {
                    return match.ChangeAnnotationName(keyValuePair.Key);
                }
            }
            return null;
        }

        FindMatch MatchCustom(BookmarkEnumerator bookmarkEnumerator)
        {
            foreach (var finder in FindOptions.CustomFinders)
            {
                var findMatch = finder.Match(bookmarkEnumerator);
                if (findMatch != null)
                {
                    return findMatch;
                }
            }
            return null;
        }
        
        public FindMatch Match(BookmarkEnumerator bookmarkEnumerator)
        {
            return MatchCustom(bookmarkEnumerator) ?? MatchInternal(bookmarkEnumerator);
        }

        private FindMatch MatchInternal(BookmarkEnumerator bookmarkEnumerator)
        {
            if (string.IsNullOrEmpty(FindOptions.Text))
            {
                return null;
            }
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

        public FindResult FindNext(BookmarkEnumerator bookmarkEnumerator)
        {
            var customMatches = new Dictionary<Bookmark, FindMatch>();
            foreach (var finder in FindOptions.CustomFinders)
            {
                var customEnumerator = new BookmarkEnumerator(bookmarkEnumerator);
                var nextMatch = finder.NextMatch(customEnumerator);
                if (nextMatch == null || customMatches.ContainsKey(customEnumerator.Current))
                {
                    continue;
                }
                customMatches.Add(customEnumerator.Current, nextMatch);
            }
            do
            {
                bookmarkEnumerator.MoveNext();
                FindMatch findMatch;
                if (customMatches.TryGetValue(bookmarkEnumerator.Current, out findMatch))
                {
                    return new FindResult(this, bookmarkEnumerator, findMatch);
                }
                findMatch = MatchInternal(bookmarkEnumerator);
                if (findMatch != null)
                {
                    return new FindResult(this, bookmarkEnumerator, findMatch);
                }
            } while (!bookmarkEnumerator.AtStart);
            return null;
        }

        public IEnumerable<FindResult> FindAll(ILongWaitBroker longWaitBroker, SrmDocument document)
        {
            longWaitBroker.Message = Resources.FindPredicate_FindAll_Found_0_matches;
            var customMatches = new HashSet<Bookmark>[FindOptions.CustomFinders.Count];
            for (int iFinder = 0; iFinder < FindOptions.CustomFinders.Count; iFinder++)
            {
                var customFinder = FindOptions.CustomFinders[iFinder];
                var bookmarkSet = new HashSet<Bookmark>();
                longWaitBroker.Message = string.Format(Resources.FindPredicate_FindAll_Searching_for__0__, customFinder.DisplayName);
                foreach (var bookmark in customFinder.FindAll(document))
                {
                    if (longWaitBroker.IsCanceled)
                    {
                        yield break;
                    }
                    bookmarkSet.Add(bookmark);
                }
                customMatches[iFinder] = bookmarkSet;
            }
            var bookmarkEnumerator = new BookmarkEnumerator(document);
            int matchCount = 0;
            do
            {
                bookmarkEnumerator.MoveNext();
                if (longWaitBroker.IsCanceled)
                {
                    yield break;
                }
                FindMatch findMatch = null;
                for (int iFinder = 0; iFinder < FindOptions.CustomFinders.Count; iFinder++)
                {
                    if (customMatches[iFinder].Contains(bookmarkEnumerator.Current))
                    {
                        findMatch = FindOptions.CustomFinders[iFinder].Match(bookmarkEnumerator);
                    }
                }
                findMatch = findMatch ?? MatchInternal(bookmarkEnumerator);
                if (findMatch != null)
                {
                    matchCount++;
                    longWaitBroker.Message = matchCount == 1
                                                 ? Resources.FindPredicate_FindAll_Found_1_match
                                                 : string.Format(Resources.FindPredicate_FindAll_Found__0__matches, matchCount);
                    yield return new FindResult(this, bookmarkEnumerator, findMatch);
                }
            } while (!bookmarkEnumerator.AtStart);
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
