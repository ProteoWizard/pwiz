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
using System.Globalization;
using System.Linq;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.ElementLocators.ExportAnnotations;
using pwiz.Skyline.Model.GroupComparison;

namespace pwiz.Skyline.Model.Results
{
    public abstract class ReplicateValue : Immutable
    {
        protected ReplicateValue(PropertyPath propertyPath)
        {
            PropertyPath = propertyPath;
        }
        
        public PropertyPath PropertyPath { get; }

        public string ToPersistedString()
        {
            if (PropertyPath.Parent.IsRoot)
            {
                return PropertyPath.Name;
            }
            return PropertyPath.ToString();
        }

        public GroupIdentifier Parse(string value)
        {
            return GroupIdentifier.MakeGroupIdentifier(ParsePersistedValue(value));
        }

        public string Serialize(GroupIdentifier? groupIdentifier)
        {
            return SerializeValue(groupIdentifier?.Value);
        }

        protected abstract object ParsePersistedValue(string value);

        protected virtual string SerializeValue(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            return LocalizationHelper.CallWithCulture(CultureInfo.InvariantCulture, value.ToString);
        }

        public string Title
        {
            get
            {
                if (DisambiguateTitle)
                {
                    return DisambiguationPrefix + BaseTitle;
                }

                var indent = new string(' ', Math.Max(PropertyPath.Length - 1, 0));
                return indent + BaseTitle;
            }
        }

        protected abstract string BaseTitle { get; }

        public bool DisambiguateTitle { get; private set; }

        public ReplicateValue ChangeDisambiguateTitle(bool disambiguateTitle)
        {
            return ChangeProp(ImClone(this), im => im.DisambiguateTitle = disambiguateTitle);
        }

        public abstract object GetValue(AnnotationCalculator annotationCalculator, ChromatogramSet chromatogramSet);

        protected abstract string DisambiguationPrefix { get; }

        protected virtual IEnumerable<AnnotationDef> AncestorAnnotationDefs
        {
            get
            {
                return Array.Empty<AnnotationDef>();
            }
        }

        public override string ToString()
        {
            return Title;
        }

        protected bool Equals(ReplicateValue other)
        {
            return Equals(PropertyPath, other.PropertyPath);
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((ReplicateValue)obj);
        }

        public override int GetHashCode()
        {
            return PropertyPath.GetHashCode();
        }

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
            public Annotation(AnnotationDef annotationDef) : base(PropertyPath.Root.Property(DocumentAnnotations.ANNOTATION_PREFIX + annotationDef.Name))
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

            protected override string DisambiguationPrefix
            {
                get { return ResultsResources.Annotation_DisambiguationPrefix_Annotation__; }
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

            protected override IEnumerable<AnnotationDef> AncestorAnnotationDefs
            {
                get
                {
                    return new[] { AnnotationDef };
                }
            }

            protected override object ParsePersistedValue(string value)
            {
                return AnnotationDef.ParsePersistedString(value);
            }
        }

        public class Lookup : ReplicateValue
        {
            public Lookup(ReplicateValue parent, AnnotationDef listColumn) : base(parent.PropertyPath.Property(AnnotationDef.ANNOTATION_PREFIX + listColumn.Name))
            {
                Parent = parent;
                ListColumn = listColumn;
            }
            
            public ReplicateValue Parent { get; }
            public AnnotationDef ListColumn { get; }
            protected override string BaseTitle
            {
                get
                {
                    return ListColumn.Name;
                }
            }

            public override object GetValue(AnnotationCalculator annotationCalculator, ChromatogramSet chromatogramSet)
            {
                var parentAnnotationDef = Parent.AncestorAnnotationDefs.FirstOrDefault();
                if (string.IsNullOrEmpty(parentAnnotationDef?.Lookup))
                {
                    return null;
                }

                var parentValue = Parent.GetValue(annotationCalculator, chromatogramSet);
                if (parentValue == null)
                {
                    return null;
                }
                var listData = annotationCalculator.SrmDocument.Settings.DataSettings.FindList(parentAnnotationDef.Lookup);
                var rowIndex = listData.RowIndexOfPrimaryKey(parentValue);
                if (rowIndex < 0)
                {
                    return null;
                }
                int columnIndex = listData.FindColumnIndex(ListColumn.Name);
                if (columnIndex < 0)
                {
                    return null;
                }

                return listData.Columns[columnIndex].GetValue(rowIndex);
            }

            protected override string DisambiguationPrefix
            {
                get
                {
                    return Parent.AncestorAnnotationDefs.FirstOrDefault()?.Lookup + @" ";
                }
            }

            protected override IEnumerable<AnnotationDef> AncestorAnnotationDefs
            {
                get
                {
                    return Parent.AncestorAnnotationDefs.Prepend(ListColumn);
                }
            }

            protected override object ParsePersistedValue(string value)
            {
                return ListColumn.ParsePersistedString(value);
            }
        }

