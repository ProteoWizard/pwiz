/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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

namespace pwiz.Common.CommandLine
{
    /// <summary>
    /// The one tokenizer: turns a command line into calls on the declared
    /// <see cref="Argument{TContext}"/> instances, under the <see cref="CommandLineGrammar"/>
    /// in force (<see cref="ArgUsage.Grammar"/>). Skyline, Osprey and CarafeSharp declare their
    /// arguments against the same framework and parse with this class; what differs between
    /// them is the grammar they install, not the parser.
    /// <para>Every usage error is a <see cref="UsageException"/> whose text comes from
    /// <see cref="ArgUsage.Provider"/>, with the argument spelled as the user typed it
    /// (<c>-o</c> or <c>--output</c>). A host reports it; the parser never writes.</para>
    /// </summary>
    public class CommandLineParser<TContext>
    {
        private readonly CommandLineGrammar _grammar;
        private readonly IList<Argument<TContext>> _arguments;

        /// <summary>
        /// A parser over <paramref name="arguments"/> under the process-wide grammar, read here
        /// once: a host installs its grammar on <see cref="ArgUsage.Grammar"/> before it
        /// declares or parses anything, the way it installs its <see cref="ArgUsage.Provider"/>.
        /// </summary>
        public CommandLineParser(IEnumerable<Argument<TContext>> arguments)
        {
            _grammar = ArgUsage.Grammar;
            _arguments = arguments.ToList();
            SeenArguments = new List<Argument<TContext>>();
        }

        /// <summary>
        /// Takes a bare word the grammar does not recognize as an option (Osprey: an input file
        /// given without <c>-i</c>). Returns true when it consumed the token; otherwise the
        /// grammar's <see cref="CommandLineGrammar.StrayTokens"/> policy applies.
        /// </summary>
        public Func<TContext, string, bool> PositionalHandler { get; set; }

        /// <summary>
        /// Every argument the last <see cref="Parse"/> matched, in command-line order, repeats
        /// included, for a host that validates dependencies between arguments after the parse.
        /// </summary>
        public IList<Argument<TContext>> SeenArguments { get; }

