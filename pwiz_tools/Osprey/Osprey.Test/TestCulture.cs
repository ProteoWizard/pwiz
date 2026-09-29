/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Runs the whole test assembly under the culture named by <c>OSPREY_TEST_CULTURE</c>
    /// (e.g. <c>ja-JP</c>, <c>fr-FR</c>), the way Skyline's TestRunner runs its tests under
    /// <c>/locale</c>. ja-JP proves an assertion reads its expected text from the resources and
    /// not from an English literal; fr-FR proves every number a program reads is written in the
    /// invariant culture, because fr-FR writes <c>1,5</c> and groups thousands with a space.
    /// Osprey runs under <c>vstest.console</c>, not TestRunner, so the switch is an environment
    /// variable: <c>Build-Osprey.ps1 -RunTests -Culture fr-FR</c> sets it.
    /// </summary>
    [TestClass]
    public static class TestCulture
    {
        public const string ENV_VAR = @"OSPREY_TEST_CULTURE";

        private static CultureScope _scope;

        [AssemblyInitialize]
        public static void Initialize(TestContext context)
        {
            string name = Environment.GetEnvironmentVariable(ENV_VAR);
            if (!string.IsNullOrEmpty(name))
                _scope = new CultureScope(CultureInfo.GetCultureInfo(name));
        }

        [AssemblyCleanup]
        public static void Cleanup()
        {
            _scope?.Dispose();
            _scope = null;
        }
    }
}
