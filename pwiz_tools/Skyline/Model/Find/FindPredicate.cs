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
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

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
                                            : keyValuePair.Key + @"=" + keyValuePair.Value;
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

        public FindResult FindNext(BookmarkEnumerator bookmarkEnumerator, IProgressMonitor progressMonitor)
        {
            int segmentCount = FindOptions.CustomFinders.Count;
            bool hasFindText = !string.IsNullOrEmpty(FindOptions.Text);
            if (hasFindText)
            {
                segmentCount++;
            }
            IProgressStatus progressStatus = new ProgressStatus().ChangeSegments(0, segmentCount);
            double closestPosition = double.MaxValue;
            Bookmark closestBookmark = null;
            FindResult closestResult = null;

            foreach (var finder in FindOptions.CustomFinders)
            {
                if (progressMonitor.IsCanceled)
                {
                    return null;
                }
                progressStatus = progressStatus.ChangeMessage(string.Format(Resources.FindPredicate_FindAll_Searching_for__0__, finder.DisplayName));
                progressMonitor.UpdateProgress(progressStatus);
                var customEnumerator = new BookmarkEnumerator(bookmarkEnumerator);
                var nextMatch = finder.NextMatch(customEnumerator, progressMonitor, ref progressStatus);
                progressStatus = progressStatus.NextSegment();
                if (nextMatch != null)
                {
                    var position = customEnumerator.GetProgressValue();
                    if (closestResult == null || position < closestPosition)
                    {
                        closestResult = new FindResult(this, customEnumerator, nextMatch);
                        closestPosition = position;
                        closestBookmark = customEnumerator.Current;
                    }
                }
            }

            if (hasFindText)
            {
                do
                {
                    if (progressMonitor.IsCanceled)
                    {
                        return null;
                    }

                    progressStatus =
                        progressStatus.ChangeMessage(Resources.FindPredicate_FindNext_Searching_for_next_result);
                    progressMonitor.UpdateProgress(progressStatus);
                    bookmarkEnumerator.MoveNext();
                    if (Equals(closestBookmark, bookmarkEnumerator.Current))
                    {
                        break;
                    }

                    FindMatch findMatch = MatchInternal(bookmarkEnumerator);
                    if (findMatch != null)
                    {
                        return new FindResult(this, bookmarkEnumerator, findMatch);
                    }
                } while (!bookmarkEnumerator.AtStart);
            }
            return closestResult;
        }

        public IEnumerable<FindResult> FindAll(IProgressMonitor progressMonitor, SrmDocument document)
        {
            int segmentCount = FindOptions.CustomFinders.Count;
            bool hasFindText = !string.IsNullOrEmpty(FindOptions.Text);
            if (hasFindText)
            {
                segmentCount++;
            }

            var progressStatus = new ProgressStatus().ChangeSegments(0, segmentCount);
            var results = new List<KeyValuePair<long, FindResult>>();
            for (int iFinder = 0; iFinder < FindOptions.CustomFinders.Count; iFinder++)
            {
                if (progressMonitor.IsCanceled)
                {
                    break;
                }
                var customFinder = FindOptions.CustomFinders[iFinder];
                progressStatus = progressStatus.ChangeMessage(string.Format(
                    Resources.FindPredicate_FindAll_Searching_for__0__,
                    customFinder.DisplayName));
                progressMonitor.UpdateProgress(progressStatus);

                foreach (var bookmark in customFinder.FindAll(document, progressMonitor, ref progressStatus))
                {
                    if (progressMonitor.IsCanceled)
                    {
                        break;
                    }

                    var bookmarkEnumeratorMatch = new BookmarkEnumerator(document, bookmark);
                    var findMatch = customFinder.Match(bookmarkEnumeratorMatch);
                    if (findMatch != null)
                    {
                        var findResult = new FindResult(this, bookmarkEnumeratorMatch, findMatch);
                        var position = bookmarkEnumeratorMatch.GetPositionAsLong();
                        results.Add(new KeyValuePair<long, FindResult>(position, findResult));
                    }
                    
                }

                progressStatus = progressStatus.NextSegment();
            }

            if (hasFindText)
            {
                progressStatus = progressStatus.ChangeMessage(GetProgressMessage(results.Count));
                var bookmarkEnumerator = new BookmarkEnumerator(document);
                do
                {
                    if (progressMonitor.IsCanceled)
                    {
                        break;
                    }
                    bookmarkEnumerator.MoveNext();
                    progressStatus = progressStatus.ChangePercentComplete((int) bookmarkEnumerator.GetProgressValue());
                    progressMonitor.UpdateProgress(progressStatus);
                    var findMatch = MatchInternal(bookmarkEnumerator);
                    if (findMatch != null)
                    {
                        long position = bookmarkEnumerator.GetPositionAsLong();
                        var findResult = new FindResult(this, bookmarkEnumerator, findMatch);
                        results.Add(new KeyValuePair<long, FindResult>(position, findResult));
                        progressStatus = progressStatus.ChangeMessage(GetProgressMessage(results.Count));
                    }
                } while (!bookmarkEnumerator.AtStart);
            }
            return results.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value);
        }

        private string GetProgressMessage(int matchCount)
        {
            if (matchCount == 0)
            {
                return Resources.FindPredicate_FindAll_Found_0_matches;
            }

            if (matchCount == 1)
            {
                return Resources.FindPredicate_FindAll_Found_1_match;
            }

            return string.Format(Resources.FindPredicate_FindAll_Found__0__matches, matchCount);
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
