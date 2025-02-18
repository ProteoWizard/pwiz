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
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    /// <summary>
    /// Formattable list of retention time values which also has "Min", "Max" and "Mean" sub-properties.
    /// Suitable for displaying a list of retention times in the Document Grid.
    /// </summary>
    public class RetentionTimeValues : IFormattable
    {
        private ImmutableList<double> _times;
        private RetentionTimeValues(ImmutableList<double> times)
        {
            _times = times;
        }

        public static RetentionTimeValues ForTimes(IEnumerable<double> times)
        {
            var list = ImmutableList.ValueOf(times.OrderBy(t => t));
            if (list.Count == 0)
            {
                return null;
            }

            return new RetentionTimeValues(list);
        }

        [Format(Formats.RETENTION_TIME)]
        public double Min
        {
            get { return _times[0]; }
        }

        [Format(Formats.RETENTION_TIME)]
        public double Max
        {
            get { return _times[_times.Count - 1]; }
        }

        [Format(Formats.RETENTION_TIME)]
        public double Mean
        {
            get { return _times.Average(); }
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return new FormattableList<double>(_times).ToString(format, formatProvider);
        }

        public override string ToString()
        {
            return new FormattableList<double>(_times).ToString();
        }
    }
}
