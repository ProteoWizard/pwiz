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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Menus;
using pwiz.SkylineTestUtil;
using System.Globalization;
using System.IO;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Documentation;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Properties;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Verifies that the HTML files in the "Documentation\Help" folder contain the same text
    /// as would be displayed on the "Help > Documentation" menu item.
    /// </summary>
    [TestClass]
    public class HelpDocumentationContentTest : AbstractUnitTestEx
    {
        protected override bool IsRecordMode => false;

        [TestMethod]
        public void TestKeyboardShortcutsHelpDocumentation()
        {
            ForEachLanguage(() =>
            {
                var filePath = Path.Combine(GetDocumentationHelpFolder(), "KeyboardShortcuts.html");
                var skylineWindow = new SkylineWindow();
                var html = FormatHtml(
                    KeyboardShortcutDocumentation.GenerateKeyboardShortcutHtml(skylineWindow.MainMenuStrip));
                VerifyFileContents(filePath, html);
            });
        }

        [TestMethod]
        public void TestReportsHelpDocumentation()
        {
            var srmDocument = new SrmDocument(SrmSettingsList.GetDefault());
            ForEachLanguage(() =>
            {
                var filePath = Path.Combine(GetDocumentationHelpFolder(), "Reports.html");
                var skylineDataSchema =
                    SkylineDataSchema.MemoryDataSchema(srmDocument, SkylineDataSchema.GetLocalizedSchemaLocalizer());
                var documentationGenerator = new DocumentationGenerator(
                    ColumnDescriptor.RootColumn(skylineDataSchema, typeof(SkylineDocument)))
                {
                    IncludeHidden = false
                };
                var html = documentationGenerator.GetDocumentationHtmlPage();
                VerifyFileContents(filePath, html);
            });
        }

        [TestMethod]
        public void TestCommandLineHelpDocumentation()
        {
            ForEachLanguage(() =>
            {
                var filePath = Path.Combine(GetDocumentationHelpFolder(), "CommandLine.html");
                var html = CommandArgs.GenerateUsageHtml();
                VerifyFileContents(filePath, html);
            });
        }

        private void ForEachLanguage(Action action)
        {
            foreach (var language in new []{"en", "ja", "zh-CHS"})
            {
                var cultureInfo = CultureInfo.GetCultureInfo(language);
                Assert.IsNotNull(cultureInfo);
                Assert.AreEqual(language, cultureInfo.Name);
                LocalizationHelper.CallWithCulture(cultureInfo, () =>
                {
                    action();
                    return 0;
                });
            }
            Assert.IsFalse(IsRecordMode, "Set IsRecordMode to false before commit");   // Avoid merging code with record mode left on.
        }

        /// <summary>
        /// Returns the folder where the generated HTML should be stored in the source tree which is
        /// a folder with the language name under the "Documentation\Help" folder.
        /// </summary>
        protected string GetDocumentationHelpFolder()
        {
            return Path.Combine(ExtensionTestContext.GetProjectDirectory("Documentation\\Help"),
                CultureInfo.CurrentCulture.Name);
        }

        protected void VerifyFileContents(string path, string expectedContents)
        {
            string actualContents = null;
            if (File.Exists(path))
            {
                actualContents = File.ReadAllText(path);
            }
            if (IsRecordMode)
            {
                if (actualContents != expectedContents)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, expectedContents);
                }
            }
            else
            {
                var message = "Rerun test with IsRecordMode=>true to update file contents";
                AssertEx.FileExists(path, message);
                AssertEx.NoDiff(expectedContents, actualContents, message);
            }
        }

        public static string FormatHtml(string html)
        {
            return html;
        }
    }
}
