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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{
    internal static class AnnotationHelper
    {
        /// <summary>
        /// Returns candidates for grouping by annotation by the annotation target. (usually replicates)
        /// </summary>
        public static string[] FindGroupsByTarget(SrmSettings settings, AnnotationDef.AnnotationTarget target)
        {
            var replicateAnnotations = settings.DataSettings.AnnotationDefs
                .Where(annotationDef => annotationDef.AnnotationTargets.Contains(target));

            return replicateAnnotations.Select(a => a.Name).ToArray();
        }

        /// <summary>
        /// Gets possible values for an annotation.
        /// </summary>
        public static string[] GetPossibleAnnotations(SrmSettings settings, string group, AnnotationDef.AnnotationTarget target)
        {
            var annotation = settings.DataSettings.AnnotationDefs.FirstOrDefault(annotationDef => annotationDef.AnnotationTargets.Contains(target) && annotationDef.Name == group);
            if (annotation == null)
            {
                return new string[0];
            }

            switch (annotation.Type)
            {
                case AnnotationDef.AnnotationType.text:
                case AnnotationDef.AnnotationType.number:
                    return settings.MeasuredResults == null ? new string[0] : settings.MeasuredResults.Chromatograms
                        .Select(c => c.Annotations.GetAnnotation(group)).Distinct().Where(s => s != null).ToArray();
                case AnnotationDef.AnnotationType.value_list:
                    return annotation.Items.ToArray();
                case AnnotationDef.AnnotationType.true_false:
                    return new[] { Resources.AnnotationHelper_GetReplicateIndicices_True, Resources.AnnotationHelper_GetReplicateIndicices_False };
                default:
                    return new string[0];   // Should never happen
            }
        }

        /// <summary>
        /// Gets replicate indices for a specific grouping annotation and value.
        /// </summary>
        public static int[] GetReplicateIndices(SrmSettings settings, string group, string annotationName)
        {
            var defaultResult = Enumerable.Range(0, settings.MeasuredResults.Chromatograms.Count).ToArray();
            if (string.IsNullOrEmpty(group) || string.IsNullOrEmpty(annotationName))
                return defaultResult;

            var annotation = settings.DataSettings.AnnotationDefs.FirstOrDefault(a => a.Name == group);
            if (annotation == null)
                return defaultResult;

            var result = new List<int>();

            for (var i = 0; i < settings.MeasuredResults.Chromatograms.Count; ++i)
            {
                switch (annotation.Type)
                {
                    case AnnotationDef.AnnotationType.text:
                    case AnnotationDef.AnnotationType.number:
                    case AnnotationDef.AnnotationType.value_list:
                        if (settings.MeasuredResults.Chromatograms[i].Annotations.GetAnnotation(group) ==
                            annotationName)
                        {
                            result.Add(i);
                        }
                        break;
                    case AnnotationDef.AnnotationType.true_false:
                        var a = settings.MeasuredResults.Chromatograms[i].Annotations.GetAnnotation(group);
                        if (a == null && annotationName == Resources.AnnotationHelper_GetReplicateIndicices_False ||
                            a != null && annotationName == Resources.AnnotationHelper_GetReplicateIndicices_True)
                        {
                            result.Add(i);
                        }
                        break;
                }
            }

            return result.ToArray();
        }
    }
}