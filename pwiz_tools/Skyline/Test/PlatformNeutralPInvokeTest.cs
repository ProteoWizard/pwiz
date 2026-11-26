/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// SkylineCmd runs on SkylineCore and the plain net10.0 assemblies under it, so they must not
    /// call Windows unguarded. The CA1416 analyzer reports an unguarded call to an API marked
    /// [SupportedOSPlatform], but it cannot see a P/Invoke that is not marked: such a call compiles
    /// and fails at run time on Linux. So every P/Invoke in these assemblies must be marked.
    /// </summary>
    [TestClass]
    public class PlatformNeutralPInvokeTest : AbstractUnitTest
    {
        /// <summary>
        /// The Skyline-side assemblies SkylineCmd loads. The Pwiz.* assemblies (pwiz-sharp and the
        /// vendor readers) belong to ProteoWizard and load vendor libraries that are Windows-only.
        /// </summary>
        private static readonly string[] PLATFORM_NEUTRAL_ASSEMBLIES =
        {
            @"SkylineCore",
            @"SkylineTool",
            @"pwiz.Common",
            @"pwiz.CommonMsData",
            @"pwiz.CommonUtil",
            @"PanoramaClient",
            @"ProteomeDb",
            @"ProteowizardWrapper",
            @"BiblioSpec.IO",
            @"BiblioSpecUtil"
        };

        [TestMethod]
        public void TestPlatformNeutralPInvokesMarked()
        {
            var assemblies = GetPlatformNeutralAssemblies();
            CollectionAssert.AreEquivalent(PLATFORM_NEUTRAL_ASSEMBLIES,
                assemblies.Select(assembly => assembly.GetName().Name).ToArray(),
                @"Every listed assembly must be among those SkylineCore loads");

            var unmarked = new List<string>();
            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
                        .Where(method => method.GetCustomAttribute<DllImportAttribute>() != null);
                    foreach (var method in methods)
                    {
                        if (!IsPlatformMarked(method))
                            unmarked.Add(assembly.GetName().Name + @": " + type.FullName + @"." + method.Name);
                    }
                }
            }
            Assert.AreEqual(0, unmarked.Count,
                @"P/Invokes without [SupportedOSPlatform], whose callers CA1416 cannot check: " + string.Join(@", ", unmarked));
        }

        private static List<Assembly> GetPlatformNeutralAssemblies()
        {
            var found = new Dictionary<string, Assembly>();
            var pending = new Queue<Assembly>();
            pending.Enqueue(typeof(SrmDocument).Assembly);
            while (pending.Count > 0)
            {
                var assembly = pending.Dequeue();
                var name = assembly.GetName().Name;
                if (found.ContainsKey(name) || !PLATFORM_NEUTRAL_ASSEMBLIES.Contains(name))
                    continue;
                found.Add(name, assembly);
                foreach (var reference in assembly.GetReferencedAssemblies())
                {
                    if (PLATFORM_NEUTRAL_ASSEMBLIES.Contains(reference.Name) && !found.ContainsKey(reference.Name))
                        pending.Enqueue(Assembly.Load(reference));
                }
            }
            return found.Values.ToList();
        }

        private static bool IsPlatformMarked(MethodInfo method)
        {
            if (method.GetCustomAttribute<SupportedOSPlatformAttribute>() != null)
                return true;
            for (var type = method.DeclaringType; type != null; type = type.DeclaringType)
            {
                if (type.GetCustomAttribute<SupportedOSPlatformAttribute>() != null)
                    return true;
            }
            return false;
        }
    }
}
