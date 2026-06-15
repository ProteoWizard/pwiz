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

namespace pwiz.OspreySharp.Core
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
    /// the standalone build.ps1) stamp from the OspreySharp Jamfile constants.
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
    /// Lives in OspreySharp.Core so every project (the pipeline tasks, IO, FDR,
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
    }
}