        /// <summary>
        /// Parses <paramref name="args"/>, calling each matched argument's
        /// <see cref="Argument{TContext}.ProcessValue"/> on <paramref name="context"/> in order.
        /// Returns false as soon as a handler does, true when every token was consumed.
        /// A usage error is thrown as a <see cref="UsageException"/>.
        /// </summary>
        public bool Parse(TContext context, IReadOnlyList<string> args)
        {
            SeenArguments.Clear();
            for (int i = 0; i < args.Count; i++)
            {
                string token = args[i];
                var arg = MatchOption(token, out string argText, out string inlineValue);
                if (arg == null)
                {
                    // Reported whole, as typed (--retired=2), so the user sees what was refused.
                    if (_grammar.IsOptionToken(token))
                        throw new UnknownArgumentException(token);
                    if (PositionalHandler != null && PositionalHandler(context, token))
                        continue;
                    if (_grammar.StrayTokens == StrayTokenPolicy.error)
                        throw new UnknownArgumentException(token);
                    continue;
                }

                bool repeated = SeenArguments.Contains(arg);
                SeenArguments.Add(arg);

                if (arg.Variadic)
                {
                    // Every value up to the next option, each dispatched as its own pair, so the
                    // handler is the same one line a single value takes.
                    var values = new List<string>();
                    if (inlineValue != null)
                        values.Add(RequireValue(arg, argText, inlineValue));
                    while (i + 1 < args.Count && IsValueToken(args[i + 1]))
                        values.Add(args[++i]);
                    foreach (string value in values)
                    {
                        if (!Dispatch(context, arg, value, repeated))
                            return false;
                    }
                    continue;
                }

                if (!Dispatch(context, arg, ReadValue(args, ref i, arg, argText, inlineValue), repeated))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// The value <paramref name="arg"/> was given on the command line (the last one, when it
        /// is repeated), or null when it is absent, read exactly the way <see cref="Parse"/>
        /// reads it - both value forms, the same missing-value error - so a host can read one
        /// argument before the full parse (Osprey's <c>--task</c> and <c>--culture</c>) without
        /// a second grammar.
        /// </summary>
        public string FindValue(IReadOnlyList<string> args, ArgumentBase arg)
        {
            string value = null;
            for (int i = 0; i < args.Count; i++)
            {
                var matched = MatchOption(args[i], out string argText, out string inlineValue);
                if (ReferenceEquals(matched, arg))
                    value = ReadValue(args, ref i, matched, argText, inlineValue);
            }
            return value;
        }

        /// <summary>
        /// The argument <paramref name="token"/> names, or null. <paramref name="argText"/> is
        /// the argument as typed, without any inline value, for messages; <paramref name="inlineValue"/>
        /// is the text after the first <c>=</c> (possibly empty) when the token carries one, else null.
        /// </summary>
        private Argument<TContext> MatchOption(string token, out string argText, out string inlineValue)
        {
            argText = token;
            inlineValue = null;
            foreach (string prefix in _grammar.LongPrefixes)
            {
                if (!token.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                string name = SplitInline(token.Substring(prefix.Length), out inlineValue);
                argText = prefix + name;
                var arg = _arguments.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.Ordinal));
                if (arg != null)
                    return arg;
            }
            string shortPrefix = _grammar.ShortPrefix;
            if (shortPrefix != null && token.StartsWith(shortPrefix, StringComparison.Ordinal))
            {
                string name = SplitInline(token.Substring(shortPrefix.Length), out inlineValue);
                argText = shortPrefix + name;
                var arg = _arguments.FirstOrDefault(a => a.ShortName != null && string.Equals(a.ShortName, name, StringComparison.Ordinal));
                if (arg != null)
                    return arg;
            }
            inlineValue = null;
            return null;
        }

        private static string SplitInline(string body, out string inlineValue)
        {
            int separator = body.IndexOf(CommandLineGrammar.INLINE_VALUE_SEPARATOR);
            if (separator < 0)
            {
                inlineValue = null;
                return body;
            }
            inlineValue = body.Substring(separator + 1);
            return body.Substring(0, separator);
        }

        /// <summary>
        /// True when the token after an argument may be its value: it is not spelled like an
        /// option, or, when the grammar lets a prefixed token be a value, it names no argument.
        /// </summary>
        private bool IsValueToken(string token)
        {
            if (!_grammar.IsOptionToken(token))
                return true;
            return !_grammar.PrefixedTokenEndsValue && MatchOption(token, out _, out _) == null;
        }

        /// <summary>
        /// The value of a single-value argument: the inline value when there is one, else the
        /// next token when the grammar allows it and the token can be a value, else null for an
        /// optional value and a <see cref="ValueMissingException"/> for a required one. A flag
        /// given an inline value is a <see cref="ValueUnexpectedException"/>.
        /// </summary>
        private string ReadValue(IReadOnlyList<string> args, ref int i, Argument<TContext> arg, string argText, string inlineValue)
        {
            if (!arg.TakesValue)
            {
                if (!string.IsNullOrEmpty(inlineValue))
                    throw new ValueUnexpectedException(arg, argText);
                return null;
            }
            string value = null;
            if (inlineValue != null)
            {
                // An empty inline value (--name=) is a name without a value.
                value = inlineValue.Length > 0 ? inlineValue : null;
            }
            else if (_grammar.ValueMayFollow && i + 1 < args.Count && IsValueToken(args[i + 1]) &&
                     (!arg.OptionalValue || arg.OptionalValueFilter == null || arg.OptionalValueFilter(args[i + 1])))
            {
                value = args[++i];
            }
            if (value == null && !arg.OptionalValue)
                throw new ValueMissingException(arg, argText);
            return value;
        }

        private static string RequireValue(ArgumentBase arg, string argText, string inlineValue)
        {
            if (inlineValue.Length == 0)
                throw new ValueMissingException(arg, argText);
            return inlineValue;
        }

        private bool Dispatch(TContext context, Argument<TContext> arg, string value, bool repeated)
        {
            var pair = new NameValuePair(arg.Name, value);
            pair.Bind(arg, _grammar.ValidatesListedValues && !arg.HasValueChecking);
            if (repeated && _grammar.FirstValueWins)
                return true;
            if (arg.ProcessValue == null)
                throw new InvalidOperationException(@"The argument " + arg.ArgumentText + @" declares no way to process its value.");
            return arg.ProcessValue(context, pair);
        }
    }
}
