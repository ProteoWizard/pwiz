/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Util
{
    internal static class AnnotationHelper
    {
        /// <summary>
        /// Gets possible values for an annotation.
        /// </summary>
        public static IEnumerable<object> GetPossibleAnnotations(SrmDocument document, ReplicateValue replicateValue)
        {
            if (!document.Settings.HasResults || null == replicateValue)
            {
                return new object[0];
            }
            var annotationCalculator = new AnnotationCalculator(document);
            return document.Settings.MeasuredResults.Chromatograms
                .Select(chromSet => replicateValue.GetValue(annotationCalculator, chromSet)).Distinct()
                .OrderBy(x=>x, CollectionUtil.ColumnValueComparer);
        }

        /// <summary>
        /// Gets replicate indices for a specific grouping annotation and value.
        /// </summary>
        public static int[] GetReplicateIndices(SrmDocument document, ReplicateValue group, object groupValue)
        {
            var defaultResult = Enumerable.Range(0, document.Settings.MeasuredResults.Chromatograms.Count).ToArray();
            if (group == null)
                return defaultResult;

            var annotationCalculator = new AnnotationCalculator(document);
            return defaultResult.Where(i => Equals(groupValue,
                group.GetValue(annotationCalculator, document.Settings.MeasuredResults.Chromatograms[i]))).ToArray();
        }
    }
}