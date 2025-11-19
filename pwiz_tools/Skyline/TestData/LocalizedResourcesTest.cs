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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineRunner;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData
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
                    var localizedValue = resourceSet.GetObject(invariantEntry.Key.ToString());
                    ValidateLocalizedResource(invariantEntry.Value, localizedValue, message);
                    ValidateErrorCheckerIsErrorLine(invariantEntry.Value, localizedValue, cultureInfo, message);
                }
            }
        }

        private void ValidateErrorCheckerIsErrorLine(object invariantValue, object localizedValue, CultureInfo cultureInfo,
            string message)
        {
            if (localizedValue == null || !(invariantValue is string invariantText))
            {
                return;
            }
            var localizedText = (string)localizedValue;
            if (ErrorChecker.IsErrorLine(invariantText))
            {
                StringAssert.StartsWith(invariantText, GetErrorTranslation(CultureInfo.InvariantCulture));
                if (localizedText != invariantText)
                {
                    var localizedError = GetErrorTranslation(cultureInfo);
                    if (localizedError != null)
                    {
                        StringAssert.StartsWith(localizedText, localizedError, message);
                        var localizedErrorWithColon =
                            LocalizationHelper.CallWithCulture(cultureInfo, () => TextUtil.AppendColon(localizedError));
                        StringAssert.StartsWith(localizedText, localizedErrorWithColon, "Localized message should use localized colon character. {0}", message);
                    }
                }
                Assert.IsTrue(ErrorChecker.IsErrorLine(localizedText), "Localized text should start with localized form of 'Error:' {0}", message);
            }
            else
            {
                Assert.IsFalse(ErrorChecker.IsErrorLine(localizedText), message);
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

        [TestMethod]
        public void TestLocalizedResourcesGetAssemblies()
        {
            var assemblies = GetAssemblies().ToList();
            foreach (var assembly in new[]
                     {
                         typeof(Skyline.SkylineWindow).Assembly,
                         typeof(ProteomeDatabase.API.ProteomeDb).Assembly,
                         typeof(CommonFormEx).Assembly,
                         typeof(MSGraph.MSGraphPane).Assembly,
                         typeof(ProteowizardWrapper.MsDataFileImpl).Assembly
                     })
            {
                Assert.IsTrue(assemblies.Contains(assembly), "Assembly {0} should have been included in list returned by GetAssemblies()", assembly.FullName);
            }
        }

        [TestMethod]
        public void TestLocalizedResourcesGetTypes()
        {
            var allTypes = GetAssemblies().SelectMany(assembly => assembly.GetTypes()).ToList();
            var toolsUiResourcesType = allTypes.FirstOrDefault(type => type == typeof(Skyline.ToolsUI.ToolsUIResources));
            Assert.IsNotNull(toolsUiResourcesType);
            Assert.IsNotNull(GetResourceManager(toolsUiResourcesType));
            var propertiesResourcesType = allTypes.FirstOrDefault(type => type == typeof(Skyline.Properties.Resources));
            Assert.IsNotNull(propertiesResourcesType);
            Assert.IsNotNull(GetResourceManager(propertiesResourcesType));
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
            if (invariantText == null)
            {
                return;
            }

            var localizedText = (string)localizedValue;
            Assert.AreEqual(MaxSubstitutionIndex(invariantText), MaxSubstitutionIndex(localizedText), message);
            StringAssert.DoesNotMatch(localizedText, RegexLowercaseMnemonic, "Mnemonic should be uppercase: {0}", message);
        }

        public static IEnumerable<Assembly> GetAssemblies()
        {
            var skylineAssembly = typeof(Skyline.SkylineWindow).Assembly;
            yield return skylineAssembly;
            yield return typeof(ProteomeDatabase.API.ProteomeDb).Assembly;
            yield return typeof(CommonFormEx).Assembly;
            yield return typeof(MSGraph.MSGraphPane).Assembly;
            yield return typeof(ProteowizardWrapper.MsDataFileImpl).Assembly;
            yield return Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(skylineAssembly.Location)!, "ZedGraph.dll"));
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
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
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

        /// <summary>
        /// Returns the localized text representing the word "Error", or null if this test does not know
        /// the translation. This text is expected to match what is in <see cref="ErrorChecker.INTL_ERROR_PREFIXES"/>
        /// without the trailing colon.
        /// </summary>
        private static string GetErrorTranslation(CultureInfo cultureInfo)
        {
            if (cultureInfo.TwoLetterISOLanguageName == "ja")
            {
                return "エラー";
            }

            if (cultureInfo.TwoLetterISOLanguageName == "zh")
            {
                return "错误";
            }

            if (string.IsNullOrEmpty(cultureInfo.Name) || cultureInfo.TwoLetterISOLanguageName == "en")
            {
                return "Error";
            }
            return null;
        }
    }
}
