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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.ElementLocators.ExportAnnotations;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls.Graphs
{
    public abstract class ReplicateValue : Immutable
    {
        public abstract string ToPersistedString();

        public string Title
        {
            get { return DisambiguateTitle ? DisambiguationPrefix + BaseTitle : BaseTitle; }
        }

        protected abstract string BaseTitle { get; }

        public bool DisambiguateTitle { get; private set; }

        public ReplicateValue ChangeDisambiguateTitle(bool disambiguateTitle)
        {
            return ChangeProp(ImClone(this), im => im.DisambiguateTitle = disambiguateTitle);
        }

        public abstract object GetValue(AnnotationCalculator annotationCalculator, ChromatogramSet chromatogramSet);

        protected abstract string DisambiguationPrefix { get; }

        public static ReplicateValue FromPersistedString(SrmSettings settings, string persistedString)
        {
            if (string.IsNullOrEmpty(persistedString))
            {
                return null;
            }
            return GetAllReplicateValues(settings)
                .FirstOrDefault(value => value.ToPersistedString() == persistedString);
        }

        public class Annotation : ReplicateValue
        {
            public Annotation(AnnotationDef annotationDef)
            {
                AnnotationDef = annotationDef;
            }

            public AnnotationDef AnnotationDef { get; private set; }

            protected override string BaseTitle
            {
                get { return AnnotationDef.Name; }
            }

            public override object GetValue(AnnotationCalculator annotationCalculator, ChromatogramSet chromatogramSet)
            {
                return annotationCalculator.GetReplicateAnnotation(AnnotationDef, chromatogramSet);
            }

            public override string ToPersistedString()
            {
                return DocumentAnnotations.ANNOTATION_PREFIX + AnnotationDef.Name;
            }

            protected override string DisambiguationPrefix
            {
                get { return Resources.Annotation_DisambiguationPrefix_Annotation__; }
            }

            protected bool Equals(Annotation other)
            {
                return AnnotationDef.Name.Equals(other.AnnotationDef.Name);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((Annotation) obj);
            }

            public override int GetHashCode()
            {
                return AnnotationDef.Name.GetHashCode();
            }
        }

        public class Property : ReplicateValue
        {
            private Func<string> _getLabelFunc;
            private Func<ChromatogramSet, object> _getValueFunc;
            public Property(string name, Func<string> getLabelFunc, Func<ChromatogramSet, object> getValueFunc)
            {
                PropertyName = name;
                _getLabelFunc = getLabelFunc;
                _getValueFunc = getValueFunc;
            }

            public string PropertyName { get; private set; }
            protected override string BaseTitle
            {
                get { return _getLabelFunc(); }
            }

            public override object GetValue(AnnotationCalculator annotationCalculator, ChromatogramSet node)
            {
                return _getValueFunc(node);
            }

            public override string ToPersistedString()
            {
                return DocumentAnnotations.PROPERTY_PREFIX + PropertyName;
            }

            protected override string DisambiguationPrefix
            {
                get { return Resources.Property_DisambiguationPrefix_Property__; }
            }

            protected bool Equals(Property other)
            {
                return PropertyName == other.PropertyName;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((Property) obj);
            }

            public override int GetHashCode()
            {
                return PropertyName.GetHashCode();
            }
        }

        public static IEnumerable<ReplicateValue> GetAllReplicateValues(SrmSettings settings)
        {
            foreach (var annotationDef in settings.DataSettings.AnnotationDefs)
            {
                if (annotationDef.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.replicate))
                {
                    yield return new Annotation(annotationDef);
                }
            }

            yield return new Property(nameof(ColumnCaptions.SampleType),
                () => ColumnCaptions.SampleType, chromatogramSet => chromatogramSet.SampleType);
            yield return new Property(nameof(ColumnCaptions.AnalyteConcentration),
                () => ColumnCaptions.AnalyteConcentration, chromatogramSet => chromatogramSet.AnalyteConcentration);
            yield return new Property(nameof(ColumnCaptions.BatchName),
                () => ColumnCaptions.BatchName, chromatogramSet => chromatogramSet.BatchName);
            yield return new Property(nameof(ColumnCaptions.SampleDilutionFactor),
                ()=>ColumnCaptions.SampleDilutionFactor, chromatogramSet=>chromatogramSet.SampleDilutionFactor);
        }

        public static IEnumerable<ReplicateValue> GetGroupableReplicateValues(SrmDocument document)
        {
            var settings = document.Settings;
            var annotationCalculator = new AnnotationCalculator(document);
            var withDistinctValues = GetAllReplicateValues(settings)
                .Where(replicateValue=>replicateValue is Annotation || HasDistinctValues(annotationCalculator, replicateValue)).ToArray();
            var lookupByTitle = withDistinctValues.ToLookup(replicateValue => replicateValue.Title);
            foreach (var replicateValue in withDistinctValues)
            {
                if (lookupByTitle[replicateValue.Title].Skip(1).Any())
                {
                    yield return replicateValue.ChangeDisambiguateTitle(true);
                }
                else
                {
                    yield return replicateValue;
                }
            }
        }

        private static bool HasDistinctValues(AnnotationCalculator annotationCalculator, ReplicateValue replicateValue)
        {
            var settings = annotationCalculator.SrmDocument.Settings;
            if (!settings.HasResults)
            {
                return false;
            }

            var values = settings.MeasuredResults.Chromatograms.Select(chromSet=>replicateValue.GetValue(annotationCalculator, chromSet)).Distinct().ToArray();
            return values.Length > 1;
        }
    }
}
