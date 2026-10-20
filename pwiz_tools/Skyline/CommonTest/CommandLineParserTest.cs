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

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.CommandLine;
using pwiz.SkylineTestUtil;

namespace CommonTest
{
    /// <summary>
    /// The one tokenizer under each grammar a host installs: Skyline's strict
    /// <c>--name=value</c>, Osprey's <c>--name value</c> with short aliases, variadic and
    /// optional-count arguments and a positional fallback, and CarafeSharp's single-dash
    /// <c>-name value</c> with the standard spelling accepted alongside. The hosts' own tests
    /// pin their parsed results; this pins the grammar rules the hosts rely on, on a small
    /// declaration set, so a rule cannot change under one host without failing here.
    /// </summary>
    [TestClass]
    public class CommandLineParserTest : AbstractUnitTest
    {
        private class Context
        {
            public readonly List<string> Inputs = new List<string>();
            public readonly List<string> Positional = new List<string>();
            public string Output;
            public string Mode;
            public string Help;
            public bool HelpSeen;
            public string Count;
            public bool CountSeen;
            public bool Flag;
            public bool Stop;
            public string TwoWords;
        }

        private static readonly Argument<Context> ARG_INPUT = new Argument<Context>(@"input", () => @"<file>",
            (c, p) => c.Inputs.Add(p.Value)) { ShortName = @"i", Variadic = true };
        private static readonly Argument<Context> ARG_OUTPUT = new Argument<Context>(@"out", () => @"<path>",
            (c, p) => c.Output = p.Value) { ShortName = @"o" };
        private static readonly Argument<Context> ARG_MODE = new Argument<Context>(@"mode", new[] { @"fast", @"slow" },
            (c, p) => c.Mode = p.Value);
        private static readonly Argument<Context> ARG_HELP = new Argument<Context>(@"help", () => @"<format>", (c, p) =>
        {
            c.HelpSeen = true;
            c.Help = p.Value;
        }) { ShortName = @"h", OptionalValue = true };
        // Skyline declares flags like this one: OptionalValue set, no value declared. A value
        // given to it is refused, as it always was.
        private static readonly Argument<Context> ARG_RAGGED = new Argument<Context>(@"ragged",
            (c, p) => c.Flag = p.IsNameOnly) { OptionalValue = true };
        private static readonly Argument<Context> ARG_COUNT = new Argument<Context>(@"count", () => @"<N>", (c, p) =>
        {
            c.CountSeen = true;
            c.Count = p.Value;
        }) { OptionalValue = true, OptionalValueFilter = token => token.All(char.IsDigit) };
        private static readonly Argument<Context> ARG_FLAG = new Argument<Context>(@"flag", (c, p) => c.Flag = true);
        private static readonly Argument<Context> ARG_STOP = new Argument<Context>(@"stop", (c, p) =>
        {
            c.Stop = true;
            return false;
        });
        // A hyphenated name, for the grammar that reads an underscore as a hyphen.
        private static readonly Argument<Context> ARG_TWO_WORDS = new Argument<Context>(@"two-words", () => @"<v>",
            (c, p) => c.TwoWords = p.Value);

        private static readonly Argument<Context>[] ARGUMENTS =
        {
            ARG_INPUT, ARG_OUTPUT, ARG_MODE, ARG_HELP, ARG_COUNT, ARG_FLAG, ARG_STOP, ARG_RAGGED, ARG_TWO_WORDS
        };

        /// <summary>
        /// Mutates the process-wide grammar and provider, so it must not share a process slice
        /// with another test building argument text.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestCommandLineParserGrammars()
        {
            var grammar = ArgUsage.Grammar;
            var provider = ArgUsage.Provider;
            string separator = ArgUsage.ArgumentValueSeparator;
            try
            {
                ArgUsage.Provider = new TestProvider();
                TestStrictGrammar();
                TestNextTokenGrammar();
                TestSingleDashGrammar();
                TestEquivalentSeparators();
            }
            finally
            {
                ArgUsage.Grammar = grammar;
                ArgUsage.Provider = provider;
                ArgUsage.ArgumentValueSeparator = separator;
            }
        }

