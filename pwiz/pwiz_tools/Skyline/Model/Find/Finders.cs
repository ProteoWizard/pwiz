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
    /// Holds the list of IFinder implementations that Skyline knows about.
    /// </summary>
    public static class Finders
    {
        private static readonly List<IFinder> LST_FINDERS = new List<IFinder>();
        /// <summary>
        /// Lists all of the Finders that have been registered.
        /// Currently, the order of the finders returned by this method
        /// is the order that the Finders are displayed in the FindNodeDlg.
        /// Also, this is the precedence order of the Finders (so that if
        /// one location in the document matches more than one Finder that
        /// the user is looking for, the earlier one in the list is the
        /// one that is displayed as matching in the FindResultsForm)
        /// </summary>
        public static IList<IFinder> ListAllFinders()
        {
            return LST_FINDERS.AsReadOnly();
        }

        /// <summary>
        /// Registers an IFinder.
        /// </summary>
        public static void AddFinder(IFinder finder)
        {
            LST_FINDERS.Add(finder);
        }

        static Finders()
        {
            AddFinder(new MismatchedIsotopeTransitionsFinder());
            AddFinder(new MissingLibraryDataFinder());
            AddFinder(new MissingAllResultsFinder());
            AddFinder(new MissingAnyResultsFinder());
            AddFinder(new UnintegratedTransitionFinder());
            AddFinder(new ManuallyIntegratedPeakFinder());
            AddFinder(new TruncatedPeakFinder());
        }
    }
}
