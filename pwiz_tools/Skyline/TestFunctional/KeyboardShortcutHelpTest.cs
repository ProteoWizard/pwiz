/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Cursor (Claude Sonnet 4) <cursor .at. anysphere.co>
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Menus;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class KeyboardShortcutHelpTest : AbstractFunctionalTestEx
    {
        [TestMethod, NoParallelTesting(TestExclusionReason.WEB_BROWSER_USE)]
        public void TestKeyboardShortcutHelp()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            ValidateDocumentationInViewer();

            // Run all keyboard validation tests
            ValidateNoDuplicateKeyboardShortcuts();
            ValidateNoDuplicateMnemonics();
        }

        private void ValidateDocumentationInViewer()
        {
            // Test that the keyboard shortcuts documentation can be generated
            using var docViewerHelper = new DocumentationViewerHelper(TestContext, SkylineWindow.ShowKeyboardShortcutsDocumentation);
            RunUI(() =>
            {
                // Get the HTML content from WebView2 (not just the member variable)
                var html = docViewerHelper.DocViewer.GetWebView2HtmlContent();
                AssertEx.Contains(html, MenusResources.Keyboard_Shortcuts_Title);
                AssertEx.Contains(html, MenusResources.Keyboard_Shortcuts_Table_Title);
                AssertEx.Contains(html, MenusResources.Keyboard_Mnemonics_Table_Title);
                AssertEx.Contains(html, "<table");
                AssertEx.Contains(html, "</table>");
                    
                // Test that the number of <td> tags matches the expected number of rows
                var shortcutRows = KeyboardShortcutDocumentation.GetShortcutRows(SkylineWindow.MainMenuStrip);
                var mnemonicRows = KeyboardShortcutDocumentation.GetMnemonicRows(SkylineWindow.MainMenuStrip);
                var gridViewRows = KeyboardShortcutDocumentation.GetGridViewShortcutRows();
                var expectedTdCount = (shortcutRows.Count + mnemonicRows.Count + gridViewRows.Count) * 2; // 2 columns per row
                    
                var actualTdCount = html.Split(new[] { "<td>" }, StringSplitOptions.None).Length - 1;
                Assert.AreEqual(expectedTdCount, actualTdCount, 
                    $"Expected {expectedTdCount} <td> tags ({shortcutRows.Count + mnemonicRows.Count + gridViewRows.Count} rows × 2 columns), but found {actualTdCount}");
            });
        }

        private void ValidateNoDuplicateKeyboardShortcuts()
        {
            RunUI(() =>
            {
                // Extract all keyboard shortcuts from the menu structure using the public method
                var shortcuts = KeyboardShortcutDocumentation.GetShortcutRows(SkylineWindow.MainMenuStrip);

                // Group by shortcut to find duplicates
                var duplicateGroups = shortcuts
                    .GroupBy(s => s.Shortcut, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .ToList();

                if (duplicateGroups.Any())
                {
                    var errorLines = new List<string> { "Duplicate keyboard shortcuts found:" };

                    foreach (var group in duplicateGroups)
                    {
                        errorLines.Add(string.Empty);
                        errorLines.Add($"Shortcut '{group.Key}' is used by:");
                        errorLines.AddRange(group.Select(item => $"  - {item.MenuPath}"));
                    }

                    Assert.Fail(TextUtil.LineSeparate(errorLines));
                }
            });
        }

        private void ValidateNoDuplicateMnemonics()
        {
            RunUI(() =>
            {
                // Extract all mnemonics from the menu structure using the public method
                var mnemonics = KeyboardShortcutDocumentation.GetMnemonicRows(SkylineWindow.MainMenuStrip);

                // Group by mnemonic to find duplicates
                var duplicateGroups = mnemonics
                    .GroupBy(m => m.Shortcut, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .ToList();

                if (duplicateGroups.Any())
                {
                    var errorLines = new List<string> { "Duplicate mnemonics found:" };

                    foreach (var group in duplicateGroups)
                    {
                        errorLines.Add(string.Empty);
                        errorLines.Add($"Mnemonic '{group.Key}' is used by:");
                        errorLines.AddRange(group.Select(item => $"  - {item.MenuPath}"));

                        // Only suggest resolutions for English locale
                        if (System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en")
                        {
                            var suggestions = SuggestMnemonicResolutions(group.ToList(), mnemonics);
                            if (suggestions.Any())
                            {
                                errorLines.Add("Suggested resolutions:".Indent(1, 2));
                                errorLines.AddRange(suggestions.Select(suggestion => suggestion.Indent(2, 2)));
                            }
                        }
                        else
                        {
                            errorLines.Add("Note: Check the English version for consistency and possible solutions.".Indent(1, 2));
                        }
                    }

                    Assert.Fail(TextUtil.LineSeparate(errorLines));
                }
            });
        }

        private static List<string> SuggestMnemonicResolutions(List<(string MenuPath, string Shortcut)> duplicateItems, 
            List<(string MenuPath, string Shortcut)> allMnemonics)
        {
            var suggestions = new List<string>();
            var usedMnemonics = allMnemonics.Select(m => m.Shortcut).ToHashSet(StringComparer.OrdinalIgnoreCase);
            
            foreach (var item in duplicateItems)
            {
                var alternatives = FindAlternativeMnemonics(item, usedMnemonics);
                if (alternatives.Any())
                {
                    var suggestion = $"{item.MenuPath} → {string.Join(", ", alternatives)}";
                    suggestions.Add(suggestion);
                }
            }
            
            return suggestions;
        }

        private static List<string> FindAlternativeMnemonics((string MenuPath, string Shortcut) item, 
            HashSet<string> usedMnemonics)
        {
            var alternatives = new List<string>();
            var baseMnemonic = item.Shortcut;
            
            // Extract the base path (everything before the last comma)
            var lastCommaIndex = baseMnemonic.LastIndexOf(',');
            var basePath = lastCommaIndex > 0 ? baseMnemonic.Substring(0, lastCommaIndex + 1) : "";
            var menuText = item.MenuPath.Split('→').Last().Trim();
            
            // Try each letter in the menu text
            foreach (var letter in menuText.Where(char.IsLetterOrDigit).Select(char.ToUpper))
            {
                var alternativeMnemonic = basePath + letter;
                if (!usedMnemonics.Contains(alternativeMnemonic) && !alternatives.Contains(alternativeMnemonic))
                {
                    alternatives.Add(alternativeMnemonic);
                }
            }
            
            // Also try common abbreviations and first letters of words
            var words = menuText.Split(' ', '→', '-', '_');
            foreach (var word in words)
            {
                if (word.Length > 0)
                {
                    var letter = char.ToUpper(word[0]);
                    if (char.IsLetterOrDigit(letter))
                    {
                        var alternativeMnemonic = basePath + letter;
                        if (!usedMnemonics.Contains(alternativeMnemonic) && !alternatives.Contains(alternativeMnemonic))
                        {
                            alternatives.Add(alternativeMnemonic);
                        }
                    }
                }
            }
            
            return alternatives.Take(3).ToList(); // Limit to 3 suggestions per item
        }
    }
}
