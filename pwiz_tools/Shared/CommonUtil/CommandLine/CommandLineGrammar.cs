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
using System.Text;

namespace pwiz.Common.CommandLine
{
    /// <summary>
    /// What <see cref="CommandLineParser{TContext}"/> does with a token that names no argument
    /// and starts with no prefix (a bare word among the options).
    /// </summary>
    public enum StrayTokenPolicy
    {
        /// <summary>Skipped without comment, as Skyline's command line always has.</summary>
        ignore,
        /// <summary>Refused as an unknown argument.</summary>
        error,
    }

    /// <summary>
    /// The command-line grammar a host parses with: a configuration the host picks, never a
    /// subclass per tool. <see cref="STRICT"/> is Skyline's <c>--name=value</c>; Osprey adds
    /// values as the next token, short aliases and variadic arguments; CarafeSharp accepts
    /// Carafe's single-dash <c>-name value</c> alongside the standard form. One grammar is in
    /// force at a time, on <see cref="ArgUsage.Grammar"/>, because an argument's text
    /// (<see cref="ArgumentBase.ArgumentText"/>, the generated help, every error message) is
    /// spelled with its <see cref="Prefix"/>; the program that owns the process installs it
    /// (<see cref="ArgUsage.Install"/>), and declarations hosted in another program's process
    /// run under that program's.
    /// <para>Every grammar accepts the inline form <c>--name=value</c>, split at the first
    /// <c>=</c>; the rest is configured here.</para>
    /// </summary>
    public sealed class CommandLineGrammar
    {
        /// <summary>
        /// Skyline's grammar, and the default: <c>--name=value</c> only, no short aliases, a
        /// value never taken from the next token, a bare word ignored, a value outside an
        /// argument's declared list refused by the parser.
        /// </summary>
        public static readonly CommandLineGrammar STRICT = new CommandLineGrammar();

        /// <summary>The character an inline value follows: <c>--name=value</c>.</summary>
        public const char INLINE_VALUE_SEPARATOR = '=';

        /// <summary>
        /// The prefix an argument's long name is spelled with in help and messages
        /// (<see cref="ArgumentBase.ArgumentText"/>): <c>--</c> for Skyline, Osprey and
        /// CarafeSharp, whose grammar also accepts Carafe's single dash when parsing.
        /// </summary>
        public string Prefix { get; init; } = ArgumentBase.ARG_PREFIX;

        /// <summary>
        /// Every prefix a long name is recognized under, <see cref="Prefix"/> included. Null
        /// means <see cref="Prefix"/> alone. CarafeSharp lists <c>-</c> and <c>--</c>, so one
        /// declaration answers to Carafe's <c>-db</c> and the standard <c>--db</c>.
        /// </summary>
        public IReadOnlyList<string> AcceptedPrefixes { get; init; }

        /// <summary>
        /// The prefix of a short alias (<see cref="ArgumentBase.ShortName"/>): <c>-i</c> for
        /// <c>--input</c>. Null (the default) means the grammar has no short aliases, and a
        /// token starting with a single dash is a bare word, not an option.
        /// </summary>
        public string ShortPrefix { get; init; }

        /// <summary>
        /// True when a value may be the token after its argument (<c>--name value</c>); false
        /// (the default) when it must be inline (<c>--name=value</c>).
        /// </summary>
        public bool ValueMayFollow { get; init; }

        /// <summary>
        /// With <see cref="ValueMayFollow"/>: true (the default) when any token starting with a
        /// prefix is the next option rather than a value, so <c>-o -l x</c> fails fast with a
        /// missing value; false when only a token naming a declared argument is, so a negative
        /// number (<c>-seed -5</c>) or a bare dash (<c>-mode -</c>) is a value, as it is to Carafe.
        /// </summary>
        public bool PrefixedTokenEndsValue { get; init; } = true;

        /// <summary>
        /// True when a repeated argument keeps its first value (Carafe); false (the default) when
        /// each occurrence is processed and the last one wins.
        /// </summary>
        public bool FirstValueWins { get; init; }

        /// <summary>
        /// True (the default) when the parser refuses a value outside an argument's declared
        /// <see cref="ArgumentBase.Values"/> unless the argument has
        /// <see cref="ArgumentBase.HasValueChecking"/>; false when the host's handlers decide,
        /// as Osprey's do (warn and default) and CarafeSharp's settings do.
        /// </summary>
        public bool ValidatesListedValues { get; init; } = true;

        /// <summary>
        /// What a bare word among the options is, when the parser's positional handler does not
        /// take it: ignored (the default) or an error.
        /// </summary>
        public StrayTokenPolicy StrayTokens { get; init; } = StrayTokenPolicy.ignore;

        /// <summary>
        /// True when an underscore, a hyphen and a lower-to-upper case boundary are the same
        /// separator in an option's name, and case is otherwise ignored, so <c>--miss_c</c> names
        /// the declaration spelled <c>miss-c</c> and <c>-minLength</c> the one spelled
        /// <c>min-length</c>. Osprey and CarafeSharp spell their names with hyphens and accept
        /// the underscore and camel-case spellings Carafe's recorded command lines use, with no
        /// alias per option; false (the default) matches names exactly.
        /// </summary>
        public bool NameSeparatorsEquivalent { get; init; }

        private IReadOnlyList<string> _longPrefixes;

        /// <summary>
        /// The prefixes a long name is recognized under, longest first. Built once: the
        /// properties it derives from are init-only, and the parser reads it per token.
        /// </summary>
        public IReadOnlyList<string> LongPrefixes
        {
            get
            {
                if (_longPrefixes == null)
                {
                    var prefixes = new List<string>(AcceptedPrefixes ?? new[] { Prefix });
                    if (!prefixes.Contains(Prefix))
                        prefixes.Insert(0, Prefix);
                    prefixes.Sort((a, b) => b.Length.CompareTo(a.Length));
                    _longPrefixes = prefixes;
                }
                return _longPrefixes;
            }
        }

        /// <summary>
        /// True when <paramref name="token"/> is spelled like an option: it starts with a long
        /// prefix or the short prefix.
        /// </summary>
        public bool IsOptionToken(string token)
        {
            foreach (string prefix in LongPrefixes)
            {
                if (token.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }
            return ShortPrefix != null && token.StartsWith(ShortPrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// True when <paramref name="typed"/>, the name as it was typed, names the declaration
        /// <paramref name="declared"/>: exactly, or with <see cref="NameSeparatorsEquivalent"/>
        /// when both read the same as <see cref="StandardName"/> spells them.
        /// </summary>
        public bool NamesMatch(string declared, string typed)
        {
            if (string.Equals(declared, typed, StringComparison.Ordinal))
                return true;
            return NameSeparatorsEquivalent &&
                   string.Equals(StandardName(declared), StandardName(typed), StringComparison.Ordinal);
        }

        /// <summary>
        /// <paramref name="name"/> in the standard spelling: lower case, with a hyphen for every
        /// underscore and before every upper-case letter that follows a lower-case one
        /// (<c>minLength</c> and <c>min_length</c> are <c>min-length</c>; <c>I2L</c> is <c>i2l</c>).
        /// </summary>
        public static string StandardName(string name)
        {
            var sb = new StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c == '_')
                    c = '-';
                else if (char.IsUpper(c) && i > 0 && char.IsLower(name[i - 1]))
                    sb.Append('-');
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}
