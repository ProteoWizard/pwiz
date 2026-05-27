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
            var predicates = Clauses
                .Select(clause => AbsoluteCollisionEnergy(clause).MakePredicate<SpectrumClass>(dataSchema)).ToList();
            return x =>
            {
                var spectrumClass = new SpectrumClass(new SpectrumClassKey(SpectrumClassColumn.ALL, x));
                foreach (var predicate in predicates)
                {
                    if (predicate(spectrumClass))
                    {
                        return true;
                    }
                }

                return false;
            };
        }

        /// <summary>
        /// Returns a copy of <paramref name="clause"/> with any CollisionEnergy criterion's operand
        /// replaced by its absolute value, so the filter matches on CE magnitude. Vendors report
        /// collision energy as a positive magnitude, but users (and Sciex-style transition lists) use
        /// negative CE for negative-polarity data; matching on magnitude lets a "CollisionEnergy = -17"
        /// filter select spectra acquired at CE 17.
        ///
        /// Comparing magnitudes cannot mix polarities here: a spectrum only reaches this filter for a
        /// target whose precursor m/z it matches, and that match already requires the spectrum's scan
        /// polarity and the target precursor's charge to agree. So the two CE values necessarily share a
        /// polarity and can differ only in sign convention (vendor magnitude vs. the user's signed value).
        ///
        /// Only the predicate built here uses the magnitude; the stored filter (and what the user sees)
        /// is left unchanged, and the spectrum side is already a magnitude.
        /// </summary>
        private static FilterClause AbsoluteCollisionEnergy(FilterClause clause)
        {
            var collisionEnergyPath = SpectrumClassColumn.CollisionEnergy.PropertyPath;
            if (!clause.FilterSpecs.Any(spec => Equals(spec.ColumnId, collisionEnergyPath)))
            {
                return clause;
            }
            return new FilterClause(clause.FilterSpecs.Select(spec =>
                Equals(spec.ColumnId, collisionEnergyPath) ? AbsoluteOperand(spec) : spec));
        }

        private static FilterSpec AbsoluteOperand(FilterSpec spec)
        {
            var operandText = spec.Predicate.InvariantOperandText;
            if (string.IsNullOrEmpty(operandText))
            {
                return spec;
            }
            // InvariantOperandText is a single value or a comma-separated list of doubles (invariant
            // formatting uses '.' for the decimal point and ',' to separate list items). Strip a
            // leading minus sign from each numeric token but otherwise leave the text exactly as
            // entered: the number of decimal places controls the match tolerance (PrecisionNumber),
            // so reformatting (e.g. Math.Abs(value).ToString()) would silently change the tolerance.
            var absoluteText = string.Join(@",", operandText.Split(',').Select(token =>
            {
                var trimmed = token.Trim();
                if (trimmed.StartsWith(@"-") &&
                    double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                {
                    return trimmed.Substring(1);
                }
                return token;
            }));
            return new FilterSpec(spec.ColumnId,
                FilterPredicate.FromInvariantOperandText(spec.Operation, absoluteText));
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
