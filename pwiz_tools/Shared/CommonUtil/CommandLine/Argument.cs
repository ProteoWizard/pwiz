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

namespace pwiz.Common.CommandLine
{
    /// <summary>
    /// A declarative command-line argument that processes its value against a host
    /// controller of type <typeparamref name="TContext"/>. The host registers one of
    /// these per recognized argument, embedding the parse/apply logic in <see cref="ProcessValue"/>.
    /// </summary>
    public class Argument<TContext> : ArgumentBase
    {
        public Argument(string name, Func<TContext, NameValuePair, bool> processValue)
            : base(name, null, null, null)
        {
            ProcessValue = processValue;
        }

        public Argument(string name, Action<TContext, NameValuePair> processValue)
            : this(name, (c, p) =>
            {
                processValue(c, p);
                return true;
            })
        {
        }

        public Argument(string name, Func<string> valueExample, Func<TContext, NameValuePair, bool> processValue)
            : base(name, valueExample, null, null)
        {
            ProcessValue = processValue;
        }

        public Argument(string name, Func<string> valueExample, Action<TContext, NameValuePair> processValue)
            : this(name, valueExample, (c, p) =>
            {
                processValue(c, p);
                return true;
            })
        {
        }

        public Argument(string name, string[] values, Func<TContext, NameValuePair, bool> processValue)
            : base(name, () => ValuesToExample(values), values, null)
        {
            ProcessValue = processValue;
        }

        public Argument(string name, string[] values, Action<TContext, NameValuePair> processValue)
            : this(name, values, (c, p) =>
            {
                processValue(c, p);
                return true;
            })
        {
        }

        public Argument(string name, Func<string[]> values, Func<TContext, NameValuePair, bool> processValue)
            : base(name, () => ValuesToExample(values()), null, values)
        {
            ProcessValue = processValue;
        }

        public Argument(string name, Func<string[]> values, Action<TContext, NameValuePair> processValue)
            : this(name, values, (c, p) =>
            {
                processValue(c, p);
                return true;
            })
        {
        }

        public Func<TContext, NameValuePair, bool> ProcessValue;
    }
}
