/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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

namespace pwiz.Common.CommandLine
{
    /// <summary>
    /// The non-generic core of a declarative command-line argument: its name, the value
    /// example/allowed values used to render help, and the flags controlling display and
    /// validation. The strongly-typed value processor lives on <see cref="Argument{TContext}"/>.
    /// </summary>
    public abstract class ArgumentBase
    {
        public const string ARG_PREFIX = "--";

        protected ArgumentBase(string name, Func<string> valueExample, string[] fixedValues, Func<string[]> dynamicValues)
        {
            Name = name;
            ValueExample = valueExample;
            _fixedValues = fixedValues;
            _dynamicValues = dynamicValues;
        }

        private readonly string[] _fixedValues;
        private readonly Func<string[]> _dynamicValues;

        public string Name { get; private set; }
        // Optional short alias (e.g. "i" for "--input", surfaced as "-i"). Used by hosts whose
        // tokenizer accepts single-dash short flags; the long Name remains the canonical identifier.
        public string ShortName { get; set; }
        public string AppliesTo { get; set; }
        public string Description
        {
            get { return ArgUsage.Provider.GetDescription(Name); }
        }
        public Func<string> ValueExample { get; private set; }
        public string[] Values
        {
            get
            {
                return _dynamicValues?.Invoke() ?? _fixedValues;
            }
        }
        public bool WrapValue { get; set; }
        public bool OptionalValue { get; set; }
        public bool InternalUse { get; set; }
        public bool HasValueChecking { get; set; }  // Set to avoid default checking against values listed for documentation

        public string ArgumentText
        {
            get { return ARG_PREFIX + Name; }
        }

        /// <summary>The short spelling (<c>-i</c> for <c>--input</c>), or null when the
        /// argument has no <see cref="ShortName"/>. The one place that spelling is built, so
        /// the usage text, a host's tokenizer and a test all agree on it.</summary>
        public string ShortArgumentText
        {
            get { return ShortName != null ? @"-" + ShortName : null; }
        }

        /// <summary>
        /// The argument and a value as one command-line token, joined by the host's
        /// <see cref="ArgUsage.ArgumentValueSeparator"/> - the same process-wide setting the
        /// usage text renders with, so a token built here always has the shape the host's
        /// own documentation shows (Skyline: <c>--in=path</c>; Osprey: <c>--threads 8</c>).
        /// A fixed value list is enforced here exactly as <see cref="NameValuePair.IsMatch"/>
        /// enforces it at parse time: not at all when <see cref="HasValueChecking"/> says the
        /// argument checks its own values (aliases, case folding, warn-and-default).
        /// </summary>
        public string GetArgumentTextWithValue(string value)
        {
            if (ValueExample == null)
                throw new ValueUnexpectedException(this);
            else if (Values != null && !HasValueChecking && !Values.Any(v => v.Equals(value, StringComparison.CurrentCultureIgnoreCase)))
                throw new ValueInvalidException(this, value, Values);

            return ArgumentText + ArgUsage.ArgumentValueSeparator + value;
        }

        /// <summary>
        /// <c>ARG_THREADS + 8</c> reads as the command line does. A non-string value is
        /// formatted with <see cref="ArgUsage.ValueFormatProvider"/>, i.e. the way the host
        /// parses, so a test that passes a number follows the host's culture rules instead
        /// of hard-coding a decimal separator. Taking <see cref="object"/> rather than
        /// <see cref="string"/> also closes a trap: with a string-only operator,
        /// <c>ARG_THREADS + 8</c> still compiled, through the implicit string conversion and
        /// the predefined <c>string + object</c>, and silently produced <c>--threads8</c>.
        /// </summary>
        public static string operator +(ArgumentBase arg, object value)
        {
            // A null value would render as a trailing separator and parse as an EMPTY value,
            // which a path option records without complaint. Nothing means that; fail here.
            if (value == null)
                throw new ArgumentNullException(nameof(value), @"An argument value cannot be null");
            // An argument's ToString() is its usage line, so ARG_A + ARG_B would render
            // "--a=--b <value>". Fail here instead of at parse time.
            if (value is ArgumentBase)
                throw new ArgumentException(@"An argument cannot be the value of another argument");

            var provider = ArgUsage.ValueFormatProvider ?? CultureInfo.CurrentCulture;
            return arg.GetArgumentTextWithValue(Convert.ToString(value, provider));
        }

        public static implicit operator string(ArgumentBase arg)
        {
            return arg.ArgumentText;
        }

        public string ArgumentDescription
        {
            get
            {
                // Hosts that set a ShortName (e.g. Osprey's -i) get it shown alongside the
                // long form: "-i, --input ...". Skyline leaves ShortName null, so this prefix is
                // empty and the rendered text is unchanged.
                var retValue = (ShortName != null ? ShortArgumentText + @", " : string.Empty) + ArgumentText;
                if (ValueExample != null)
                {
                    var valueText = ArgUsage.ArgumentValueSeparator + (WrapValue ? Environment.NewLine : string.Empty) + ValueExample();
                    if (OptionalValue)
                        valueText = '[' + valueText + ']';
                    retValue += valueText;
                }
                return retValue;
            }
        }

        public override string ToString()
        {
            return ArgumentDescription;
        }

        public static string ValuesToExample(IEnumerable<string> options)
        {
            var sb = new StringBuilder();
            sb.Append('<');
            foreach (var o in options)
            {
                if (sb.Length > 1)
                    sb.Append(@" | ");
                sb.Append(o);
            }
            sb.Append('>');
            return sb.ToString();
        }

        public static string ValuesToExample(params string[] options)
        {
            return ValuesToExample((IEnumerable<string>) options);
        }

        public static NameValuePair Parse(string arg)
        {
            if (!arg.StartsWith(ARG_PREFIX))
                return NameValuePair.EMPTY;

            // Split on the same separator GetArgumentTextWithValue joins with, so the framework
            // round-trips its own tokens whatever the host's grammar. Skyline's is "=".
            string name, value = null;
            arg = arg.Substring(2);
            string separator = ArgUsage.ArgumentValueSeparator;
            int indexSeparator = arg.IndexOf(separator, StringComparison.Ordinal);
            if (indexSeparator >= 0)
            {
                name = arg.Substring(0, indexSeparator);
                value = arg.Substring(indexSeparator + separator.Length);
            }
            else
            {
                name = arg;
            }
            return new NameValuePair(name, value);
        }
    }
}