        public class Property : ReplicateValue
        {
            private readonly Func<string> _getLabelFunc;
            private readonly Func<ChromatogramSet, object> _getValueFunc;
            private readonly Func<string, object> _parseFunc;
            private readonly Func<object, string> _serializeFunc;
            private Property(string name, Func<string> getLabelFunc, Func<ChromatogramSet, object> getValueFunc, Func<string, object> parseFunc, Func<object, string> serializeFunc) : base(PropertyPath.Root.Property(DocumentAnnotations.PROPERTY_PREFIX + name))
            {
                PropertyName = name;
                _getLabelFunc = getLabelFunc;
                _getValueFunc = getValueFunc;
                _parseFunc = parseFunc;
                _serializeFunc = serializeFunc;
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

            protected override string DisambiguationPrefix
            {
                get { return ResultsResources.Property_DisambiguationPrefix_Property__; }
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

            public static Property Define<T>(string name, Func<string> getLabelFunc,
                Func<ChromatogramSet, T> getValueFunc, Func<string, T> parseFunc, Func<T, string> serializeFunc)
            {
                Func<object, string> serializeObjectFunc = o => o is T t ? serializeFunc(t) : string.Empty;
                return new Property(name, getLabelFunc, c => getValueFunc(c), s => parseFunc(s), serializeObjectFunc);
            }

            public static Property DefineString(string name, Func<string> getLabelFunc,
                Func<ChromatogramSet, string> getValueFunc)
            {
                return Define(name, getLabelFunc, getValueFunc, s => s, s=>s);
            }

            public static Property DefineNumber(string name, Func<string> getLabelFunc,
                Func<ChromatogramSet, double?> getValueFunc)
            {
                return Define(name, getLabelFunc, getValueFunc, 
                    AnnotationDef.ParseNumber,
                    d => d?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            }

            protected override object ParsePersistedValue(string value)
            {
                return _parseFunc(value);
            }

            protected override string SerializeValue(object value)
            {
                return _serializeFunc(value);
            }
        }

        private static IEnumerable<ReplicateValue> GetLookupReplicateValues(SrmSettings settings, ReplicateValue parent)
        {
            var listDef = settings.DataSettings
                .FindList(parent.AncestorAnnotationDefs.FirstOrDefault()?.Lookup)?.ListDef;
            if (listDef == null)
            {
                yield break;
            }
            var parentLists = parent.AncestorAnnotationDefs.Select(annotationDef => annotationDef.Lookup).ToHashSet();
            foreach (var column in listDef.Properties)
            {
                if (column.Name == listDef.IdProperty)
                {
                    continue;
                }
                var lookup = new Lookup(parent, column);
                yield return lookup;
                if (string.IsNullOrEmpty(column.Lookup) || parentLists.Contains(column.Lookup))
                {
                    foreach (var child in GetLookupReplicateValues(settings, lookup))
                    {
                        yield return child;
                    }
                }
            }
        }

        public static IEnumerable<ReplicateValue> GetAllReplicateValues(SrmSettings settings)
        {
            foreach (var annotationDef in settings.DataSettings.AnnotationDefs)
            {
                if (annotationDef.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.replicate))
                {
                    var annotation = new Annotation(annotationDef);
                    yield return annotation;
                    foreach (var child in GetLookupReplicateValues(settings, annotation))
                    {
                        yield return child;
                    }
                }
            }

            yield return Property.Define(nameof(ColumnCaptions.SampleType),
                () => ColumnCaptions.SampleType, chromatogramSet => chromatogramSet.SampleType, SampleType.FromName, s=>s.Name);
            yield return Property.DefineNumber(nameof(ColumnCaptions.AnalyteConcentration),
                () => ColumnCaptions.AnalyteConcentration, chromatogramSet => chromatogramSet.AnalyteConcentration);
            yield return Property.DefineString(nameof(ColumnCaptions.BatchName),
                () => ColumnCaptions.BatchName, chromatogramSet => chromatogramSet.BatchName);
            yield return Property.DefineNumber(nameof(ColumnCaptions.SampleDilutionFactor),
                ()=>ColumnCaptions.SampleDilutionFactor, chromatogramSet=>chromatogramSet.SampleDilutionFactor);
        }

        public static IEnumerable<ReplicateValue> GetGroupableReplicateValues(SrmDocument document)
        {
            var settings = document.Settings;
            var annotationCalculator = new AnnotationCalculator(document);
            var replicateValues = GetAllReplicateValues(settings).ToList();
            if (document.Settings.HasResults)
            {
                replicateValues = replicateValues.Where(replicateValue =>
                    replicateValue is Annotation || HasDistinctValues(annotationCalculator, replicateValue)).ToList();
            }

            var countsByTitle = replicateValues.GroupBy(replicateValue => replicateValue.Title)
                .ToDictionary(group => group.Key, group => group.Count());
            foreach (var replicateValue in replicateValues)
            {
                if (countsByTitle[replicateValue.Title] > 1)
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
