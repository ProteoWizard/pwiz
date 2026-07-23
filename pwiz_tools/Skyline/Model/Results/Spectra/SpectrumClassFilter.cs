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
using System.Text.RegularExpressions;
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

            // "Is Declared"/"Is Not Declared" test only for the term's presence: a value-less flag term
            // captures with an empty (non-null) value, so presence is exactly GetValue() != null, while a
            // spectrum lacking the term yields null. This bypasses the value-coercion path below, which a
            // present flag's empty value could not survive as "present".
            var op = spec.Operation;
            if (Equals(op, FilterOperations.OP_IS_DECLARED) || Equals(op, FilterOperations.OP_IS_NOT_DECLARED))
            {
                bool declaredWanted = Equals(op, FilterOperations.OP_IS_DECLARED);
                return metadata => (column.GetValue(metadata) != null) == declaredWanted;
            }

            // Equals/Not Equals with a numeric operand compares per value: numerically where the term's
            // value is a number (so "1.0e03" equals "1000"), by string otherwise. Unlike the ordered
            // comparisons, equality never hard-fails on a present non-numeric value - a string term is
            // simply compared as text - so a numeric-looking operand cannot abort extraction.
            if ((Equals(op, FilterOperations.OP_EQUALS) || Equals(op, FilterOperations.OP_NOT_EQUALS)) &&
                OperandIsNumeric(spec))
            {
                var numericPredicate = spec.Predicate.MakePredicate(dataSchema, typeof(double));
                var stringPredicate = spec.Predicate.MakePredicate(dataSchema, typeof(string));
                return metadata =>
                {
                    var raw = column.GetValue(metadata) as string;
                    if (string.IsNullOrEmpty(raw))
                    {
                        // Absent or value-less term: keep the value operators' null semantics
                        // (equals -> no match, not-equals -> match).
                        return numericPredicate(null);
                    }
                    return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                        ? numericPredicate(number)
                        : stringPredicate(raw);
                };
            }

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
        /// True when the spec's operand parses as an invariant number. There is no stored type for these
        /// terms, so a numeric operand is what makes an equality comparison numeric (see the per-value
        /// dispatch in <see cref="CompileCvSpec"/>).
        /// </summary>
        private static bool OperandIsNumeric(FilterSpec spec)
        {
            var operand = spec.Predicate.InvariantOperandText;
            return operand != null &&
                   double.TryParse(operand, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        }

        /// <summary>
        /// Decides whether a CV/user-parameter spec is a numeric or a string comparison. There is no
        /// stored type for these terms, so the operator implies it: the ordered comparisons are numeric
        /// (and hard-fail on a non-numeric value); everything else here - "contains"/"starts with" and
        /// non-numeric-operand equality - compares as text. Numeric-operand equality is handled per value
        /// in <see cref="CompileCvSpec"/> before this is reached.
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

        /// <summary>
        /// True if any spec filters on a dynamic mzML CV/user-parameter column. Chromatogram
        /// extraction only captures the (otherwise-dropped) CV terms onto each spectrum when this is
        /// true, so the capture cost is paid only for documents that actually filter on them.
        /// </summary>
        public bool ReferencesCvColumns()
        {
            foreach (var clause in Clauses)
            {
                foreach (var filterSpec in clause.FilterSpecs)
                {
                    var column = SpectrumClassColumn.FindColumn(filterSpec.ColumnId);
                    if (column != null && SpectrumClassColumn.IsCvParamColumn(column))
                    {
                        return true;
                    }
                }
            }

            return false;
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
            // Normalize a human-authored filter first: rewrite CV/userParam column references to their
            // canonical encoded tokens (the generic grammar cannot carry them directly) and friendly
            // operator words to the symbols the grammar expects, so a transition list or command line can
            // name a CV term by "MS:1000505" and use readable operators like "isblank"/"greaterthan".
            filterString = NormalizeAuthoredFilterString(filterString);
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
            // expected form (column/operator/value with spaces, combined with "and"/"or") and lists the
            // operator vocabulary, since the accepted operator tokens are not otherwise discoverable.
            throw new FormatException(TextUtil.SpaceSeparate(
                string.Format(SpectraResources.SpectrumClassFilter_ParseFilterString_Invalid_spectrum_filter_format,
                    filterString),
                string.Format(SpectraResources.SpectrumClassFilter_ParseFilterString_Available_operators__0_,
                    OPERATOR_VOCABULARY)),
                firstError);
        }

        // A controlled-vocabulary accession: a letter prefix, a colon, then digits (e.g. "MS:1000505").
        private static readonly Regex CV_ACCESSION = new Regex(@"[A-Za-z]+:[0-9]+");
        private static readonly Regex CV_ACCESSION_ANCHORED = new Regex(@"\G[A-Za-z]+:[0-9]+");

        // Explicit marker for a userParam column reference. A userParam has no accession - its identity is
        // an arbitrary name with no distinctive pattern - so it cannot be recognized implicitly the way a
        // CV accession can; without the marker an unknown token stays an "unknown property" error, which
        // keeps typos of interpreted columns from silently resolving to a no-match userParam.
        private const string USER_PARAM_PREFIX = @"userParam:";

        // Case-insensitive friendly aliases for the filter-string operator symbols, so an authored spectrum
        // filter can use readable operator words (mirroring the UI names) instead of the terse or cryptic
        // symbols. Values are the canonical OpSymbol the generic grammar expects. Kept English (like the
        // "and"/"or" keywords) so a filter authored in one locale parses in another. The word-symbols that
        // the grammar already accepts are included so they match case-insensitively too.
        private static readonly IDictionary<string, string> OPERATOR_ALIASES =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { @"equals", @"=" },
                { @"notequals", @"<>" }, { @"notequal", @"<>" }, { @"doesnotequal", @"<>" },
                { @"greaterthan", @">" }, { @"greaterthanorequal", @">=" }, { @"greaterthanorequalto", @">=" },
                { @"lessthan", @"<" }, { @"lessthanorequal", @"<=" }, { @"lessthanorequalto", @"<=" },
                { @"contains", @"contains" }, { @"notcontains", @"notcontains" }, { @"doesnotcontain", @"notcontains" },
                { @"startswith", @"startswith" }, { @"notstartswith", @"notstartswith" },
                { @"isblank", @"isnullorblank" }, { @"isnotblank", @"isnotnullorblank" },
                { @"isnullorblank", @"isnullorblank" }, { @"isnotnullorblank", @"isnotnullorblank" },
                { @"isdeclared", @"isdeclared" }, { @"isnotdeclared", @"isnotdeclared" }
            };

        // The operator tokens listed in the parse-error message so the vocabulary is discoverable. The
        // readable blank/declared forms are shown rather than the "isnullorblank" symbols.
        private static readonly string OPERATOR_VOCABULARY = string.Join(@" ", new[]
        {
            @"=", @"<>", @">", @">=", @"<", @"<=",
            @"contains", @"notcontains", @"startswith", @"notstartswith",
            @"isblank", @"isnotblank", @"isdeclared", @"isnotdeclared"
        });

        /// <summary>
        /// Rewrites a human-authored filter into the tokens the generic grammar expects, before parsing, so
        /// a filter typed in a transition list or on the command line can be written readably. Two kinds of
        /// token are normalized, both only outside single-quoted operand text (which is copied verbatim):
        /// <para>Column references - a CV accession such as "MS:1000505", whether bare or embedded in a
        /// double-quoted caption like "base peak intensity (MS:1000505)" (any other text in the caption is
        /// ignored, since a CV term's identity is its accession alone); and a userParam named with the
        /// explicit "userParam:" marker, either bare ("userParam:vendorSetting") or inside a double-quoted
        /// caption for names with spaces ("userParam:vendor setting"). The generic grammar cannot carry
        /// these directly: a colon is not a legal PropertyPath character, and a quoted caption's spaces and
        /// parentheses are stripped before PropertyPath.Parse, which then rejects them.</para>
        /// <para>Operators - a friendly, case-insensitive operator word ("equals", "greaterthan",
        /// "isblank", ...) is rewritten to its canonical symbol, so the readable form the UI shows can be
        /// typed instead of the terse or cryptic symbol. Only recognized alias words are rewritten;
        /// anything else (column names, "and"/"or", operands) is copied unchanged.</para>
        /// </summary>
        private static string NormalizeAuthoredFilterString(string filterString)
        {
            if (string.IsNullOrEmpty(filterString))
            {
                return filterString;
            }

            var result = new StringBuilder(filterString.Length);
            int i = 0;
            while (i < filterString.Length)
            {
                char c = filterString[i];
                if (c == '\'')
                {
                    // Single-quoted operand: copy verbatim through the closing quote ('' escapes a quote).
                    result.Append(c);
                    i++;
                    while (i < filterString.Length)
                    {
                        char oc = filterString[i];
                        result.Append(oc);
                        i++;
                        if (oc == '\'')
                        {
                            if (i < filterString.Length && filterString[i] == '\'')
                            {
                                result.Append('\'');
                                i++;
                                continue;
                            }
                            break;
                        }
                    }
                    continue;
                }

                if (c == '"')
                {
                    // Double-quoted column caption: collapse to the canonical token when it carries an
                    // accession, otherwise leave it exactly as authored.
                    int start = i;
                    var inner = new StringBuilder();
                    i++;
                    bool closed = false;
                    while (i < filterString.Length)
                    {
                        char qc = filterString[i];
                        if (qc == '"')
                        {
                            i++;
                            if (i < filterString.Length && filterString[i] == '"')
                            {
                                inner.Append('"');
                                i++;
                                continue;
                            }
                            closed = true;
                            break;
                        }
                        inner.Append(qc);
                        i++;
                    }
                    if (closed && TryEncodeColumnReference(inner.ToString(), out var token))
                    {
                        result.Append(token);
                    }
                    else
                    {
                        result.Append(filterString, start, i - start);
                    }
                    continue;
                }

                // A bare userParam reference: the explicit marker followed by the name (up to the next
                // delimiter). The marker is checked before the accession rule so a userParam named like an
                // accession, e.g. "userParam:123", is a userParam and not read as a CV term.
                if (StartsWithUserParamMarker(filterString, i))
                {
                    int nameStart = i + USER_PARAM_PREFIX.Length;
                    int nameEnd = nameStart;
                    while (nameEnd < filterString.Length && !IsColumnReferenceDelimiter(filterString[nameEnd]))
                    {
                        nameEnd++;
                    }
                    result.Append(EncodeCvColumnToken(filterString.Substring(nameStart, nameEnd - nameStart)));
                    i = nameEnd;
                    continue;
                }

                // A bare accession can only be a column reference here: an operand is a number or is
                // single-quoted, so an unquoted "letters:digits" token is never operand text.
                var match = CV_ACCESSION_ANCHORED.Match(filterString, i);
                if (match.Success)
                {
                    result.Append(EncodeCvColumnToken(match.Value));
                    i += match.Length;
                    continue;
                }

                // A run of letters is either an operator (word symbol or friendly alias), a column name, or
                // "and"/"or". Only a recognized alias is rewritten to its symbol; everything else - column
                // names never collide with an alias, and operands are numbers or single-quoted - is copied
                // as-is. Read the whole run so a non-alias word is not re-scanned character by character.
                if (char.IsLetter(c))
                {
                    int wordEnd = i;
                    while (wordEnd < filterString.Length && char.IsLetter(filterString[wordEnd]))
                    {
                        wordEnd++;
                    }
                    var word = filterString.Substring(i, wordEnd - i);
                    result.Append(OPERATOR_ALIASES.TryGetValue(word, out var symbol) ? symbol : word);
                    i = wordEnd;
                    continue;
                }

                result.Append(c);
                i++;
            }

            return result.ToString();
        }

        private static bool StartsWithUserParamMarker(string s, int i)
        {
            return i + USER_PARAM_PREFIX.Length <= s.Length &&
                   string.Compare(s, i, USER_PARAM_PREFIX, 0, USER_PARAM_PREFIX.Length,
                       StringComparison.OrdinalIgnoreCase) == 0;
        }

        // Characters that end an unquoted column reference: whitespace, quotes, parentheses, and the
        // comparison-operator characters (so a no-space form like "userParam:foo>5" still splits).
        private static bool IsColumnReferenceDelimiter(char c)
        {
            return char.IsWhiteSpace(c) || @"""'()=<>".IndexOf(c) >= 0;
        }

        // The canonical encoded column name for a CV/userParam identity (a CV accession encodes as
        // "cvid...", any other name as the "cvup..." userParam form; see SpectrumClassColumn).
        private static string EncodeCvColumnToken(string identity)
        {
            return SpectrumClassColumn.CvParam(identity, null, false).ColumnName;
        }

        /// <summary>
        /// If <paramref name="text"/> is a CV/userParam column caption - a userParam marked with
        /// "userParam:", or a caption containing a CV accession (e.g. "MS:1000505") - sets
        /// <paramref name="token"/> to that term's canonical encoded column name and returns true.
        /// </summary>
        private static bool TryEncodeColumnReference(string text, out string token)
        {
            token = null;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            if (text.StartsWith(USER_PARAM_PREFIX, StringComparison.OrdinalIgnoreCase))
            {
                token = EncodeCvColumnToken(text.Substring(USER_PARAM_PREFIX.Length));
                return true;
            }
            var match = CV_ACCESSION.Match(text);
            if (!match.Success)
            {
                return false;
            }
            token = EncodeCvColumnToken(match.Value);
            return true;
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
