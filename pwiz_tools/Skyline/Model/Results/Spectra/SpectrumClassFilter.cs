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
using Sprache;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public struct SpectrumClassFilter : IEquatable<SpectrumClassFilter>, IComparable, IComparable<SpectrumClassFilter>
    {
        public static readonly FilterPage Ms1FilterPage = new FilterPage(() => SpectraResources.SpectrumClassFilter_Ms1FilterPage_MS1,
            ()=>SpectraResources.SpectrumClassFilter_Ms1FilterPage_Criteria_which_MS1_spectra_must_satisfy_to_be_included_in_extracted_chromatogram,
            new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClassColumn.MsLevel)),
                FilterPredicate.CreateFilterPredicate(FilterOperations.OP_EQUALS, 1))
            ,
            SpectrumClassColumn.MS1.Select(col => col.PropertyPath));

        public static readonly FilterPage Ms2FilterPage = new FilterPage(() => SpectraResources.SpectrumClassFilter_Ms2FilterPage_MS2_,
            ()=>SpectraResources.SpectrumClassFilter_Ms2FilterPage_Criteria_which_spectra_with_MS_level_2_or_higher_must_satisfy_to_be_included_in_extracted_chromatogram,
            new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClassColumn.MsLevel)),
                FilterPredicate.CreateFilterPredicate(FilterOperations.OP_IS_GREATER_THAN, 1)),
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
            var predicates = Clauses.Select(x => x.MakePredicate<SpectrumClass>(dataSchema)).ToList();
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

            var operandType = filterSpec.Operation.GetOperandType(dataSchema, column.ValueType);
            if (operandType == null)
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

        #region Filter String Parsing and Serialization

        /// <summary>
        /// Returns the filter as a human-readable string that can be parsed back.
        /// Format: FilterClauses are separated by " or ", FilterSpecs within a clause by " and ".
        /// Multiple specs get parentheses: (spec1) and (spec2)
        /// Multiple clauses get parentheses: (clause1) or (clause2)
        /// </summary>
        public string ToFilterString()
        {
            if (IsEmpty)
            {
                return string.Empty;
            }

            if (Clauses.Count == 1)
            {
                return FormatClause(Clauses[0]);
            }

            return string.Join(@" or ", Clauses.Select(c => @"(" + FormatClause(c) + @")"));
        }

        private static string FormatClause(FilterClause clause)
        {
            if (clause.FilterSpecs.Count == 0)
            {
                return string.Empty;
            }

            if (clause.FilterSpecs.Count == 1)
            {
                return FormatFilterSpec(clause.FilterSpecs[0]);
            }

            return string.Join(@" and ", clause.FilterSpecs.Select(s => @"(" + FormatFilterSpec(s) + @")"));
        }

        private static string FormatFilterSpec(FilterSpec spec)
        {
            var sb = new StringBuilder();
            sb.Append(QuoteColumnIfNeeded(spec.Column));
            sb.Append(@" ");
            sb.Append(spec.Operation.OpSymbol);
            if (spec.Operation.GetOperandType(new DataSchema(), typeof(object)) != null)
            {
                sb.Append(@" ");
                sb.Append(FormatOperandValue(spec.Predicate.InvariantOperandText ?? string.Empty));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Quote column names with double quotes if needed.
        /// </summary>
        private static string QuoteColumnIfNeeded(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return @"""""";
            }
            if (value.Any(c => char.IsWhiteSpace(c) || c == '"' || c == '(' || c == ')'))
            {
                return @"""" + value.Replace(@"""", @"""""") + @"""";
            }
            return value;
        }

        /// <summary>
        /// Format operand values: numbers are unquoted, strings use single quotes.
        /// Arrays of values use square brackets: [value1, value2, ...]
        /// </summary>
        private static string FormatOperandValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return @"''";
            }
            // If it looks like a number, don't quote it
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                return value;
            }
            // Check if this is a comma-separated list (array)
            if (value.Contains(@","))
            {
                var elements = value.Split(',').Select(s => FormatSingleValue(s.Trim()));
                return @"[" + string.Join(@", ", elements) + @"]";
            }
            // Otherwise, use single quotes with '' as escape for literal single quote
            return @"'" + value.Replace(@"'", @"''") + @"'";
        }

        /// <summary>
        /// Format a single value (not an array): numbers are unquoted, strings use single quotes.
        /// </summary>
        private static string FormatSingleValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return @"''";
            }
            // If it looks like a number, don't quote it
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                return value;
            }
            // Otherwise, use single quotes with '' as escape for literal single quote
            return @"'" + value.Replace(@"'", @"''") + @"'";
        }

        /// <summary>
        /// Parses a filter string back into a SpectrumClassFilter.
        /// </summary>
        public static SpectrumClassFilter ParseFilterString(string filterString)
        {
            if (string.IsNullOrWhiteSpace(filterString))
            {
                return default;
            }

            var result = FilterParser.TryParse(filterString);
            if (!result.WasSuccessful)
            {
                throw new FormatException(string.Format(
                    SpectraResources.SpectrumClassFilter_ParseFilterString_Invalid_filter_string___0__, filterString));
            }
            return result.Value;
        }

        private static readonly Parser<SpectrumClassFilter> FilterParser = CreateFilterParser();

        private static Parser<SpectrumClassFilter> CreateFilterParser()
        {
            // Whitespace parser
            var ws = Parse.WhiteSpace.Many();

            // Double-quoted string for column names: "..." where "" represents a literal "
            var escapedDoubleQuote = Parse.String(@"""""").Return('"');
            var doubleQuotedChar = escapedDoubleQuote.Or(Parse.CharExcept('"'));
            var doubleQuotedString = Parse.Char('"')
                .Then(_ => doubleQuotedChar.Many().Text())
                .Then(content => Parse.Char('"').Return(content));

            // Unquoted identifier for column names: no whitespace, parens, or quotes
            var unquotedIdentifier = Parse.CharExcept(c => char.IsWhiteSpace(c) || c == '"' || c == '\'' || c == '(' || c == ')', @"identifier char")
                .AtLeastOnce().Text();

            // Column name identifier (double-quoted or unquoted)
            var identifier = doubleQuotedString.Or(unquotedIdentifier);

            // Single-quoted string for operand values: '...' where '' represents a literal '
            var escapedSingleQuote = Parse.String(@"''").Return('\'');
            var singleQuotedChar = escapedSingleQuote.Or(Parse.CharExcept('\''));
            var singleQuotedString = Parse.Char('\'')
                .Then(_ => singleQuotedChar.Many().Text())
                .Then(content => Parse.Char('\'').Return(content));

            // Unquoted number for operand values: optional minus, digits, optional decimal
            var unquotedNumber = Parse.Regex(@"-?[0-9]+\.?[0-9]*");

            // Single operand value (single-quoted string or unquoted number)
            var singleOperandValue = singleQuotedString.Or(unquotedNumber);

            // Array of values: [value1, value2, ...] - returns comma-separated string
            var arrayValue = Parse.Char('[')
                .Then(_ => ws)
                .Then(_ => singleOperandValue.DelimitedBy(ws.Then(_ => Parse.Char(',')).Then(_ => ws)))
                .Then(values => ws.Then(_ => Parse.Char(']')).Return(string.Join(@", ", values)));

            // Operand value (array, single-quoted string, or unquoted number)
            var operandValue = arrayValue.Or(singleQuotedString).Or(unquotedNumber);

            // Operator names
            var opSymbols = FilterOperations.ListOperations()
                .Where(op=>!string.IsNullOrEmpty(op.OpSymbol))
                .Select(op => op.OpSymbol)
                .OrderByDescending(name => name.Length) // Longer operators first to avoid partial matches
                .ToList();

            // Operator parser - try each operator name
            var operatorParser = opSymbols
                .Select(name => Parse.String(name).Text())
                .Aggregate((a, b) => a.Or(b));

            // FilterSpec: identifier operator value?
            var dataSchema = new DataSchema();
            var filterSpec = identifier
                .Then(column => ws.Then(_ => operatorParser)
                    .Then(opSymbol =>
                    {
                        var op = FilterOperations.GetOperationBySymbol(opSymbol);
                        // Check if this operator takes an operand
                        if (op.GetOperandType(dataSchema, typeof(object)) == null)
                        {
                            // No operand needed
                            return Parse.Return(new FilterSpec(PropertyPath.Parse(column),
                                FilterPredicate.CreateFilterPredicate(dataSchema, typeof(string), op, null)));
                        }
                        // Operand required
                        return ws.Then(_ => operandValue)
                            .Select(value => new FilterSpec(PropertyPath.Parse(column),
                                FilterPredicate.CreateFilterPredicate(dataSchema, typeof(string), op, value)));
                    }));

            // FilterSpec possibly wrapped in parentheses
            var parenFilterSpec = Parse.Char('(')
                .Then(_ => ws)
                .Then(_ => filterSpec)
                .Then(spec => ws.Then(_ => Parse.Char(')')).Return(spec));

            // A single spec or parenthesized spec
            var singleOrParenSpec = parenFilterSpec.Or(filterSpec);

            // FilterClause: specs joined by " and "
            var andKeyword = ws.Then(_ => Parse.String(@"and")).Then(_ => ws);
            var filterClause = singleOrParenSpec
                .Then(first => andKeyword.Then(_ => singleOrParenSpec).Many()
                    .Select(rest => new FilterClause(new[] { first }.Concat(rest))));

            // FilterClause possibly wrapped in parentheses
            var parenClause = Parse.Char('(')
                .Then(_ => ws)
                .Then(_ => filterClause)
                .Then(clause => ws.Then(_ => Parse.Char(')')).Return(clause));

            // A single clause or parenthesized clause
            // Try filterClause first so that "(spec1) and (spec2)" is parsed as a single clause with multiple specs,
            // rather than "(spec1)" being parsed as a parenthesized clause.
            var singleOrParenClause = filterClause.Or(parenClause);

            // Full filter: clauses joined by " or "
            var orKeyword = ws.Then(_ => Parse.String(@"or")).Then(_ => ws);
            return singleOrParenClause
                .Then(first => orKeyword.Then(_ => singleOrParenClause).Many()
                    .Select(rest => new SpectrumClassFilter(new[] { first }.Concat(rest))))
                .End();
        }

        #endregion
    }
}
