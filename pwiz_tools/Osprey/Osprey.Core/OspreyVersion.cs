/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Single source of truth for the logical "osprey version" -- the value
    /// stamped into the blib (<c>osprey_version</c>), the parquet/sidecar score
    /// caches (<c>osprey.version</c>), and read back by
    /// <c>ParquetScoreCache.CheckParquetMetadata</c> to decide whether a cache is
    /// still compatible with the current binary. It follows the Skyline scheme
    /// (<c>YEAR.ORDINAL.BRANCH.DOY</c>); a cached score file is reused only when
    /// this version matches the current binary exactly. Any difference
    /// (release line or daily build) aborts cache reuse.
    ///
    /// By default this reflects the assembly version, which the Boost build (and
    /// the standalone build.ps1) stamp from the Osprey Jamfile constants.
    /// The <c>Directory.Build.props</c> placeholder is the fallback for ad-hoc
    /// `dotnet build` invocations that bypass those scripts.
    ///
    /// Bit-parity override: the regression harness needs the stamped version to be
    /// deterministic so the committed blib golden (which compares the
    /// <c>osprey_version</c> metadata cell exactly) does not go red on the
    /// daily-changing build version. Setting <c>OSPREY_VERSION_OVERRIDE</c> pins
    /// <see cref="Current"/> to a canonical constant for every invocation in the
    /// run. This is a diagnostic seam (mirroring the OSPREY_* env vars in
    /// <see cref="OspreyEnvironment"/>), not a production code path: the version
    /// stamp is pure provenance, so pinning it cannot mask any algorithmic drift.
    ///
    /// Lives in Osprey.Core so every project (the pipeline tasks, IO, FDR,
    /// ...) reads the same value without depending on the main exe.
    /// </summary>
    public static class OspreyVersion
    {
        /// <summary>
        /// OSPREY_VERSION_OVERRIDE: pins the logical version to a fixed string,
        /// regardless of the assembly version. Set only by the bit-parity
        /// regression harness; never in production. Read once at process start.
        /// </summary>
        public static readonly string Current = ResolveVersion();

        /// <summary>
        /// The full informational version stamped by the build:
        /// <c>YEAR.ORDINAL.BRANCH.DOY-&lt;shorthash&gt;[-dirty]</c>. Mirrors the
        /// Skyline <c>AssemblyInformationalVersion</c> scheme (see
        /// pwiz_tools/Skyline/Util/Install.cs) so a binary is always traceable to
        /// its source commit -- the defect that made a build-vs-build FDRBench
        /// attribution impossible (TODO-osprey_version_git_hash.md). The hash is
        /// stamped on every build path by pwiz_tools/Osprey/Directory.Build.targets;
        /// falls back to the bare numeric <see cref="Current"/> for a non-git build
        /// or under the bit-parity <c>OSPREY_VERSION_OVERRIDE</c>.
        /// </summary>
        public static readonly string InformationalVersion = ResolveInformationalVersion();

        /// <summary>
        /// Git short hash parsed from <see cref="InformationalVersion"/> (empty for
        /// a non-git build). Follows the Skyline split convention: the token after
        /// the numeric version, without any trailing <c>-dirty</c> marker.
        /// </summary>
        public static string GitHash => ExtractGitHash(InformationalVersion);

        /// <summary>
        /// Human-facing version, Skyline-style <c>26.1.1.182 (b2373f9f9c)</c> (or
        /// <c>... (b2373f9f9c-dirty)</c>); the plain numeric version when no hash
        /// is stamped. Used by <c>--version</c> and the startup log.
        /// </summary>
        public static string DisplayVersion => FormatDisplay(InformationalVersion);

        private static string ResolveVersion()
        {
            string overrideValue = Environment.GetEnvironmentVariable(@"OSPREY_VERSION_OVERRIDE");
            if (!string.IsNullOrEmpty(overrideValue))
                return overrideValue;
            // The assembly version carries the Skyline-scheme YEAR.ORDINAL.BRANCH.DOY
            // stamped by the build; Version.ToString() renders all four components.
            var version = typeof(OspreyVersion).Assembly.GetName().Version;
            return version != null ? version.ToString() : @"0.0.0.0";
        }

        private static string ResolveInformationalVersion()
        {
            // Honor the bit-parity pin so --version and provenance stay
            // deterministic under the regression harness (the override string
            // carries no hash; DisplayVersion then renders it as-is).
            string overrideValue = Environment.GetEnvironmentVariable(@"OSPREY_VERSION_OVERRIDE");
            if (!string.IsNullOrEmpty(overrideValue))
                return overrideValue;
            var attr = typeof(OspreyVersion).Assembly
                .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
                .OfType<AssemblyInformationalVersionAttribute>().FirstOrDefault();
            string info = attr?.InformationalVersion;
            if (string.IsNullOrEmpty(info))
                return Current;
            // Defensive: strip any SDK-appended source-revision metadata ("+<sha>").
            // We stamp the hash ourselves as a "-<hash>" suffix and disable the SDK
            // append (IncludeSourceRevisionInInformationalVersion=false).
            int plus = info.IndexOf('+');
            return plus >= 0 ? info.Substring(0, plus) : info;
        }

        /// <summary>
        /// Extracts the git short hash from an informational version such as
        /// <c>26.1.1.182-b2373f9f9c</c> or <c>26.1.1.182-b2373f9f9c-dirty</c>.
        /// Returns the empty string for a bare numeric version (no hash stamped).
        /// </summary>
        internal static string ExtractGitHash(string informational)
        {
            if (string.IsNullOrEmpty(informational))
                return string.Empty;
            // "26.1.1.182-b2373f9f9c[-dirty]".Split('-') -> the hash is element [1]
            // (Skyline Install.GitHash convention); a trailing "dirty" is element [2].
            var parts = informational.Split('-');
            return parts.Length > 1 ? parts[1] : string.Empty;
        }

        /// <summary>
        /// Renders an informational version Skyline-style: the numeric version
        /// followed by the hash (and any <c>-dirty</c> marker) in parentheses.
        /// A bare numeric version is returned unchanged.
        /// </summary>
        internal static string FormatDisplay(string informational)
        {
            if (string.IsNullOrEmpty(informational))
                return informational;
            // "26.1.1.182-b2373f9f9c[-dirty]" -> "26.1.1.182 (b2373f9f9c[-dirty])"
            return Regex.Replace(informational, @"^(\d+\.\d+\.\d+\.\d+)-(.+)$", @"$1 ($2)");
        }
    }
}
