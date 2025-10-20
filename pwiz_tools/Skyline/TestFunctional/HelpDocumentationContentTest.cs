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
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Documentation;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Menus;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that the HTML files in the "Documentation\Help" folder contain the same text
    /// as would be displayed on the "Help > Documentation" menu item.
    /// </summary>
    [TestClass]
    public class HelpDocumentationContentTest : AbstractFunctionalTest
    {
        protected override bool IsRecordMode => false;

        [TestMethod]
        public void TestKeyboardShortcutsHelpDocumentation()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            ForEachLanguage(() =>
            {
                RunUI(() =>
                {
                    var filePath = Path.Combine(GetDocumentationHelpFolder(), "KeyboardShortcuts.html");
                    ApplyResourcesToSkylineMenus(SkylineWindow);
                    var html = KeyboardShortcutDocumentation.GenerateKeyboardShortcutHtml(Program.MainWindow.MainMenuStrip);
                    VerifyFileContents(filePath, html);
                });
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
            // Reset the settings before the test since some command-line options like "--background-proteome-name" include
            // values from the user settings.
            Settings.Default.Reset();
            ForEachLanguage(() =>
            {
                var filePath = Path.Combine(GetDocumentationHelpFolder(), "CommandLine.html");
                var html = CommandArgs.GenerateUsageHtml();
                VerifyFileContents(filePath, html);
            });
        }

        private void ForEachLanguage(Action action)
        {
            foreach (var language in new []{ "en", "ja", "zh-CHS" })
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

        /// <summary>
        /// Localizes all items on Skyline main menu based on CultureInfo.CurrentUICulture
        /// </summary>
        /// <param name="skylineWindow"></param>
        private static void ApplyResourcesToSkylineMenus(SkylineWindow skylineWindow)
        {
            var processedItems = new HashSet<ReferenceValue<ToolStripItem>>();
            foreach ((ComponentResourceManager resourceManager, IEnumerable<ToolStripItem> items) in GetMenuItems(skylineWindow))
            {
                ApplyResources(resourceManager, items, processedItems);
            }
        }

        private static void ApplyResources(ComponentResourceManager resourceManager, IEnumerable<ToolStripItem> items,
            HashSet<ReferenceValue<ToolStripItem>> processedItems)
        {
            foreach (var item in items ?? Array.Empty<ToolStripItem>())
            {
                if (!processedItems.Add(item))
                {
                    continue;
                }
                resourceManager.ApplyResources(item, item.Name);
                ApplyResources(resourceManager, (item as ToolStripDropDownItem)?.DropDownItems.Cast<ToolStripItem>(), processedItems);
            }
        }

        private static IEnumerable<Tuple<ComponentResourceManager, IEnumerable<ToolStripItem>>> GetMenuItems(
            SkylineWindow skylineWindow)
        {
            yield return Tuple.Create(new ComponentResourceManager(typeof(RefineMenu)), skylineWindow.RefineMenu.DropDownItems);
            yield return Tuple.Create(new ComponentResourceManager(typeof(EditMenu)), skylineWindow.EditMenu.DropDownItems);
            yield return Tuple.Create(new ComponentResourceManager(typeof(ViewMenu)), skylineWindow.ViewMenu.DropDownItems);
            yield return Tuple.Create(new ComponentResourceManager(typeof(SkylineWindow)),
                skylineWindow.MainMenuStrip.Items.OfType<ToolStripItem>());
        }
    }
}
