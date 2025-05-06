/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class LocalizedResourcesTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestLocalizedResources()
        {
            foreach (var assembly in GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    var resourceManager = GetResourceManager(type);
                    if (resourceManager != null)
                    {
                        VerifyResourceManager(resourceManager);
                    }
                }
            }
        }

        private void VerifyResourceManager(ResourceManager resourceManager)
        {
            var cultureInfos = new[] { "ja", "zh-CHS" }.Select(CultureInfo.GetCultureInfo).ToList();
            foreach (DictionaryEntry invariantEntry in resourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, true).Cast<DictionaryEntry>().OrderBy(entry=>entry.Key))
            {
                foreach (var cultureInfo in cultureInfos)
                {
                    var message = string.Format("{0} Entry:{1} Language:{2}", resourceManager.BaseName,
                        invariantEntry.Key, cultureInfo.Name);
                    var resourceSet = resourceManager.GetResourceSet(cultureInfo, true, true);
                    ValidateLocalizedResource(invariantEntry.Value, resourceSet.GetObject(invariantEntry.Key.ToString()), message);
                }
            }
        }

        [TestMethod]
        public void TestValidateLocalizedResource()
        {
            Assert.AreEqual(-1, MaxSubstitutionIndex("2"));
            Assert.AreEqual(3, MaxSubstitutionIndex("Hello, {3} my name is {1}"));
            Assert.AreEqual(2, MaxSubstitutionIndex("Hello, {2}"));
            AssertEx.ThrowsException<AssertFailedException>(() =>
                ValidateLocalizedResource("L&earning document:", "学習ドキュメント(&e):", string.Empty));
            AssertEx.ThrowsException<AssertFailedException>(() =>
                ValidateLocalizedResource("Two {0} substitutions {1}", "One substitution {0}", string.Empty));
        }

        private static readonly Regex RegexLowercaseMnemonic = new Regex("\\(&[a-z]");
        /// <summary>
        /// Verifies that the localized value is compatible with the invariant value.
        /// Ensures that it has the same number of arguments in curly braces (actually, just ensures that the highest number in curly braces is the same-- it's okay
        /// with intermediate values being ignored in one or the other language).
        /// Ensures that any mnemonics in parentheses are not lowercase. 
        /// </summary>
        private void ValidateLocalizedResource(object invariantValue, object localizedValue, string message)
        {
            if (localizedValue == null)
            {
                return;
            }
            Assert.AreEqual(invariantValue?.GetType(), localizedValue.GetType(), message);
            var invariantText = invariantValue as string;
            if (invariantText != null)
            {
                var localizedText = (string)localizedValue;
                Assert.AreEqual(MaxSubstitutionIndex(invariantText), MaxSubstitutionIndex(localizedText), message);
                StringAssert.DoesNotMatch(localizedText, RegexLowercaseMnemonic, "Mnemonic should be uppercase: {0}", message);
            }
        }

        public static IEnumerable<Assembly> GetAssemblies()
        {
            yield return typeof(Skyline.SkylineWindow).Assembly;
            yield return typeof(ProteomeDatabase.API.ProteomeDb).Assembly;
            yield return typeof(ZedGraph.ZedGraphControl).Assembly;
            yield return typeof(Common.SystemUtil.CommonFormEx).Assembly;
            yield return typeof(MSGraph.MSGraphPane).Assembly;
            yield return typeof(ProteowizardWrapper.MsDataFileImpl).Assembly;
        }

        /// <summary>
        /// Returns the highest number that is in curly braces in the string, or -1
        /// if there are no numbers in curly braces.
        /// </summary>
        private int MaxSubstitutionIndex(string text)
        {
            int maxSubstitution = -1;
            bool prevEmpty = false;
            foreach (var part in text.Split('{').Skip(1))
            {
                if (prevEmpty)
                {
                    prevEmpty = false;
                    // Two curly braces in a row should be ignored
                    continue;
                }

                prevEmpty = part.Length == 0;
                if (prevEmpty)
                {
                    continue;
                }

                var digits = new string(part.TakeWhile(char.IsDigit).ToArray());
                if (digits.Length > 0)
                {
                    maxSubstitution = Math.Max(maxSubstitution, int.Parse(digits));
                }
            }
            return maxSubstitution;
        }

        private ResourceManager GetResourceManager(Type type)
        {
            var resourceManagerProperty = type.GetProperty(nameof(Skyline.Properties.Resources.ResourceManager),
                BindingFlags.Static | BindingFlags.NonPublic);
            if (resourceManagerProperty != null)
            {
                var resourceManager = resourceManagerProperty.GetValue(null) as ResourceManager;
                if (resourceManager != null)
                {
                    return resourceManager;
                }
            }

            if (typeof(Control).IsAssignableFrom(type))
            {
                var resourceManager = new ComponentResourceManager(type);
                try
                {
                    resourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, true);
                    return resourceManager;
                }
                catch
                {
                    // ignore
                }
            }

            return null;
        }
    }
}
