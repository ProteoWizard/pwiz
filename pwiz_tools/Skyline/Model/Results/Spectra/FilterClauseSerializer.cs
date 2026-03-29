/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.6) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Filtering;
using Sprache;

namespace pwiz.Skyline.Model.Results.Spectra
{
    /// <summary>
    /// Handles parsing a filter string into a list of <see cref="FilterClause"/> and
    /// serializing a list of <see cref="FilterClause"/> back to a filter string.
    /// </summary>
    public class FilterClauseSerializer
    {
        private readonly ColumnDescriptor _rootColumn;

        public FilterClauseSerializer(ColumnDescriptor rootColumn)
        {
            _rootColumn = rootColumn;
        }

        public CultureInfo CultureInfo
        {
            get
            {
                return _rootColumn.DataSchema.DataSchemaLocalizer.FormatProvider;
            }
        }

        /// <summary>
        /// Returns a list of filter clauses as a human-readable string that can be parsed back.
        /// Format: FilterClauses are separated by " or ", FilterSpecs within a clause by " and ".
        /// Multiple specs get parentheses: (spec1) and (spec2)
        /// Multiple clauses get parentheses: (clause1) or (clause2)
        /// </summary>
        public string ToFilterString(IList<FilterClause> clauses)
        {
            if (clauses == null || clauses.Count == 0)
            {
                return string.Empty;
            }

            if (clauses.Count == 1)
            {
                return FormatClause(clauses[0]);
            }

            return string.Join(@" or ", clauses.Select(c => @"(" + FormatClause(c) + @")"));
        }

        /// <summary>
        /// Parses a filter string into a list of <see cref="FilterClause"/>.
        /// </summary>
        public List<FilterClause> ParseFilterString(string filterString)
        {
            if (string.IsNullOrWhiteSpace(filterString))
            {
                return new List<FilterClause>();
            }

            var result = CreateFilterParser().TryParse(filterString);
            if (!result.WasSuccessful)
            {
                throw new FormatException(string.Format(
                    SpectraResources.SpectrumClassFilter_ParseFilterString_Invalid_filter_string___0__, filterString));
            }
            return result.Value;
        }

        private string FormatClause(FilterClause clause)
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

        private string FormatFilterSpec(FilterSpec spec)
        {
            var sb = new StringBuilder();
            sb.Append(QuoteColumnIfNeeded(spec.Column));
            sb.Append(@" ");
            sb.Append(spec.Operation.OpSymbol);
            if (spec.Operation.HasOperand())
            {
                sb.Append(@" ");
                sb.Append(FormatOperandTokens(GetOperandTokens(spec)));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Converts a <see cref="FilterSpec"/>'s operand to a list of string tokens
        /// by resolving its column and using <see cref="IFilterHandler.OperandToTokens"/>.
        /// Falls back to splitting the invariant text if the column cannot be resolved.
        /// </summary>
        private IList<string> GetOperandTokens(FilterSpec spec)
        {
            var column = FilterClause.FindColumn(_rootColumn, spec.ColumnId);
            if (column == null)
            {
                return new[] { spec.Predicate.InvariantOperandText ?? string.Empty};
            }

            return column.GetFilterHandler().OperandToTokens(spec.Operation, CultureInfo, spec.Predicate.GetOperandValue(column));
        }

        /// <summary>
        /// Converts a list of parsed string tokens into the invariant operand text for
        /// <see cref="FilterPredicate"/> by resolving the column and using
        /// <see cref="IFilterHandler.ParseOperandTokens"/> and <see cref="IFilterHandler.OperandToString"/>.
        /// Falls back to joining the tokens if the column cannot be resolved.
        /// </summary>
        private string TokensToInvariantText(PropertyPath columnId, IFilterOperation operation, IList<string> tokens)
        {
            var column = FilterClause.FindColumn(_rootColumn, columnId);
            if (column != null)
            {
                var handler = column.GetFilterHandler();
                var operand = handler.ParseOperandTokens(operation, CultureInfo, tokens);
                return handler.SerializeOperand(operation, operand);
            }
            return string.Join(@", ", tokens);
        }

        /// <summary>
        /// Formats a list of string tokens into filter string syntax.
        /// Numbers are unquoted, strings are single-quoted.
        /// Multiple values use square bracket syntax.
        /// </summary>
        private static string FormatOperandTokens(IList<string> tokens)
        {
            if (tokens.Count == 1)
            {
                return FormatSingleToken(tokens[0]);
            }
            return @"[" + string.Join(@", ", tokens.Select(FormatSingleToken)) + @"]";
        }

        /// <summary>
        /// Formats a single token value for filter string syntax.
        /// Values that look like numbers are unquoted; others are single-quoted.
        /// </summary>
        private static string FormatSingleToken(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return @"''";
            }
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                return value;
            }
            return @"'" + value.Replace(@"'", @"''") + @"'";
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

        private Parser<List<FilterClause>> CreateFilterParser()
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

            // Single operand token (string literal or number text)
            var singleToken = singleQuotedString.Or(unquotedNumber);

            // Array of tokens: [value1, value2, ...]
            var arrayTokens = Parse.Char('[')
                .Then(_ => ws)
                .Then(_ => singleToken.DelimitedBy(ws.Then(_ => Parse.Char(',')).Then(_ => ws)))
                .Then(values => ws.Then(_ => Parse.Char(']'))
                    .Return((IList<string>)values.ToList()));

            // Operand: single token (as one-element list) or array
            var operandTokens = arrayTokens.Or(singleToken.Select(s => (IList<string>)new List<string> { s }));

            // Operator names
            var opSymbols = FilterOperations.ListOperations()
                .Where(op => !string.IsNullOrEmpty(op.OpSymbol))
                .Select(op => op.OpSymbol)
                .OrderByDescending(name => name.Length)
                .ToList();

            // Operator parser - try each operator name
            var operatorParser = opSymbols
                .Select(name => Parse.String(name).Text())
                .Aggregate((a, b) => a.Or(b));

            // FilterSpec: identifier operator operand?
            var filterSpec = identifier
                .Then(column => ws.Then(_ => operatorParser)
                    .Then(opSymbol =>
                    {
                        var op = FilterOperations.GetOperationBySymbol(opSymbol);
                        var columnId = PropertyPath.Parse(column);
                        if (!op.HasOperand())
                        {
                            return Parse.Return(new FilterSpec(columnId,
                                new FilterPredicate(op, null)));
                        }
                        return ws.Then(_ => operandTokens)
                            .Select(tokens => new FilterSpec(columnId,
                                new FilterPredicate(op, TokensToInvariantText(columnId, op, tokens))));
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
                    .Select(rest => new[] { first }.Concat(rest).ToList()))
                .End();
        }
    }
}
