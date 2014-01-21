/*
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
using System.Collections.Generic;

namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Interface for a custom finder.
    /// Custom finders are displayed in a CheckedListBox in the 
    /// <see cref="pwiz.Skyline.EditUI.FindNodeDlg" />.
    /// </summary>
    public interface IFinder
    {
        /// <summary>
        /// Returns the unique name of this Finder.  This name is used to identify
        /// which Finders are currently selected in 
        /// <see cref="pwiz.Skyline.Properties.Settings" />.
        /// </summary>
        string Name { get; }
        /// <summary>
        /// Returns the name that should be displayed in the UI.
        /// </summary>
        string DisplayName { get; }
        /// <summary>
        /// Returns a FindMatch if the location in the document satisfies
        /// this IFinder's criteria.
        /// This method gets called when the <see cref="pwiz.Skyline.Controls.FindResultsForm" />
        /// needs to update its display when the document changes.
        /// Returning null from this method will cause the FindResult to be displayed
        /// in a strikethrough font.
        /// </summary>
        /// <returns>A FindMatch indicating the text to be displayed and the range within that text to be highlighted, or null
        /// if the location in the document no longer satisfies this IFinder's criteria.</returns>
        FindMatch Match(BookmarkEnumerator bookmarkEnumerator);
        /// <summary>
        /// Advances the BookmarkEnumerator to the next location in the document which 
        /// meets this IFinder's criteria, and returns a FindMatch.
        /// If no location in the document matches, returns null.
        /// </summary>
        FindMatch NextMatch(BookmarkEnumerator bookmarkEnumerator);
        /// <summary>
        /// Returns all of the locations in the document which match this IFinder's criteria.
        /// The bookmarks can be returned in any order-- the caller will enumerate them in
        /// the correct order for display, and call <see cref="Match" /> as necessary.
        /// </summary>
        IEnumerable<Bookmark> FindAll(SrmDocument document);
    }
}
