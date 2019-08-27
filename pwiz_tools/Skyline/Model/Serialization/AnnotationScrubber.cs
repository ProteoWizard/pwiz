/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Serialization
{
    /// <summary>
    /// Removes invalid and calculated annotations from Annotation object, and also uses a StringPool to
    /// reduce duplicated strings.
    /// </summary>
    public class AnnotationScrubber
    {
        private readonly IDictionary<AnnotationDef.AnnotationTarget, HashSet<string>> _validAnnotationNames;

        /// <summary>
        /// Construct a new AnnotationScrubber.
        /// </summary>
        public static AnnotationScrubber MakeAnnotationScrubber(StringPool stringPool, DataSettings dataSettings, bool removeInvalidAnnotations)
        {
            var writeableAnnotations = dataSettings.AnnotationDefs.Where(def => def.Expression == null).ToArray();
            Dictionary<AnnotationDef.AnnotationTarget, HashSet<string>> validAnnotationNames = null;
            if (removeInvalidAnnotations)
            {
                validAnnotationNames = new Dictionary<AnnotationDef.AnnotationTarget, HashSet<string>>();
                foreach (AnnotationDef.AnnotationTarget target in Enum.GetValues(typeof(AnnotationDef.AnnotationTarget)))
                {
                    var names = new HashSet<string>(writeableAnnotations.Where(def => def.AnnotationTargets.Contains(target)).Select(def => def.Name));
                    validAnnotationNames.Add(target, names);
                }
            }
            return new AnnotationScrubber(stringPool, validAnnotationNames);
        }
        private AnnotationScrubber(StringPool stringPool,
            IDictionary<AnnotationDef.AnnotationTarget, HashSet<string>> validAnnotationNames)
        {
            StringPool = stringPool;
            _validAnnotationNames = validAnnotationNames;
        }

        public StringPool StringPool { get; private set; }

        /// <summary>
        /// Remove annotations from the Annotations object which do not apply to the particular target,
        /// or are calculated.
        /// Also uses the StringPool to reduce duplicated strings.
        /// </summary>
        public Annotations ScrubAnnotations(Annotations annotations, AnnotationDef.AnnotationTarget target)
        {
            if (annotations.IsEmpty)
            {
                return annotations;
            }

            var newAnnotations = annotations.ListAnnotations().AsEnumerable();
            if (null != _validAnnotationNames)
            {
                var validNames = _validAnnotationNames[target];
                newAnnotations = newAnnotations.Where(kvp => validNames.Contains(kvp.Key));
            }

            newAnnotations = newAnnotations.Select(kvp => new KeyValuePair<string, string>(
                StringPool.GetString(kvp.Key), kvp.Value));

            return new Annotations(StringPool.GetString(annotations.Note), newAnnotations, annotations.ColorIndex);
        }

        /// <summary>
        /// Calls ScrubAnnotations on the Annotations for the ChromatogramSets in the SrmSettings.
        /// </summary>
        public SrmSettings ScrubSrmSettings(SrmSettings settings)
        {
            if (!settings.HasResults)
            {
                return settings;
            }

            var newChromatograms = ImmutableList.ValueOf(settings.MeasuredResults.Chromatograms.Select(chromSet =>
                chromSet.ChangeAnnotations(ScrubAnnotations(chromSet.Annotations,
                    AnnotationDef.AnnotationTarget.replicate))));
            return settings.ChangeMeasuredResults(settings.MeasuredResults.ChangeChromatograms(newChromatograms));
        }
    }
}
