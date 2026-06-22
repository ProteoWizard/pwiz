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

        public string GetArgumentTextWithValue(string value)
        {
            if (ValueExample == null)
                throw new ValueUnexpectedException(this);
            else if (Values != null && !Values.Any(v => v.Equals(value, StringComparison.CurrentCultureIgnoreCase)))
                throw new ValueInvalidException(this, value, Values);

            return ArgumentText + '=' + value;
        }

        public static string operator +(ArgumentBase arg, string value)
        {
            return arg.GetArgumentTextWithValue(value);
        }

        public static implicit operator string(ArgumentBase arg)
        {
            return arg.ArgumentText;
        }

        public string ArgumentDescription
        {
            get
            {
                var retValue = ArgumentText;
                if (ValueExample != null)
                {
                    var valueText = '=' + (WrapValue ? Environment.NewLine : string.Empty) + ValueExample();
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

            string name, value = null;
            arg = arg.Substring(2);
            int indexEqualsSign = arg.IndexOf('=');
            if (indexEqualsSign >= 0)
            {
                name = arg.Substring(0, indexEqualsSign);
                value = arg.Substring(indexEqualsSign + 1);
            }
            else
            {
                name = arg;
            }
            return new NameValuePair(name, value);
        }
    }
}
