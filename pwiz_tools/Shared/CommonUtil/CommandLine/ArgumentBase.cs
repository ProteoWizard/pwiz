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
    /// What a property's value is, which decides the control a settings page shows for it and
    /// how the value is checked. See <see cref="ArgumentBase.PropertyType"/>.
    /// </summary>
    public enum ArgumentPropertyType
    {
        /// <summary>A switch with no value: present or absent.</summary>
        flag,
        /// <summary>Free text.</summary>
        text,
        /// <summary>A file or folder path.</summary>
        path,
        /// <summary>A whole number, within <see cref="ArgumentBase.MinValue"/> and <see cref="ArgumentBase.MaxValue"/> when set.</summary>
        integer,
        /// <summary>A decimal number, within <see cref="ArgumentBase.MinValue"/> and <see cref="ArgumentBase.MaxValue"/> when set.</summary>
        number,
        /// <summary>One of the declaration's listed values.</summary>
        list,
    }

    /// <summary>
    /// The non-generic core of a declarative command-line argument: its name, the value
    /// example/allowed values used to render help, and the flags controlling display and
    /// validation. The strongly-typed value processor lives on <see cref="Argument{TContext}"/>.
    /// </summary>
    public abstract class ArgumentBase
    {
        /// <summary>The standard long-name prefix, the one <see cref="CommandLineGrammar.STRICT"/> uses.</summary>
        public const string ARG_PREFIX = "--";

        protected ArgumentBase(string name, Func<string> valueExample, string[] fixedValues, Func<string[]> dynamicValues)
        {
            Name = name;
            ValueExample = valueExample;
            _fixedValues = fixedValues;
            _dynamicValues = dynamicValues;
        }

        /// <summary>
        /// A declaration with everything <paramref name="template"/> declares about its name and
        /// value - the name, the value example, the listed and accepted values, whether the value
        /// is optional or variadic - for a host that includes another tool's declarations in its
        /// own parse (Osprey presents CarafeSharp's library options). The short name and the
        /// applies-to text are the host's to set: both are spelled in the host's grammar and the
        /// short names of two tools collide.
        /// </summary>
        protected ArgumentBase(ArgumentBase template)
            : this(template.Name, template.ValueExample, template._fixedValues, template._dynamicValues)
        {
            WrapValue = template.WrapValue;
            OptionalValue = template.OptionalValue;
            OptionalValueFilter = template.OptionalValueFilter;
            Variadic = template.Variadic;
            MissingValueMessage = template.MissingValueMessage;
            InternalUse = template.InternalUse;
            HasValueChecking = template.HasValueChecking;
            AcceptedValues = template.AcceptedValues;
            AcceptsValue = template.AcceptsValue;
            PropertyName = template.PropertyName;
            PropertyType = template.PropertyType;
            DefaultValue = template.DefaultValue;
            MinValue = template.MinValue;
            MaxValue = template.MaxValue;
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
        /// <summary>
        /// With <see cref="OptionalValue"/> under a grammar whose values may follow as the next
        /// token: decides whether that token is this argument's value or the next thing on the
        /// command line. Null takes any token that is not an option. Osprey's
        /// <c>--parallel-files [N]</c> takes only a non-negative integer, so a file name after
        /// it stays a file name.
        /// </summary>
        public Func<string, bool> OptionalValueFilter { get; set; }
        /// <summary>
        /// Consumes every value up to the next option (<c>-i a.mzML b.mzML</c>), each handed to
        /// the processor as its own value. Only meaningful under a grammar whose values may
        /// follow as the next token.
        /// </summary>
        public bool Variadic { get; set; }
        /// <summary>
        /// The missing-value error for this argument, given the argument as typed, when the
        /// provider's general text is not enough: Osprey's <c>--task</c> lists the task names,
        /// because that error is the user's only guide to them. Null uses the provider's.
        /// </summary>
        public Func<string, string> MissingValueMessage { get; set; }
        public bool InternalUse { get; set; }
        public bool HasValueChecking { get; set; }  // Set to avoid default checking against values listed for documentation
        /// <summary>
        /// Values accepted in addition to <see cref="Values"/> without being listed in help or errors,
        /// e.g. the invariant names for an argument whose values are shown localized.
        /// </summary>
        public Func<string[]> AcceptedValues { get; set; }
        /// <summary>
        /// A test for values accepted beyond <see cref="Values"/> and <see cref="AcceptedValues"/>
        /// that no list can name, e.g. a culture accepted because its parent is known. An argument
        /// that checks its value when parsed (<see cref="HasValueChecking"/>) should test with the
        /// same rule here, so a token built with <c>+</c> is refused exactly when the parser would
        /// refuse it.
        /// </summary>
        public Func<string, bool> AcceptsValue { get; set; }

        /// <summary>
        /// The name of the argument as a property on a settings page (the label of a grid row),
        /// read on every call so it follows the UI culture. Null for an argument that is no
        /// property: an input, an output, a command (help, version) or an internal switch. An
        /// argument with a name is exposed to a host that presents the tool's settings (Skyline's
        /// Additional settings grid), with <see cref="PropertyType"/> deciding its control and
        /// validation and <see cref="DefaultValue"/>, <see cref="MinValue"/> and
        /// <see cref="MaxValue"/> its default and range.
        /// </summary>
        public virtual Func<string> PropertyName { get; set; }

        /// <summary>
        /// The kind of value the property takes, when the declaration does not say: a declaration
        /// without a value is a <see cref="ArgumentPropertyType.flag"/>, one with listed values a
        /// <see cref="ArgumentPropertyType.list"/>, and any other takes text. Set for a number,
        /// an integer or a path, which the declaration cannot tell from text.
        /// </summary>
        public ArgumentPropertyType? PropertyType { get; set; }

        /// <summary>The property's type: <see cref="PropertyType"/>, or the one the declaration implies.</summary>
        public ArgumentPropertyType EffectivePropertyType
        {
            get
            {
                if (PropertyType.HasValue)
                    return PropertyType.Value;
                if (!TakesValue)
                    return ArgumentPropertyType.flag;
                return Values != null ? ArgumentPropertyType.list : ArgumentPropertyType.text;
            }
        }

        /// <summary>
        /// The value the tool uses when the argument is absent, as the command line would spell
        /// it (a number in the invariant culture), or null when the tool has none to name: a
        /// path, or a value derived from others at run time. Read on every call, since a default
        /// may depend on the computer (the thread count).
        /// </summary>
        public Func<string> DefaultValue { get; set; }

        /// <summary>The smallest value a number or integer property accepts, or null for no bound.</summary>
        public double? MinValue { get; set; }

        /// <summary>The largest value a number or integer property accepts, or null for no bound.</summary>
        public double? MaxValue { get; set; }

        /// <summary>
        /// The argument as typed and as help shows it: the installed grammar's
        /// <see cref="CommandLineGrammar.Prefix"/> and the name (<c>--input</c>, <c>--db</c>).
        /// </summary>
        public string ArgumentText
        {
            get { return ArgUsage.Grammar.Prefix + Name; }
        }

        /// <summary>The short spelling (<c>-i</c> for <c>--input</c>), or null when the
        /// argument has no <see cref="ShortName"/> or the grammar no short prefix. The one place
        /// that spelling is built, so the usage text, the parser and a test all agree on it.</summary>
        public string ShortArgumentText
        {
            get
            {
                string shortPrefix = ArgUsage.Grammar.ShortPrefix;
                return ShortName != null && shortPrefix != null ? shortPrefix + ShortName : null;
            }
        }

        /// <summary>
        /// True when the parser reads a value for this argument: it declares one
        /// (<see cref="ValueExample"/>). An argument that declares none is a flag and never
        /// takes a value, <see cref="OptionalValue"/> or not: Skyline declares several such
        /// flags with OptionalValue set, and its tests pin that a value given to one is refused.
        /// </summary>
        public bool TakesValue
        {
            get { return ValueExample != null; }
        }

        /// <summary>
        /// The argument and a value as one command-line token, joined by the host's
        /// <see cref="ArgUsage.ArgumentValueSeparator"/> - the same process-wide setting the
        /// usage text renders with, so a token built here always has the shape the host's
        /// own documentation shows (Skyline: <c>--in=path</c>; Osprey: <c>--threads 8</c>).
        /// A value list (<see cref="Values"/> and <see cref="AcceptedValues"/>) is always enforced here, <see cref="HasValueChecking"/> or not:
        /// Skyline's ConsoleArgumentInvalidValuesTest pins that every listed argument refuses
        /// an unlisted value at build time. A host whose parser accepts an unlisted alias
        /// passes that value as its own token instead of through this builder.
        /// </summary>
        public string GetArgumentTextWithValue(string value)
        {
            if (ValueExample == null)
                throw new ValueUnexpectedException(this);
            else if (!IsValidValue(value))
                throw new ValueInvalidException(this, value, ValuesForError);

            return ArgumentText + ArgUsage.ArgumentValueSeparator + value;
        }

        /// <summary>
        /// True if the value is one of <see cref="Values"/> or <see cref="AcceptedValues"/>, ignoring case,
        /// or passes <see cref="AcceptsValue"/>, or if the argument has none of them and so nothing to
        /// check the value against.
        /// </summary>
        public bool IsValidValue(string value)
        {
            if (Values == null && AcceptedValues == null && AcceptsValue == null)
                return true;
            if (Values != null && Values.Any(v => string.Equals(v, value, StringComparison.CurrentCultureIgnoreCase)))
                return true;
            if (AcceptedValues != null && AcceptedValues().Any(v => string.Equals(v, value, StringComparison.OrdinalIgnoreCase)))
                return true;
            return AcceptsValue != null && AcceptsValue(value);
        }

        /// <summary>
        /// The values to name in an invalid value message: those listed for documentation, or the
        /// accepted values for an argument that lists none.
        /// </summary>
        public string[] ValuesForError
        {
            get { return Values ?? AcceptedValues?.Invoke() ?? Array.Empty<string>(); }
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
                var retValue = (ShortArgumentText != null ? ShortArgumentText + @", " : string.Empty) + ArgumentText;
                if (ValueExample != null)
                {
                    string separator = ArgUsage.ArgumentValueSeparator;
                    string example = (WrapValue ? Environment.NewLine : string.Empty) + ValueExample();
                    string valueText;
                    if (!OptionalValue)
                        valueText = separator + example;
                    else if (string.IsNullOrWhiteSpace(separator))
                        valueText = separator + '[' + example + ']';   // --name [<value>]
                    else
                        valueText = '[' + separator + example + ']';   // --name[=value]
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

        /// <summary>
        /// One token in the strict form, <c>--name</c> or <c>--name=value</c>, as a pair, or
        /// <see cref="NameValuePair.EMPTY"/> for a token without the prefix. The parser does
        /// its own matching (<see cref="CommandLineParser{TContext}"/>); this is for a test that
        /// builds one token and checks it against one argument.
        /// </summary>
        public static NameValuePair Parse(string arg)
        {
            string prefix = ArgUsage.Grammar.Prefix;
            if (!arg.StartsWith(prefix, StringComparison.Ordinal))
                return NameValuePair.EMPTY;

            // Split on the same separator GetArgumentTextWithValue joins with, so the framework
            // round-trips its own tokens whatever the host's grammar. Skyline's is "=".
            string name, value = null;
            arg = arg.Substring(prefix.Length);
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
