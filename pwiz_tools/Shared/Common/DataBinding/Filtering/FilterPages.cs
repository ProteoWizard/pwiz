/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Filtering
{
    /// <summary>
    /// A list of FilterClauses that have been split up into separate pages for editing.
    /// </summary>
    public class FilterPages : Immutable, IEquatable<FilterPages>
    {
        public FilterPages(IEnumerable<FilterPage> pages, IEnumerable<FilterClause> clauses)
        {
            Pages = ImmutableList.ValueOf(pages);
            Clauses = ImmutableList.ValueOf(clauses);
            if (Clauses.Count != Pages.Count)
            {
                throw new ArgumentException();
            }
        }

        /// <summary>
        /// Returns a FilterPages consisting of the pages which match the passed in clauses.
        /// </summary>
        public static FilterPages FromClauses(IList<FilterPage> availablePages, IEnumerable<FilterClause> clauses)
        {
            var pages = new List<FilterPage>();
            var remainders = new List<FilterClause>();
            foreach (var clause in clauses)
            {
                foreach (var page in availablePages)
                {
                    var remainder = page.MatchDiscriminant(clause);
                    if (remainder != null)
                    {
                        pages.Add(page);
                        remainders.Add(remainder);
                        break;
                    }
                }
            }

            return new FilterPages(pages, remainders);
        }

        /// <summary>
        /// Returns a FilterPages representing a blank filter with the given pages.
        /// </summary>
        public static FilterPages Blank(params FilterPage[] pages)
        {
            return new FilterPages(pages, Enumerable.Repeat(FilterClause.EMPTY, pages.Length));
        }

        public ImmutableList<FilterPage> Pages { get; }
        public ImmutableList<FilterClause> Clauses { get; private set; }

        public FilterPages ReplaceClause(int pageIndex, FilterClause clause)
        {
            return ChangeProp(ImClone(this), im =>
                im.Clauses = im.Clauses.ReplaceAt(pageIndex, clause)
            );
        }

        public bool Equals(FilterPages other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(Pages, other.Pages) && Equals(Clauses, other.Clauses);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((FilterPages)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Pages.GetHashCode()* 397) ^ Clauses.GetHashCode();
            }
        }
    }
}
