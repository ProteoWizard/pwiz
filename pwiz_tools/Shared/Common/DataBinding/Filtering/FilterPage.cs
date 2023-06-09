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

namespace pwiz.Common.DataBinding.Filtering
{
    /// <summary>
    /// Represents a particular page to be displayed in the filter editor.
    /// The filter page has an implied filter (the <see cref="Discriminant"/>)
    /// which gets AND'd with the <see cref="FilterSpec"/> lines the user
    /// adds in the filter editor.
    /// </summary>
    public class FilterPage
    {
        private readonly Func<string> _getCaptionFunc;
        private readonly Func<string> _getDescriptionFunc;
        public FilterPage(Func<string> getCaptionFunc, Func<string> getDescriptionFunc, FilterClause discriminant,
            IEnumerable<PropertyPath> availableColumns)
        {
            _getCaptionFunc = getCaptionFunc;
            _getDescriptionFunc = getDescriptionFunc;
            Discriminant = discriminant;
            AvailableColumns = ImmutableList.ValueOf(availableColumns);
        }

        public FilterPage(Func<string> getCaptionFunc, Func<string> getDescriptionFunc, FilterSpec discriminant,
            IEnumerable<PropertyPath> availableColumns) : this(getCaptionFunc, getDescriptionFunc, new FilterClause(ImmutableList.Singleton(discriminant)), availableColumns)
        {
        }

        /// <summary>
        /// Constructs a FilterPage for a clause which does not match any pre-defined filter pages.
        /// </summary>
        /// <param name="availableColumns"></param>
        public FilterPage(IEnumerable<PropertyPath> availableColumns) 
            : this(null, null, FilterClause.EMPTY, availableColumns)
        {
        }

        public string Caption
        {
            get { return _getCaptionFunc?.Invoke(); }
        }

        public string Description
        {
            get { return _getDescriptionFunc?.Invoke(); }
        }
        /// <summary>
        /// The filter which is AND'd with the user's filter.
        /// </summary>
        public virtual FilterClause Discriminant { get; }
        public virtual IEnumerable<PropertyPath> AvailableColumns { get; }

        /// <summary>
        /// If the filter clause does not include all of the filter specs in the discriminant,
        /// returns null.
        /// Otherwise, returns the filter clause with the discriminant removed.
        /// </summary>
        public FilterClause MatchDiscriminant(FilterClause filterClause)
        {
            var discriminant = Discriminant.FilterSpecs.ToHashSet();
            var remainder = new List<FilterSpec>();
            foreach (var filterSpec in filterClause.FilterSpecs)
            {
                if (!discriminant.Remove(filterSpec))
                {
                    remainder.Add(filterSpec);
                }
            }

            if (discriminant.Count == 0)
            {
                return new FilterClause(remainder);
            }

            return null;
        }

        protected bool Equals(FilterPage other)
        {
            return Discriminant.Equals(other.Discriminant) &&
                   AvailableColumns.Equals(other.AvailableColumns);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((FilterPage)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Discriminant.GetHashCode();
                hashCode = (hashCode * 397) ^ AvailableColumns.GetHashCode();
                return hashCode;
            }
        }
    }
}