        /// <summary>Skyline's: inline values only, no short aliases, a bare word ignored, listed values enforced.</summary>
        private static void TestStrictGrammar()
        {
            ArgUsage.Grammar = CommandLineGrammar.STRICT;
            ArgUsage.ArgumentValueSeparator = @"=";
            AssertEx.AreEqual(@"--out", ARG_OUTPUT.ArgumentText);
            AssertEx.IsNull(ARG_OUTPUT.ShortArgumentText, @"no short prefix, no short spelling");
            AssertEx.AreEqual(@"--out=x", ARG_OUTPUT + @"x");
            AssertEx.AreEqual(@"--help[=value]", new Argument<Context>(@"help", () => @"value", (c, p) => true) { OptionalValue = true }.ArgumentDescription);

            var context = Parse(@"--out=x", @"stray", @"-o", @"--mode=slow", @"--help", @"--flag");
            AssertEx.AreEqual(@"x", context.Output);
            AssertEx.AreEqual(@"slow", context.Mode);
            AssertEx.IsTrue(context.HelpSeen);
            AssertEx.IsNull(context.Help);
            AssertEx.IsTrue(context.Flag);
            AssertEx.AreEqual(@"ascii", Parse(@"--help=ascii").Help);
            // A value is never the next token: it is a stray word, and the argument has none.
            var missing = Assert.ThrowsException<ValueMissingException>(() => Parse(@"--out", @"x"));
            Assert.AreSame(ARG_OUTPUT, missing.Argument);
            AssertEx.AreEqual(@"missing --out", missing.Message);
            Assert.ThrowsException<ValueMissingException>(() => Parse(@"--out="));
            // A flag given a value, an unknown argument, a value outside the list.
            AssertEx.AreEqual(@"unexpected --flag", Assert.ThrowsException<ValueUnexpectedException>(() => Parse(@"--flag=1")).Message);
            AssertEx.IsTrue(Parse(@"--ragged").Flag);
            AssertEx.AreEqual(@"unexpected --ragged", Assert.ThrowsException<ValueUnexpectedException>(() => Parse(@"--ragged=maybe")).Message);
            AssertEx.AreEqual(@"unknown --bogus=1", Assert.ThrowsException<UnknownArgumentException>(() => Parse(@"--bogus=1")).Message);
            AssertEx.AreEqual(@"unknown --", Assert.ThrowsException<UnknownArgumentException>(() => Parse(@"--")).Message);
            AssertEx.AreEqual(@"invalid bad --mode fast, slow", Assert.ThrowsException<ValueInvalidException>(() => Parse(@"--mode=bad")).Message);
            // A variadic argument takes no following token under a grammar whose values never follow.
            var strictInput = Parse(@"--input=a", @"b");
            CollectionAssert.AreEqual(new[] { @"a" }, strictInput.Inputs);
            CollectionAssert.AreEqual(new[] { @"b" }, strictInput.Positional);
            // A handler returning false stops the parse there.
            var parser = new CommandLineParser<Context>(ARGUMENTS);
            context = new Context();
            AssertEx.IsFalse(parser.Parse(context, new[] { @"--flag", @"--stop", @"--out=x" }));
            AssertEx.IsTrue(context.Stop);
            AssertEx.IsNull(context.Output);
            CollectionAssert.AreEqual(new[] { ARG_FLAG, ARG_STOP }, parser.SeenArguments.ToArray());
            // A stray word is an error on request, when no positional handler takes it.
            ArgUsage.Grammar = new CommandLineGrammar { StrayTokens = StrayTokenPolicy.error };
            parser = new CommandLineParser<Context>(ARGUMENTS);
            AssertEx.AreEqual(@"unknown stray", Assert.ThrowsException<UnknownArgumentException>(() => parser.Parse(new Context(), new[] { @"stray" })).Message);
        }

