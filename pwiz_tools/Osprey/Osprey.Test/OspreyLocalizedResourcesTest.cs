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

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.IO;
using pwiz.Osprey.Scoring;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Osprey's counterpart of Skyline's LocalizedResourcesTest: every Japanese and Chinese
    /// resource must be usable in place of the English one. A translation that drops or
    /// renumbers a placeholder throws or prints the wrong value at run time, and a translated
    /// "Error:" that the exit-code detector does not recognize makes the log and the exit code
    /// disagree - neither shows up in an English run.
    /// </summary>
    [TestClass]
    public class OspreyLocalizedResourcesTest
    {
        private static readonly string[] LANGUAGES = { @"ja", @"zh-Hans" };

        // A format item: {0}, {1:N0}, {2,5:F1}. Doubled braces are literal text, not items.
        private static readonly Regex FORMAT_ITEM = new Regex(@"(?<!\{)\{\d+(?:,-?\d+)?(?::[^{}]*)?\}(?!\})");

        [TestMethod]
        public void TestLocalizedResources()
        {
            var resourceManagers = GetResourceManagers().ToList();
            // Every assembly that writes user text has a resource class; losing one from the
            // scan would silently stop checking its translations.
            foreach (var type in new[]
                     {
                         typeof(OspreyChromatographyResources), typeof(OspreyCoreResources),
                         typeof(OspreyFDRResources), typeof(OspreyIOResources), typeof(OspreyScoringResources),
                         typeof(OspreyTasksResources), typeof(OspreyResources), typeof(OspreyCommandArgUsage)
                     })
            {
                Assert.IsTrue(resourceManagers.Any(rm => rm.BaseName == type.FullName),
                    string.Format(@"{0} is missing from the scanned resource managers", type.FullName));
            }

            foreach (var resourceManager in resourceManagers)
                VerifyResourceManager(resourceManager);
        }

        private static void VerifyResourceManager(ResourceManager resourceManager)
        {
            var invariantSet = resourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, true);
            Assert.IsNotNull(invariantSet, resourceManager.BaseName);
            foreach (var language in LANGUAGES)
            {
                // tryParents: false, so a missing satellite or entry is not hidden by the English.
                var localizedSet = resourceManager.GetResourceSet(CultureInfo.GetCultureInfo(language), true, false);
                Assert.IsNotNull(localizedSet,
                    string.Format(@"{0} has no {1} satellite resources", resourceManager.BaseName, language));
                foreach (var entry in invariantSet.Cast<DictionaryEntry>().OrderBy(e => (string) e.Key))
                {
                    string message = string.Format(@"{0} Entry:{1} Language:{2}", resourceManager.BaseName, entry.Key, language);
                    var invariantText = entry.Value as string;
                    if (invariantText == null)
                        continue;
                    // Skyline's standard: an untranslated string carries the English, so every
                    // entry exists in every language file.
                    var localizedText = localizedSet.GetString((string) entry.Key);
                    Assert.IsNotNull(localizedText, message + @" is missing");
                    CollectionAssert.AreEquivalent(FormatItems(invariantText), FormatItems(localizedText),
                        message + @" has different format items: " + localizedText);
                    Assert.AreEqual(CommandStatusWriter.IsErrorLine(invariantText), CommandStatusWriter.IsErrorLine(localizedText),
                        message + @" disagrees with the English about being an error line: " + localizedText);
                }
            }
        }

        private static List<string> FormatItems(string text)
        {
            return FORMAT_ITEM.Matches(text).Select(m => m.Value).ToList();
        }

        private static IEnumerable<ResourceManager> GetResourceManagers()
        {
            var assemblies = new[]
            {
                typeof(OspreyChromatographyResources).Assembly, typeof(OspreyCoreResources).Assembly,
                typeof(OspreyFDRResources).Assembly, typeof(OspreyIOResources).Assembly,
                typeof(OspreyScoringResources).Assembly, typeof(OspreyTasksResources).Assembly,
                typeof(OspreyResources).Assembly
            }.Distinct();
            foreach (var type in assemblies.SelectMany(assembly => assembly.GetTypes()))
            {
                var property = type.GetProperty(nameof(OspreyCoreResources.ResourceManager),
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.GetValue(null) is ResourceManager resourceManager)
                    yield return resourceManager;
            }
        }
    }
}
