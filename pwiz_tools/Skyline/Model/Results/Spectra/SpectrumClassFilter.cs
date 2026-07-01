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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Filtering;
using pwiz.Common.Spectra;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public struct SpectrumClassFilter : IEquatable<SpectrumClassFilter>, IComparable, IComparable<SpectrumClassFilter>
    {
        public static readonly FilterPage Ms1FilterPage = new FilterPage(() => SpectraResources.SpectrumClassFilter_Ms1FilterPage_MS1,
            ()=>SpectraResources.SpectrumClassFilter_Ms1FilterPage_Criteria_which_MS1_spectra_must_satisfy_to_be_included_in_extracted_chromatogram,
            new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClassColumn.MsLevel)),FilterOperations.OP_EQUALS, 1),
            SpectrumClassColumn.MS1.Select(col => col.PropertyPath));

        public static readonly FilterPage Ms2FilterPage = new FilterPage(() => SpectraResources.SpectrumClassFilter_Ms2FilterPage_MS2_,
            ()=>SpectraResources.SpectrumClassFilter_Ms2FilterPage_Criteria_which_spectra_with_MS_level_2_or_higher_must_satisfy_to_be_included_in_extracted_chromatogram,
            new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClassColumn.MsLevel)), FilterOperations.OP_IS_GREATER_THAN, 1),
            SpectrumClassColumn.ALL.Select(col => col.PropertyPath));

        public static readonly FilterPage GenericFilterPage = new FilterPage(SpectrumClassColumn.ALL.Select(col => col.PropertyPath));

        private static ImmutableList<FilterPage> _allPages =
            ImmutableList.ValueOf(new[] { Ms1FilterPage, Ms2FilterPage, GenericFilterPage });
        public const string XML_ROOT = "spectrum_filter";
        private ImmutableList<FilterClause> _clauses;

        public SpectrumClassFilter(IEnumerable<FilterClause> alternatives)
        {
            var list = ImmutableList.ValueOf(alternatives);
            if (list?.Count > 0 && !list.Any(clause=>clause.IsEmpty))
            {
                _clauses = list;
            }
            else
            {
                _clauses = null;
            }
        }

        public SpectrumClassFilter(params FilterClause[] alternatives) : this(
            (IEnumerable<FilterClause>)alternatives)
        {
        }

        public static SpectrumClassFilter FromFilterPages(FilterPages filterPages)
        {
            if (filterPages.Clauses.All(clause => clause.IsEmpty))
            {
                return default;
            }
            var clauses = new List<FilterClause>();
            for (int iPage = 0; iPage < filterPages.Pages.Count; iPage++)
            {
                clauses.Add(new FilterClause(filterPages.Pages[iPage].Discriminant.FilterSpecs
                    .Concat(filterPages.Clauses[iPage].FilterSpecs)));
            }

            return new SpectrumClassFilter(clauses);
        }

        /// <summary>
        /// Returns a filter which matches all MS1 spectra plus the MS2 spectra which match the given filter.
        /// </summary>
        public static SpectrumClassFilter Ms2Filter(FilterClause ms2FilterClause)
        {
            return new SpectrumClassFilter(Ms1FilterPage.Discriminant,
                new FilterClause(Ms2FilterPage.Discriminant.FilterSpecs.Concat(ms2FilterClause.FilterSpecs)));
        }

        public ImmutableList<FilterClause> Clauses
        {
            get
            {
                return _clauses ?? ImmutableList<FilterClause>.EMPTY;
            }
        }

        public FilterClause this[int index]
        {
            get
            {
                return Clauses[index];
            }
        }

        public bool IsEmpty
        {
            get { return Clauses.Count == 0; }
        }

        public Predicate<SpectrumMetadata> MakePredicate()
        {
            if (IsEmpty)
            {
                return x => true;
            }

            var dataSchema = new DataSchema();
            var predicates = Clauses.Select(clause => CompileClause(clause, dataSchema)).ToList();
            return x =>
            {
                try
                {
                    var spectrumClass = new SpectrumClass(new SpectrumClassKey(SpectrumClassColumn.ALL, x));
                    foreach (var predicate in predicates)
                    {
                        if (predicate(spectrumClass, x))
                        {
                            return true;
                        }
                    }

                    return false;
                }
                catch (ListComparisonException exception)
                {
                    // A multi-value comparison operand pairwise-compares against the spectrum's value list
                    // (e.g. CollisionEnergy per MS level), which is valid when the lengths line up but can
                    // only be detected per spectrum. When they do not, surface the mismatch with
                    // spectrum-filter context so chromatogram extraction reports a clear error rather than
                    // failing with an opaque exception.
                    throw new InvalidDataException(string.Format(
                        SpectraResources.SpectrumClassFilter_MakePredicate_Error_evaluating_the_spectrum_filter___0_,
                        exception.Message), exception);
                }
            };
        }

        /// <summary>
        /// Compiles one clause into a predicate over both projections. The interpreted spectrum
        /// properties are evaluated against the <see cref="SpectrumClass"/> POCO as before; the dynamic
        /// mzML CV/user-parameter properties, which the POCO cannot host, are evaluated directly against
        /// <see cref="SpectrumMetadata"/>. A clause matches when every one of its specs matches (AND).
        /// </summary>
        private static Func<SpectrumClass, SpectrumMetadata, bool> CompileClause(FilterClause clause, DataSchema dataSchema)
        {
            var cvSpecs = new List<FilterSpec>();
            var nonCvSpecs = new List<FilterSpec>();
            foreach (var spec in clause.FilterSpecs)
            {
                var column = SpectrumClassColumn.FindColumn(spec.ColumnId);
                if (column != null && SpectrumClassColumn.IsCvParamColumn(column))
                {
                    cvSpecs.Add(spec);
                }
                else
                {
                    nonCvSpecs.Add(spec);
                }
            }

            // Non-CV specs keep resolving through the SpectrumClass POCO (and still throw for a genuinely
            // unknown column, as before). An empty non-CV list is a vacuously-true match.
            Predicate<SpectrumClass> nonCvPredicate = nonCvSpecs.Count == 0
                ? null
                : new FilterClause(nonCvSpecs).MakePredicate<SpectrumClass>(dataSchema);

            var cvPredicates = cvSpecs.Select(spec => CompileCvSpec(spec, dataSchema)).ToList();

            return (spectrumClass, metadata) =>
            {
                if (nonCvPredicate != null && !nonCvPredicate(spectrumClass))
                {
                    return false;
                }

                foreach (var cvPredicate in cvPredicates)
                {
                    if (!cvPredicate(metadata))
                    {
                        return false;
                    }
                }

                return true;
            };
        }

        /// <summary>
        /// Compiles one CV/user-parameter spec into a predicate over <see cref="SpectrumMetadata"/>.
        /// The value comes straight from <see cref="SpectrumMetadata.OtherParams"/> (bypassing the
        /// <see cref="SpectrumClass"/> projection) and is coerced to the type the operator implies.
        /// </summary>
        private static Predicate<SpectrumMetadata> CompileCvSpec(FilterSpec spec, DataSchema dataSchema)
        {
            var column = SpectrumClassColumn.FindColumn(spec.ColumnId);
            var type = DetermineCvOperandType(spec);
            var rawPredicate = spec.Predicate.MakePredicate(dataSchema, type);
            var columnDisplay = column.GetLocalizedColumnName(CultureInfo.CurrentCulture);
            return metadata =>
            {
                var value = CoerceCvValue(column.GetValue(metadata), type, columnDisplay);
                return rawPredicate(value);
            };
        }

        /// <summary>
        /// Decides whether a CV/user-parameter spec is a numeric or a string comparison. There is no
        /// stored type for these terms (a value is numeric only if it parses as an invariant number), so
        /// the operator and operand imply it: ordered comparisons are numeric; "contains"/"starts with"
        /// are string; equals/not-equals are numeric only when the operand itself is a number.
        /// </summary>
        private static Type DetermineCvOperandType(FilterSpec spec)
        {
            var op = spec.Operation;
            if (Equals(op, FilterOperations.OP_IS_GREATER_THAN) || Equals(op, FilterOperations.OP_IS_LESS_THAN) ||
                Equals(op, FilterOperations.OP_IS_GREATER_THAN_OR_EQUAL) ||
                Equals(op, FilterOperations.OP_IS_LESS_THAN_OR_EQUAL))
            {
                return typeof(double);
            }

            if (Equals(op, FilterOperations.OP_EQUALS) || Equals(op, FilterOperations.OP_NOT_EQUALS))
            {
                var operand = spec.Predicate.InvariantOperandText;
                if (operand != null &&
                    double.TryParse(operand, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                {
                    return typeof(double);
                }
            }

            return typeof(string);
        }

        /// <summary>
        /// Coerces a term's raw text value to the comparison type. A missing (or value-less) term yields
        /// null, which no comparison matches. A numeric comparison against a present, non-numeric value
        /// throws with spectrum-filter context (user decision: hard-fail rather than silently skip), so
        /// chromatogram extraction reports a clear error.
        /// </summary>
        private static object CoerceCvValue(object rawValue, Type type, string columnDisplay)
        {
            if (type != typeof(double))
            {
                return rawValue;
            }

            var text = rawValue as string;
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                return number;
            }

            throw new InvalidDataException(string.Format(
                SpectraResources.SpectrumClassFilter_MakePredicate_Error_evaluating_the_spectrum_filter___0_,
                string.Format(
                    SpectraResources.SpectrumClassFilter_CoerceCvValue_The_value___0___of_spectrum_property___1___is_not_a_number,
                    text, columnDisplay)));
        }

        public override string ToString()
        {
            return GetAbbreviatedText();
        }

        public string GetAbbreviatedText()
        {
            return GetText(false);
        }

        /// <summary>
        /// Returns a string representation of the filter.
        /// </summary>
        /// <param name="includeAll">Whether to include the word "All" when describing a clause that accepts all items</param>
        public string GetText(bool includeAll)
        {
            if (IsEmpty)
            {
                return string.Empty;
            }
            var filterPages = GetFilterPages();
            var parts = new List<string>();
            for (int iPage = 0; iPage < filterPages.Pages.Count; iPage++)
            {
                var page = filterPages.Pages[iPage];
                var clause = filterPages.Clauses[iPage];
                string clauseText;
                if (clause.IsEmpty)
                {
                    if (page.Discriminant.IsEmpty)
                    {
                        return includeAll ? SpectraResources.SpectrumClassFilter_GetText_All : string.Empty;
                    }
                    else
                    {
                        clauseText = SpectraResources.SpectrumClassFilter_GetText_All;
                    }
                }
                else
                {
                    clauseText = GetAbbreviatedText(filterPages.Clauses[iPage]);
                }
                string caption = filterPages.Pages[iPage].Caption;
                string part = clauseText;
                if (caption != null)
                {
                    part = TextUtil.ColonSeparate(caption, clauseText);
                }
                if (iPage != 0)
                {
                    part = TextUtil.SpaceSeparate(Resources.SpectrumClassFilter_GetAbbreviatedText_OR, part);
                }
                parts.Add(part);
            }

            return TextUtil.SpaceSeparate(parts);
        }

        public bool Equals(SpectrumClassFilter other)
        {
            return Equals(_clauses, other._clauses);
        }

        public override bool Equals(object obj)
        {
            return obj is SpectrumClassFilter other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (_clauses != null ? _clauses.GetHashCode() : 0);
        }

        public int CompareTo(SpectrumClassFilter other)
        {
            for (int i = 0; i < Clauses.Count && i < other.Clauses.Count; i++)
            {
                int result = Clauses[i].CompareTo(other.Clauses[i]);
                if (result != 0)
                {
                    return result;
                }
            }

            return Clauses.Count.CompareTo(other.Clauses.Count);
        }

        int IComparable.CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            return CompareTo((SpectrumClassFilter)obj);
        }
        public static string GetOperandDisplayText(DataSchema dataSchema, FilterSpec filterSpec)
        {
            var column = SpectrumClassColumn.FindColumn(filterSpec.ColumnId);
            if (column == null)
            {
                return filterSpec.Predicate.InvariantOperandText;
            }

            if (!filterSpec.Operation.HasOperand())
            {
                return null;
            }

            try
            {
                var value = filterSpec.Predicate.GetOperandValue(dataSchema, column.ValueType);
                if (value == null)
                {
                    return string.Empty;
                }

                // Use the operation-aware filter handler so values format the same as the grid
                // (e.g. Equals shows "5, 7" rather than the explicit-precision form "5E+0,7E+0").
                var handler = dataSchema.GetFilterHandler(column.ValueType);
                if (handler != null)
                {
                    return handler.OperandToString(filterSpec.Operation, dataSchema.DataSchemaLocalizer.FormatProvider, value);
                }
                return column.FormatAbbreviatedValue(value);
            }
            catch
            {
                return filterSpec.Predicate.GetOperandDisplayText(dataSchema, column.ValueType);
            }
        }

        public static string GetAbbreviatedText(FilterClause filterClause)
        {
            var dataSchema = new DataSchema(SkylineDataSchema.GetLocalizedSchemaLocalizer());
            var clauses = new List<string>();
            foreach (var filterSpec in filterClause.FilterSpecs)
            {
                var spectrumClassColumn = SpectrumClassColumn.FindColumn(filterSpec.ColumnId);
                if (spectrumClassColumn == null)
                {
                    clauses.Add(TextUtil.SpaceSeparate(filterSpec.Column, filterSpec.Operation?.DisplayName, filterSpec.Predicate.InvariantOperandText));
                }
                else
                {
                    var clauseText = new StringBuilder(spectrumClassColumn.GetAbbreviatedColumnName());
                    var opText = filterSpec.Operation.ShortDisplayName;
                    if (char.IsLetterOrDigit(opText[0]) || char.IsLetterOrDigit(opText[opText.Length - 1]))
                    {
                        clauseText.Append(@" ");
                        clauseText.Append(opText);
                        clauseText.Append(@" ");
                    }
                    else
                    {
                        clauseText.Append(opText);
                    }

                    clauseText.Append(GetOperandDisplayText(dataSchema, filterSpec));
                    clauses.Add(clauseText.ToString().Trim());
                }
            }

            return string.Join(SpectraResources.SpectrumClassFilter_GetAbbreviatedText__AND_, clauses);
        }

        public static SpectrumClassFilter ReadXml(XmlReader reader)
        {
            var clauses = new List<FilterClause>();
            while (reader.IsStartElement(XML_ROOT))
            {
                clauses.Add(new XmlElementHelper<FilterClause>(XML_ROOT).Deserialize(reader));
            }

            return new SpectrumClassFilter(clauses);
        }

        public void WriteXml(XmlWriter writer)
        {
            foreach (var clause in Clauses)
            {
                writer.WriteStartElement(XML_ROOT);
                clause.WriteXml(writer);
                writer.WriteEndElement();
            }
        }

        public static FilterPages GetFilterPages(TransitionGroupDocNode transitionGroupDocNode)
        {
            if (!transitionGroupDocNode.SpectrumClassFilter.IsEmpty)
            {
                return transitionGroupDocNode.SpectrumClassFilter.GetFilterPages();
            }

            if (transitionGroupDocNode.Transitions.Any(t => t.IsMs1) &&
                transitionGroupDocNode.Transitions.Any(t => !t.IsMs1))
            {
                return FilterPages.Blank(Ms1FilterPage, Ms2FilterPage);
            }

            return FilterPages.Blank(GenericFilterPage);
        }

        public FilterPages GetFilterPages()
        {
            return FilterPages.FromClauses(_allPages, Clauses);
        }

        public static FilterPages GetBlankFilterPages(IEnumerable<TransitionGroupDocNode> transitionGroupDocNodes)
        {
            bool anyMs1 = false;
            bool anyMs2 = false;
            foreach (var transition in transitionGroupDocNodes.SelectMany(tg => tg.Transitions))
            {
                anyMs1 = anyMs1 || transition.IsMs1;
                anyMs2 = anyMs2 || !transition.IsMs1;
                if (anyMs1 && anyMs2)
                {
                    return FilterPages.Blank(Ms1FilterPage, Ms2FilterPage);
                }
            }
            return FilterPages.Blank(GenericFilterPage);
        }

        public string ToFilterString()
        {
            return CreateSerializer().ToFilterString(Clauses);
        }

        public static SpectrumClassFilter ParseFilterString(string filterString)
        {
            // Try several cultures so a filter authored in one locale parses in another (e.g. a
            // comma-decimal value from a European transition list imported on a period-decimal
            // machine). Only number formatting varies; keywords are culture-independent (English).
            FormatException firstError = null;
            foreach (var localizer in GetParseLocalizers())
            {
                try
                {
                    return new SpectrumClassFilter(CreateSerializer(localizer).ParseFilterString(filterString));
                }
                catch (FilterOperandException)
                {
                    // An operand that is invalid for its column's type (e.g. a negative CollisionEnergy)
                    // is invalid in every locale, so surface its specific message rather than retrying
                    // and reporting the generic "invalid format" message below.
                    throw;
                }
                catch (FormatException ex)
                {
                    firstError = firstError ?? ex;
                }
            }
            // Replace the serializer's terse "invalid filter string" with a message that shows the
            // expected form (e.g. column/operator/value with spaces, combined with "and"/"or").
            throw new FormatException(string.Format(
                SpectraResources.SpectrumClassFilter_ParseFilterString_Invalid_spectrum_filter_format, filterString),
                firstError);
        }

        /// <summary>
        /// Returns null if <paramref name="filterString"/> parses and refers only to known
        /// spectrum properties; otherwise a message describing the problem. The serializer is
        /// syntactically lenient (an unrecognized column name is preserved rather than rejected),
        /// so this also checks that every referenced column resolves to a real spectrum property.
        /// </summary>
        public static string ValidateFilterString(string filterString)
        {
            if (string.IsNullOrEmpty(filterString))
            {
                return null;
            }
            SpectrumClassFilter filter;
            try
            {
                filter = ParseFilterString(filterString);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            foreach (var clause in filter.Clauses)
            {
                foreach (var filterSpec in clause.FilterSpecs)
                {
                    if (SpectrumClassColumn.FindColumn(filterSpec.ColumnId) == null)
                    {
                        return string.Format(
                            SpectraResources.SpectrumClassFilter_ValidateFilterString_Unknown_spectrum_property___0__,
                            filterSpec.Column);
                    }
                }
            }
            return null;
        }

        private static FilterClauseSerializer CreateSerializer()
        {
            return new FilterClauseSerializer(
                ColumnDescriptor.RootColumn(new DataSchema(SkylineDataSchema.GetLocalizedSchemaLocalizer()), typeof(SpectrumClass)));
        }

        private static FilterClauseSerializer CreateSerializer(DataSchemaLocalizer localizer)
        {
            return new FilterClauseSerializer(
                ColumnDescriptor.RootColumn(new DataSchema(localizer), typeof(SpectrumClass)));
        }

        /// <summary>
        /// Cultures to try when parsing a filter string, in priority order: invariant (period
        /// decimals; US/UK/Asian-authored files), French (the representative comma-decimal locale,
        /// so a European-authored value parses on a period-decimal machine), then the current
        /// culture (the user's own typing). Only the decimal separator differs between these;
        /// "and"/"or" are intentionally English in the parseable form regardless of culture, so
        /// no locale-specific keyword handling belongs here.
        /// </summary>
        private static IEnumerable<DataSchemaLocalizer> GetParseLocalizers()
        {
            var seen = new HashSet<string>();
            var cultures = new[]
            {
                CultureInfo.InvariantCulture,
                CultureInfo.GetCultureInfo(@"fr-FR"),
                CultureInfo.CurrentCulture
            };
            foreach (var culture in cultures)
            {
                if (seen.Add(culture.Name))
                {
                    yield return new DataSchemaLocalizer(culture, culture);
                }
            }
        }
    }
}