        /// <summary>Osprey's: a value as the next token, short aliases, variadic and optional-count arguments, a positional fallback, listed values left to the handler.</summary>
        private static void TestNextTokenGrammar()
        {
            ArgUsage.Grammar = new CommandLineGrammar { ShortPrefix = @"-", ValueMayFollow = true, ValidatesListedValues = false };
            ArgUsage.ArgumentValueSeparator = @" ";
            AssertEx.AreEqual(@"-o", ARG_OUTPUT.ShortArgumentText);
            AssertEx.AreEqual(@"--out x", ARG_OUTPUT + @"x");
            AssertEx.AreEqual(@"--count [<N>]", ARG_COUNT.ArgumentDescription);

            var context = Parse(@"-i", @"a", @"b", @"--out", @"x", @"--mode", @"bad", @"-h", @"--flag");
            CollectionAssert.AreEqual(new[] { @"a", @"b" }, context.Inputs);
            AssertEx.AreEqual(@"x", context.Output);
            AssertEx.AreEqual(@"bad", context.Mode, @"listed values are the handler's to check");
            AssertEx.IsTrue(context.HelpSeen);
            AssertEx.IsNull(context.Help, @"the next option is not the optional value");
            AssertEx.IsTrue(context.Flag);
            // Both value forms, and a short alias with an inline value.
            AssertEx.AreEqual(@"x", Parse(@"--out=x").Output);
            AssertEx.AreEqual(@"a=b", Parse(@"-o=a=b").Output);
            CollectionAssert.AreEqual(new[] { @"a", @"b" }, Parse(@"-i=a", @"b").Inputs);
            AssertEx.AreEqual(@"ascii", Parse(@"--help", @"ascii").Help);
            // A missing value names the argument as typed; any dash-prefixed token ends a value.
            AssertEx.AreEqual(@"missing -o", Assert.ThrowsException<ValueMissingException>(() => Parse(@"-o")).Message);
            AssertEx.AreEqual(@"missing --out", Assert.ThrowsException<ValueMissingException>(() => Parse(@"--out", @"-5")).Message);
            Assert.ThrowsException<ValueMissingException>(() => Parse(@"--out="));
            Assert.ThrowsException<ValueMissingException>(() => Parse(@"-i="));
            AssertEx.AreEqual(@"unexpected --flag", Assert.ThrowsException<ValueUnexpectedException>(() => Parse(@"--flag=1")).Message);
            AssertEx.AreEqual(@"unknown -x", Assert.ThrowsException<UnknownArgumentException>(() => Parse(@"-x")).Message);
            // A listed value refused under an alias names the alias, and an argument may own
            // its missing-value text.
            ArgUsage.Grammar = new CommandLineGrammar { ShortPrefix = @"-", ValueMayFollow = true };
            ARG_MODE.ShortName = @"m";
            try
            {
                AssertEx.AreEqual(@"invalid bad -m fast, slow", Assert.ThrowsException<ValueInvalidException>(() => Parse(@"-m", @"bad")).Message);
            }
            finally
            {
                ARG_MODE.ShortName = null;
            }
            ARG_OUTPUT.MissingValueMessage = argText => @"own " + argText;
            try
            {
                AssertEx.AreEqual(@"own -o", Assert.ThrowsException<ValueMissingException>(() => Parse(@"-o")).Message);
            }
            finally
            {
                ARG_OUTPUT.MissingValueMessage = null;
            }
            ArgUsage.Grammar = new CommandLineGrammar { ShortPrefix = @"-", ValueMayFollow = true, ValidatesListedValues = false };
            AssertEx.AreEqual(@"unknown --bogus", Assert.ThrowsException<UnknownArgumentException>(() => Parse(@"--bogus", @"1")).Message);
            // An optional count takes only a token its filter accepts; the rest is positional.
            context = Parse(@"--count", @"3", @"--count", @"a.mzML", @"--count");
            AssertEx.IsTrue(context.CountSeen);
            AssertEx.IsNull(context.Count, @"the last --count had no value");
            CollectionAssert.AreEqual(new[] { @"a.mzML" }, context.Positional);
            AssertEx.AreEqual(@"3", Parse(@"--count", @"3", @"--flag").Count);
            AssertEx.AreEqual(@"abc", Parse(@"--count=abc").Count, @"an inline value is explicit, filter or not");
            // Without a positional handler the grammar's policy applies: ignored by default.
            var parser = new CommandLineParser<Context>(ARGUMENTS);
            context = new Context();
            AssertEx.IsTrue(parser.Parse(context, new[] { @"stray", @"--flag" }));
            AssertEx.IsTrue(context.Flag);
            // FindValue reads one argument the way the parser does: both forms, the last one wins.
            AssertEx.AreEqual(@"y", parser.FindValue(new[] { @"-i", @"a", @"--out", @"x", @"-o=y" }, ARG_OUTPUT));
            AssertEx.IsNull(parser.FindValue(new[] { @"-i", @"a" }, ARG_OUTPUT));
            AssertEx.AreEqual(@"missing --out", Assert.ThrowsException<ValueMissingException>(() => parser.FindValue(new[] { @"--out" }, ARG_OUTPUT)).Message);
            AssertEx.IsNull(parser.FindValue(new[] { @"-h" }, ARG_HELP));
            AssertEx.AreEqual(@"html", parser.FindValue(new[] { @"-h", @"html" }, ARG_HELP));
        }

