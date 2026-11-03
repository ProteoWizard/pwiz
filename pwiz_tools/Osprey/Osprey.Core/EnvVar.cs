/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using System.Linq;
using System.Reflection;

// Developer switches, deliberately kept off the command line: names, values and descriptions are
// developer text, never translated (see "Localization" in docs/20-command-line.md).
// ReSharper disable LocalizableElement

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// What a switch does to a run, which is how <c>--help-env</c> groups the catalog. The kinds
    /// deserve visibly different treatment: a diagnostic is safe anywhere, while an allowance on a
    /// standing gate can only mask the regression that gate exists to catch.
    /// </summary>
    public enum EnvVarGroup
    {
        /// <summary>Changes the reported results.</summary>
        behavior,
        /// <summary>Relaxes a check that would otherwise stop the run.</summary>
        allowance,
        /// <summary>Changes how the work is done, not what it produces.</summary>
        performance,
        /// <summary>Stops early or limits the work, for fast iteration; the output is incomplete.</summary>
        harness,
        /// <summary>Writes extra files or log lines; the output is unchanged.</summary>
        diagnostic,
    }

    /// <summary>
    /// One OSPREY_* environment variable: the second, developer-only level of Osprey's controls,
    /// catalogued the way <c>pwiz.Common.CommandLine.ArgumentBase</c> catalogs the first. It
    /// carries the same cataloging members - a name, a description, a value example, the listed
    /// values and a default - and adds the group, and both the group and the description are
    /// constructor arguments, so an entry cannot be declared without them.
    ///
    /// <para>Declared as a static field beside the code that reads it, never in a central list,
    /// so the catalog cannot drift from the behavior: <see cref="EnvVarCatalog.Collect"/> finds
    /// every declaration by reflection. Reading by raw name is not an expressible program - the
    /// only way to a value is <see cref="Value"/> on a declaration - and
    /// <c>CodeInspectionTest.TestEnvironmentReadThroughEnvVar</c> keeps
    /// <see cref="Environment.GetEnvironmentVariable(string)"/> out of every other file.</para>
    ///
    /// <para>How a value is interpreted (a flag on by default or off by default, a number, a
    /// path) stays with the code that reads it, as before; this class only supplies the text.
    /// A declaration that lists its <see cref="Values"/> (or supplies <see cref="AcceptsValue"/>)
    /// has any other value refused at startup (<see cref="ValueError"/>), because a value typo on
    /// such a switch would otherwise run the default arm under the name of the one asked for.</para>
    /// </summary>
    public sealed class EnvVar
    {
        /// <summary>The prefix every Osprey variable carries, and the namespace Osprey claims.</summary>
        public const string PREFIX = @"OSPREY_";

        /// <summary>The value example shown for an on/off switch.</summary>
        public const string FLAG_EXAMPLE = @"0|1";

        // Variables a test has overridden (OverrideVariables); null in production.
        private static IReadOnlyDictionary<string, string> _overrides;

        /// <summary>
        /// How the platform compares environment variable names: case-insensitively on Windows,
        /// exactly elsewhere. The catalog and the scan must agree with it, or a Linux node would
        /// accept 'osprey_log_memory' and then never read it as OSPREY_LOG_MEMORY.
        /// </summary>
        public static readonly StringComparer NameComparer =
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        private static readonly StringComparison NameComparison =
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        /// <summary>
        /// Replace the named variables with <paramref name="values"/> until the returned object
        /// is disposed; a null value reads as unset. Every other variable still reads the
        /// process environment. Only the values that re-read on each access see the override -
        /// a readonly field was fixed when its class loaded.
        /// </summary>
        public static IDisposable OverrideVariables(IReadOnlyDictionary<string, string> values)
        {
            var restorer = new OverridesRestorer(_overrides);
            _overrides = values;
            return restorer;
        }

        /// <summary>
        /// The OSPREY_* names set to a non-empty value in this process - its environment, or a
        /// test's <see cref="OverrideVariables"/> - sorted. An empty value reads as unset, as it
        /// does everywhere else: a cleared <c>export</c> or a blanked CI parameter is not a
        /// request for anything.
        /// </summary>
        public static IReadOnlyList<string> SetVariableNames()
        {
            var names = new SortedSet<string>(NameComparer);
            foreach (string name in Environment.GetEnvironmentVariables().Keys)
            {
                if (name.StartsWith(PREFIX, NameComparison))
                    names.Add(name);
            }
            if (_overrides != null)
            {
                foreach (string name in _overrides.Keys)
                {
                    if (name.StartsWith(PREFIX, NameComparison))
                        names.Add(name);
                }
            }
            return names.Where(name => !string.IsNullOrEmpty(Read(name))).ToList();
        }

        public EnvVar(string name, EnvVarGroup group, string description)
        {
            if (name == null || !name.StartsWith(PREFIX, StringComparison.Ordinal))
                throw new ArgumentException(string.Format(@"An Osprey environment variable must start with {0}: {1}", PREFIX, name));
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException(string.Format(@"{0} needs a description", name));
            Name = name;
            Group = group;
            Description = description;
        }

        /// <summary>The variable's name, e.g. <c>OSPREY_LOG_MEMORY</c>.</summary>
        public string Name { get; }

        /// <summary>What the switch does to a run.</summary>
        public EnvVarGroup Group { get; }

        /// <summary>One or a few sentences for <c>--help-env</c>.</summary>
        public string Description { get; }

        /// <summary>
        /// The value as help shows it (<c>&lt;N&gt;</c>, <c>&lt;path&gt;</c>), or null for an
        /// on/off switch, which shows <see cref="FLAG_EXAMPLE"/>. A declaration with
        /// <see cref="Values"/> shows them instead.
        /// </summary>
        public string ValueExample { get; set; }

        /// <summary>
        /// The values the variable accepts, for help and for the startup check; any other set
        /// value stops the run (<see cref="ValueError"/>). Compared ignoring case and surrounding
        /// whitespace. Null for a variable whose reader takes any value.
        /// </summary>
        public string[] Values { get; set; }

        /// <summary>
        /// The test for an accepted value when no list can name them all (<c>mean-best-&lt;N&gt;</c>).
        /// When set it decides alone, and <see cref="Values"/> is only what help and the refusal
        /// show; any value it refuses is a startup error.
        /// </summary>
        public Func<string, bool> AcceptsValue { get; set; }

        /// <summary>What the run does when the variable is unset, as help shows it; null when that
        /// is simply "off".</summary>
        public string DefaultValue { get; set; }

        /// <summary>
        /// The value exactly as set, or null when unset. The one read of an environment variable in
        /// Osprey: a test's <see cref="OverrideVariables"/> first, then the process environment.
        /// Re-read on every access, so a caller decides whether to keep it.
        /// </summary>
        public string Value
        {
            get { return Read(Name); }
        }

        /// <summary>True when the variable is set to a non-empty value.</summary>
        public bool IsSet
        {
            get { return !string.IsNullOrEmpty(Value); }
        }

        /// <summary>The variable and its value as help shows them: <c>OSPREY_X=&lt;N&gt;</c>.</summary>
        public string Usage
        {
            get
            {
                string example = Values != null ? string.Join(@"|", Values) : ValueExample ?? FLAG_EXAMPLE;
                return Name + @"=" + example;
            }
        }

        /// <summary>
        /// The startup refusal for a set value this declaration does not accept, or null when it is
        /// unset, accepted, or the declaration lists no values.
        /// </summary>
        public string ValueError
        {
            get
            {
                if (!IsSet || (Values == null && AcceptsValue == null))
                    return null;
                if (AcceptsValue != null ? AcceptsValue(Value) : IsListedValue(Value))
                    return null;
                return string.Format(@"{0}='{1}' is not a recognized value. Use {2}, or unset it for the default.",
                    Name, Value, Values != null ? string.Join(@" or ", Values.Select(v => @"'" + v + @"'")) : ValueExample);
            }
        }

        /// <summary>
        /// Set the variable in this process, as the OSPREY_DIAGNOSTICS bundle does for the dumps it
        /// turns on, so a reader created afterwards sees it exactly as an exported value.
        /// </summary>
        public void SetInProcess(string value)
        {
            Environment.SetEnvironmentVariable(Name, value);
        }

        public override string ToString()
        {
            return Usage;
        }

        private bool IsListedValue(string value)
        {
            string trimmed = value.Trim();
            return Values.Any(v => string.Equals(v, trimmed, StringComparison.OrdinalIgnoreCase));
        }

        private static string Read(string name)
        {
            if (_overrides != null && _overrides.TryGetValue(name, out string value))
                return value;
            return Environment.GetEnvironmentVariable(name);
        }

        private sealed class OverridesRestorer : IDisposable
        {
            private readonly IReadOnlyDictionary<string, string> _saved;

            public OverridesRestorer(IReadOnlyDictionary<string, string> saved)
            {
                _saved = saved;
            }

            public void Dispose()
            {
                _overrides = _saved;
            }
        }
    }

    /// <summary>
    /// Every <see cref="EnvVar"/> a build declares, found where it is declared. The table behind
    /// <c>--help-env</c>, the startup refusal of an OSPREY_* name it does not hold, the startup
    /// check of listed values, and the startup line naming every switch that is set.
    /// </summary>
    public static class EnvVarCatalog
    {
        private const string ASSEMBLY_PREFIX = @"Osprey";

        /// <summary>
        /// The declarations in <paramref name="root"/> and every Osprey assembly it references,
        /// directly or not, sorted by name. A declaration is a static field of type
        /// <see cref="EnvVar"/>, public or not; reading it runs its class's static initializer,
        /// which is the initialization that class would get on first use anyway. Two declarations
        /// of one name are a programming error and throw.
        /// </summary>
        public static IReadOnlyList<EnvVar> Collect(Assembly root)
        {
            var entries = new List<EnvVar>();
            foreach (var assembly in OspreyAssemblies(root))
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsGenericTypeDefinition)
                        continue;
                    const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                    foreach (var field in type.GetFields(flags))
                    {
                        if (field.FieldType == typeof(EnvVar) && field.GetValue(null) is EnvVar entry && !entries.Contains(entry))
                            entries.Add(entry);
                    }
                }
            }
            var duplicate = entries.GroupBy(e => e.Name, EnvVar.NameComparer).FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null)
                throw new InvalidOperationException(string.Format(@"{0} is declared more than once", duplicate.Key));
            return entries.OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// The OSPREY_* variables set in this process that <paramref name="catalog"/> does not
        /// hold, sorted: retired switches, names only Rust osprey reads, and misspellings.
        /// </summary>
        public static IReadOnlyList<string> UnrecognizedVariables(IEnumerable<EnvVar> catalog)
        {
            var known = new HashSet<string>(catalog.Select(e => e.Name), EnvVar.NameComparer);
            return EnvVar.SetVariableNames().Where(name => !known.Contains(name)).ToList();
        }

        /// <summary>
        /// The catalog name closest to <paramref name="name"/> by edit distance, when it is close
        /// enough to be a misspelling of it (at most a third of the name's length, and 3), or null.
        /// </summary>
        public static string SuggestName(string name, IEnumerable<EnvVar> catalog)
        {
            string upper = name.ToUpperInvariant();
            int limit = Math.Min(3, upper.Length / 3);
            string best = null;
            int bestDistance = int.MaxValue;
            foreach (var entry in catalog)
            {
                int distance = EditDistance(upper, entry.Name);
                if (distance <= limit && distance < bestDistance)
                {
                    best = entry.Name;
                    bestDistance = distance;
                }
            }
            return best;
        }

        /// <summary>The refusal of each set variable whose value its declaration does not accept.</summary>
        public static IReadOnlyList<string> ValueErrors(IEnumerable<EnvVar> catalog)
        {
            return catalog.Select(e => e.ValueError).Where(error => error != null).ToList();
        }

        /// <summary>The declarations whose variable is set, in name order.</summary>
        public static IReadOnlyList<EnvVar> SetVariables(IEnumerable<EnvVar> catalog)
        {
            return catalog.Where(e => e.IsSet).ToList();
        }

        private static IEnumerable<Assembly> OspreyAssemblies(Assembly root)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Queue<Assembly>();
            pending.Enqueue(root);
            seen.Add(root.GetName().Name ?? string.Empty);
            while (pending.Count > 0)
            {
                var assembly = pending.Dequeue();
                yield return assembly;
                foreach (var reference in assembly.GetReferencedAssemblies())
                {
                    string referenceName = reference.Name ?? string.Empty;
                    if (!referenceName.StartsWith(ASSEMBLY_PREFIX, StringComparison.Ordinal) || !seen.Add(referenceName))
                        continue;
                    pending.Enqueue(Assembly.Load(reference));
                }
            }
        }

        // Levenshtein distance, two rows.
        private static int EditDistance(string a, string b)
        {
            var previous = new int[b.Length + 1];
            var current = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++)
                previous[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int substitution = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                    current[j] = Math.Min(substitution, Math.Min(previous[j], current[j - 1]) + 1);
                }
                (previous, current) = (current, previous);
            }
            return previous[b.Length];
        }
    }
}
