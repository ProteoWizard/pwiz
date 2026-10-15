/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace pwiz.Common.CommandLine
{
    /// <summary>
    /// A named, renderable group of related <see cref="Argument{TContext}"/> declarations.
    /// Renders itself as an ascii/unicode table or HTML, and carries optional group-level
    /// validation and inter-argument dependencies evaluated by the host controller.
    /// </summary>
    public class ArgumentGroup<TContext> : IUsageBlock
    {
        private readonly Func<string> _getTitle;

        public ArgumentGroup(Func<string> getTitle, bool showHeaders, params Argument<TContext>[] args)
        {
            _getTitle = getTitle;
            Args = args;
            ShowHeaders = showHeaders;
            Dependencies = new Dictionary<Argument<TContext>, Argument<TContext>>();
        }

        public string Title { get { return _getTitle(); } }
        public Func<string> Preamble { get; set; }
        public Func<string> Postamble { get; set; }
        public IList<Argument<TContext>> Args { get; private set; }
        public bool ShowHeaders { get; private set; }

        public IDictionary<Argument<TContext>, Argument<TContext>> Dependencies { get; set; }

        public Func<TContext, bool> Validate { get; set; }

        public int? LeftColumnWidth { get; set; }

        public bool IncludeInUsage
        {
            get { return !Args.All(a => a.InternalUse); }
        }

        public override string ToString()
        {
            return ToString(78, null, true);
        }

        public string ToString(int width, string formatType)
        {
            return ToString(width, formatType, false);
        }

        private string ToString(int width, string formatType, bool forDebugging)
        {
            if (!IncludeInUsage && !forDebugging)
                return string.Empty;

            var ct = new ConsoleTable
            {
                Title = Title,
                Borders = formatType != ArgUsage.FORMAT_NO_BORDERS,
                Ascii = formatType == ArgUsage.FORMAT_ASCII
            };
            if (Preamble != null)
                ct.Preamble = Preamble();
            if (Postamble != null)
                ct.Postamble = Postamble();
            ct.Width = width;

            bool hasAppliesTo = Args.Any(a => a.AppliesTo != null);
            if (ShowHeaders)
            {
                if (hasAppliesTo)
                    ct.SetHeaders(ArgUsage.Provider.AppliesToHeader,
                        ArgUsage.Provider.ArgumentHeader,
                        ArgUsage.Provider.DescriptionHeader);
                else
                    ct.SetHeaders(ArgUsage.Provider.ArgumentHeader,
                        ArgUsage.Provider.DescriptionHeader);
            }

            var usageArgs = Args.Where(a => !a.InternalUse).ToList();
            foreach (var commandArg in usageArgs)
            {
                if (hasAppliesTo)
                    ct.AddRow(commandArg.AppliesTo ?? string.Empty, commandArg.ArgumentDescription, commandArg.Description);
                else
                    ct.AddRow(commandArg.ArgumentDescription, commandArg.Description);
            }

            int GetIdealArgumentWidth(Argument<TContext> a)
            {
                int maxWidth = width / 2;

                // if value is on a separate line or the argument by itself is over the max width, just use argument plus =
                if (a.WrapValue || a.ArgumentText.Length + 2 > maxWidth)
                    return a.ArgumentText.Length + 2;

                // if value is included on single line, set a reasonable limit
                return Math.Min(maxWidth, a.ArgumentDescription.Length);
            }

            if (!hasAppliesTo)
            {
                int minLines = int.MaxValue;
                int bestWidth = 0;
                for (int leftColumnWidth = usageArgs.Max(GetIdealArgumentWidth); leftColumnWidth < width - 10; leftColumnWidth += 5)
                {
                    ct.Widths = new[] { leftColumnWidth, width - leftColumnWidth - 3 };   // 3 borders
                    int numLines = ct.ToString().Split(new [] { Environment.NewLine }, StringSplitOptions.None).Length;
                    if (bestWidth == 0 || numLines <= minLines)
                    {
                        minLines = numLines;
                        bestWidth = leftColumnWidth;
                    }
                }
                ct.Widths = new[] { bestWidth, width - bestWidth - 3 };   // 3 borders
            }

            return ct.ToString();
        }

        public string ToHtmlString()
        {
            if (!IncludeInUsage)
                return string.Empty;

            // ReSharper disable LocalizableElement
            var sb = new StringBuilder();
            sb.AppendLine("<div class=\"RowType\">" + UsageHtmlEncoder.Encode(Title) + "</div>");
            if (Preamble != null)
                sb.AppendLine("<p>" + Preamble() + "</p>");
            sb.AppendLine("<table>");
            bool hasAppliesTo = Args.Any(a => a.AppliesTo != null);
            if (ShowHeaders)
            {
                sb.Append("<tr>");

                if (hasAppliesTo)
                    sb.Append("<th>").Append(ArgUsage.Provider.AppliesToHeader).Append("</th>");
                sb.Append("<th>").Append(ArgUsage.Provider.ArgumentHeader).Append("</th>");
                sb.Append("<th>").Append(ArgUsage.Provider.DescriptionHeader).Append("</th>");

                sb.AppendLine("</tr>");
            }
            foreach (var commandArg in Args.Where(a => !a.InternalUse))
            {
                sb.Append("<tr>");

                if (hasAppliesTo)
                    sb.Append("<td>").Append(commandArg.AppliesTo != null ? UsageHtmlEncoder.Encode(commandArg.AppliesTo) : "&nbsp;").Append("</td>");
                string argDescription = UsageHtmlEncoder.Encode(commandArg.ArgumentDescription);
                if (!argDescription.Contains('|') && !ArgUsage.IsWrappableListType(commandArg.ValueExample))
                    argDescription = argDescription.Replace(" ", "&nbsp;");
                argDescription = argDescription.Replace(Environment.NewLine, "<br/>");
                sb.Append("<td>").Append(argDescription).Append("</td>");
                sb.Append("<td>").Append(UsageHtmlEncoder.Encode(commandArg.Description)).Append("</td>");

                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</table>");
            if (Postamble != null)
                sb.AppendLine("<p>" + Postamble() + "</p>");

            return sb.ToString();
            // ReSharper restore LocalizableElement
        }
    }

    /// <summary>
    /// Non-generic helper for argument-table HTML encoding. Kept out of the generic
    /// <see cref="ArgumentGroup{TContext}"/> so its compiled regex is shared across all
    /// context types rather than duplicated per generic instantiation.
    /// </summary>
    internal static class UsageHtmlEncoder
    {
        // Regular expression for an argument: a hyphen surrounded by zero or more word characters
        // (i.e. letters, numbers or UnicodeCategory.ConnectorPunctuation) or hyphens
        private static readonly Regex REGEX_ARGUMENT = new Regex(@"[\w-]*-[\w-]*",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        /// <summary>
        /// HTML encodes the string.
        /// Also, puts &lt;nobr> tags around everything that contains a hyphen so that argument names do not get broken across lines.
        /// </summary>
        public static string Encode(string str)
        {
            str = str ?? string.Empty;
            var result = new StringBuilder();
            int charIndex = 0;
            var matchCollection = REGEX_ARGUMENT.Matches(str);
            foreach (Match match in matchCollection)
            {
                result.Append(ArgUsage.HtmlEncode(str.Substring(charIndex, match.Index - charIndex)));
                // ReSharper disable LocalizableElement
                result.Append("<nobr>");
                result.Append(ArgUsage.HtmlEncode(match.Value));
                result.Append("</nobr>");
                // ReSharper restore LocalizableElement
                charIndex = match.Index + match.Length;
            }

            result.Append(ArgUsage.HtmlEncode(str.Substring(charIndex)));
            return result.ToString();
        }
    }
}
