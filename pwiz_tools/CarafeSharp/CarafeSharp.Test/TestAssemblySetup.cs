/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Proteome;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// The test process stands in for CarafeSharp.exe, whose Main installs Carafe's grammar and
    /// text on the process-wide command-line seams; the tests parse and render help under them
    /// the same way. Nothing else installs them: hosted in Skyline or Osprey, the declarations
    /// run under the host's settings.
    /// </summary>
    [TestClass]
    public static class TestAssemblySetup
    {
        [AssemblyInitialize]
        public static void InstallCarafeArgUsage(TestContext context)
        {
            CarafeCommandLine.InstallGrammar();
        }
    }
}
