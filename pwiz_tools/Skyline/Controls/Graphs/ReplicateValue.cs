using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.ElementLocators.ExportAnnotations;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Controls.Graphs
{
    public abstract class ReplicateValue : Immutable
    {
        public abstract string ToPersistedString();
        public abstract string Title { get; }
        public bool DisambiguateTitle { get; private set; }

        public ReplicateValue ChangeDisambiguateTitle(bool disambiguateTitle)
        {
            return ChangeProp(ImClone(this), im => im.DisambiguateTitle = disambiguateTitle);
        }

        public abstract object GetValue(ChromatogramSet chromatogramSet);

        public override string ToString()
        {
            return base.ToString();
        }

        protected abstract string DisambiguationPrefix { get; }

        public class Annotation : ReplicateValue
        {
            public Annotation(AnnotationDef annotationDef)
            {
                AnnotationDef = annotationDef;
            }

            public AnnotationDef AnnotationDef { get; private set; }

            public override string Title
            {
                get { return AnnotationDef.Name; }
            }

            public override object GetValue(ChromatogramSet chromatogramSet)
            {
                return chromatogramSet.Annotations.GetAnnotation(AnnotationDef);
            }

            public override string ToPersistedString()
            {
                return DocumentAnnotations.ANNOTATION_PREFIX + AnnotationDef.Name;
            }

            protected override string DisambiguationPrefix
            {
                get { return "Annotation: "; }
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
            public override string Title
            {
                get { return _getLabelFunc(); }
            }

            public override object GetValue(ChromatogramSet node)
            {
                return _getValueFunc(node);
            }

            public override string ToPersistedString()
            {
                return DocumentAnnotations.PROPERTY_PREFIX + PropertyName;
            }

            protected override string DisambiguationPrefix
            {
                get { return "Property: "; }
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
        }

        public static IEnumerable<ReplicateValue> GetGroupableReplicateValues(SrmSettings settings)
        {
            var withDistinctValues = GetAllReplicateValues(settings)
                .Where(replicateValue=>replicateValue is Annotation || HasDistinctValues(settings, replicateValue)).ToArray();
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

        private static bool HasDistinctValues(SrmSettings settings, ReplicateValue replicateValue)
        {
            if (!settings.HasResults)
            {
                return false;
            }

            var values = settings.MeasuredResults.Chromatograms.Select(replicateValue.GetValue).Distinct().ToArray();
            return values.Length > 1;
        }
    }
}
