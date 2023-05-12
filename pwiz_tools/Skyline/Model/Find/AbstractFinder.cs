﻿/*
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

using pwiz.Common.SystemUtil;
using System.Collections.Generic;

namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Default implementation of the IFinder interface.
    /// </summary>
    public abstract class AbstractFinder : IFinder
    {
        public abstract string Name
        { 
            get;
        }
        public abstract string DisplayName
        { 
            get;
        }

        public abstract FindMatch Match(BookmarkEnumerator bookmarkEnumerator);
        public virtual FindMatch NextMatch(BookmarkStartPosition start, IProgressMonitor progressMonitor, ref IProgressStatus status)
        {
            long index = 0;
            long updateFrequency = start.GetProgressUpdateFrequency();
            var bookmarkEnumerator = new BookmarkEnumerator(start);
            do
            {
                if (progressMonitor.IsCanceled)
                {
                    return null;
                }
                bookmarkEnumerator.MoveNext();
                if (0 == index++ % updateFrequency)
                {
                    progressMonitor.UpdateProgress(status =
                        status.ChangePercentComplete(bookmarkEnumerator.GetProgressValue()));
                }
                var findMatch = Match(bookmarkEnumerator);
                if (findMatch != null)
                {
                    return findMatch;
                }
            } while (!bookmarkEnumerator.AtStart);
            return null;
        }

        public virtual IEnumerable<Bookmark> FindAll(SrmDocument document, IProgressMonitor progressMonitor, ref IProgressStatus status)
        {
            var results = new List<Bookmark>();
            var start = new BookmarkStartPosition(document);
            var bookmarkEnumerator = new BookmarkEnumerator(start);
            long index = 0;
            long updateFrequency = start.GetProgressUpdateFrequency();
            while (true)
            {
                if (progressMonitor.IsCanceled)
                {
                    return results;
                }
                bookmarkEnumerator.MoveNext();
                if (0 == index++ % updateFrequency)
                {
                    progressMonitor.UpdateProgress(status =
                        status.ChangePercentComplete(bookmarkEnumerator.GetProgressValue()));
                }

                if (Match(bookmarkEnumerator) != null)
                {
                    results.Add(bookmarkEnumerator.Current);
                }
                if (bookmarkEnumerator.AtStart)
                {
                    return results;
                }
            }
        }
    }
}