        /// <summary>CarafeSharp's: the standard spelling shown, Carafe's single dash accepted for every name, a prefixed token a value unless it names an option, the first of a repeat winning.</summary>
        private static void TestSingleDashGrammar()
        {
            ArgUsage.Grammar = new CommandLineGrammar
            {
                AcceptedPrefixes = new[] { @"--", @"-" },
                ShortPrefix = @"-",
                ValueMayFollow = true,
                PrefixedTokenEndsValue = false,
                FirstValueWins = true,
                ValidatesListedValues = false,
            };
            ArgUsage.ArgumentValueSeparator = @" ";
            AssertEx.AreEqual(@"--out", ARG_OUTPUT.ArgumentText);
            AssertEx.AreEqual(@"-o", ARG_OUTPUT.ShortArgumentText);
            AssertEx.AreEqual(@"--out x", ARG_OUTPUT + @"x");

            foreach (var spelling in new[] { @"-out", @"--out", @"-o" })
                AssertEx.AreEqual(@"x", Parse(spelling, @"x").Output, spelling);
            AssertEx.AreEqual(@"x", Parse(@"-out=x").Output);
            AssertEx.AreEqual(@"x", Parse(@"--out=x").Output);
            // A dash-prefixed token that names no option is a value; one that does ends the value.
            AssertEx.AreEqual(@"-5", Parse(@"-out", @"-5").Output);
            AssertEx.AreEqual(@"-", Parse(@"-out", @"-").Output);
            AssertEx.AreEqual(@"missing -out", Assert.ThrowsException<ValueMissingException>(() => Parse(@"-out", @"-flag")).Message);
            AssertEx.AreEqual(@"missing --out", Assert.ThrowsException<ValueMissingException>(() => Parse(@"--out", @"--flag")).Message);
            // The first value wins, and the repeat's value is still consumed, not left as a word.
            var context = Parse(@"-out", @"a", @"-out", @"b", @"-flag", @"-flag", @"stray");
            AssertEx.AreEqual(@"a", context.Output);
            AssertEx.IsTrue(context.Flag);
            // Unknown and flag-with-value, as typed; a lone dash is an unknown option.
            AssertEx.AreEqual(@"unknown -bogus", Assert.ThrowsException<UnknownArgumentException>(() => Parse(@"-bogus")).Message);
            AssertEx.AreEqual(@"unknown --bogus", Assert.ThrowsException<UnknownArgumentException>(() => Parse(@"--bogus")).Message);
            AssertEx.AreEqual(@"unexpected -flag", Assert.ThrowsException<ValueUnexpectedException>(() => Parse(@"-flag=1")).Message);
            AssertEx.AreEqual(@"unknown -", Assert.ThrowsException<UnknownArgumentException>(() => Parse(@"-")).Message);
        }

        /// <summary>
        /// With <see cref="CommandLineGrammar.NameSeparatorsEquivalent"/> an underscore in a typed
        /// name matches a hyphen in the declared one (Osprey and CarafeSharp, whose declarations
        /// are hyphenated and whose users have Carafe's underscore spellings in scripts); the
        /// declaration still spells itself with the hyphen, and without the rule the underscore
        /// spelling is an unknown argument, as Skyline's grammar has always treated it.
        /// </summary>
        private static void TestEquivalentSeparators()
        {
            ArgUsage.Grammar = CommandLineGrammar.STRICT;
            ArgUsage.ArgumentValueSeparator = @"=";
            AssertEx.AreEqual(@"x", Parse(@"--two-words=x").TwoWords);
            AssertEx.AreEqual(@"unknown --two_words=x", Assert.ThrowsException<UnknownArgumentException>(() => Parse(@"--two_words=x")).Message);

            ArgUsage.Grammar = new CommandLineGrammar { ShortPrefix = @"-", ValueMayFollow = true, NameSeparatorsEquivalent = true };
            ArgUsage.ArgumentValueSeparator = @" ";
            AssertEx.AreEqual(@"--two-words", ARG_TWO_WORDS.ArgumentText);
            AssertEx.AreEqual(@"--two-words x", ARG_TWO_WORDS + @"x");
            foreach (var spelling in new[] { @"--two-words", @"--two_words" })
            {
                AssertEx.AreEqual(@"x", Parse(spelling, @"x").TwoWords, spelling);
                AssertEx.AreEqual(@"y", Parse(spelling + @"=y").TwoWords, spelling);
            }
            // A separator matches only a separator, in the same place; an unknown name is reported as typed.
            AssertEx.AreEqual(@"unknown --two_word", Assert.ThrowsException<UnknownArgumentException>(() => Parse(@"--two_word", @"x")).Message);
            AssertEx.AreEqual(@"unknown --twowords", Assert.ThrowsException<UnknownArgumentException>(() => Parse(@"--twowords", @"x")).Message);
            AssertEx.AreEqual(@"unknown --two_words_", Assert.ThrowsException<UnknownArgumentException>(() => Parse(@"--two_words_", @"x")).Message);
            // FindValue follows the same rule.
            AssertEx.AreEqual(@"x", new CommandLineParser<Context>(ARGUMENTS).FindValue(new[] { @"--two_words", @"x" }, ARG_TWO_WORDS));
        }

        private static Context Parse(params string[] args)
        {
            var context = new Context();
            var parser = new CommandLineParser<Context>(ARGUMENTS)
            {
                PositionalHandler = (c, token) =>
                {
                    c.Positional.Add(token);
                    return true;
                }
            };
            AssertEx.IsTrue(parser.Parse(context, args));
            return context;
        }

        /// <summary>Terse English, so a test can pin which message was built and with what.</summary>
        private class TestProvider : IArgUsageProvider
        {
            public string GetDescription(string argName) { return argName; }
            public string AppliesToHeader { get { return @"Applies To"; } }
            public string ArgumentHeader { get { return @"Argument"; } }
            public string DescriptionHeader { get { return @"Description"; } }
            public string ValueMissingMessage(string argText) { return @"missing " + argText; }
            public string ValueUnexpectedMessage(string argText) { return @"unexpected " + argText; }
            public string ValueInvalidMessage(string argText, string value, string[] argValues) { return @"invalid " + value + @" " + argText + @" " + string.Join(@", ", argValues); }
            public string ValueInvalidBoolMessage(string argText, string value) { return @"bool " + value; }
            public string ValueInvalidIntMessage(string argText, string value) { return @"int " + value; }
            public string ValueOutOfRangeIntMessage(string argText, int value, int minVal, int maxVal) { return @"range " + value; }
            public string ValueInvalidDoubleMessage(string argText, string value) { return @"double " + value; }
            public string ValueOutOfRangeDoubleMessage(string argText, double value, double minVal, double maxVal) { return @"range " + value; }
            public string ValueInvalidDateMessage(string argText, string value) { return @"date " + value; }
            public string ValueInvalidPathMessage(string argText, string value) { return @"path " + value; }
            public string UnknownArgumentMessage(string argText) { return @"unknown " + argText; }
        }
    }
}
