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

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Web;
using System.Windows.Forms;
using pwiz.Common.DataBinding.Documentation;

namespace pwiz.Skyline.Menus
{
    [SuppressMessage("ReSharper", "LocalizableElement")]
    public static class KeyboardShortcutDocumentation
    {
        public static string GenerateKeyboardShortcutHtml(MenuStrip menuMain)
        {
            if (menuMain == null)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("<html><head>");
            sb.AppendLine(@"<meta charset=""utf-8"">");
            sb.AppendLine(DocumentationGenerator.GetStyleSheetHtml());
            sb.AppendLine("</head><body>");
            sb.Append("<h2>").Append(HtmlEncode(MenusResources.Keyboard_Shortcuts_Title)).AppendLine("</h2>");

            // Add explanatory text
            sb.Append("<p>").Append(HtmlEncode(MenusResources.Keyboard_Shortcuts_Explanation_Intro)).AppendLine("</p>");
            sb.AppendLine("<ul>");
            sb.Append("<li><strong>").Append(MenusResources.Keyboard_Shortcuts_Table_Title).Append("</strong> ")
                .Append(HtmlEncode(MenusResources.Keyboard_Shortcuts_Explanation_Shortcuts)).AppendLine("</li>");
            sb.Append("<li><strong>").Append(MenusResources.Keyboard_Mnemonics_Table_Title).Append("</strong> ")
                .Append(HtmlEncode(MenusResources.Keyboard_Shortcuts_Explanation_Mnemonics)).AppendLine("</li>");
            sb.AppendLine("</ul>");

            // Keyboard shortcuts table
            AddShortcutsTable(sb, 
                GetShortcutRows(menuMain), 
                MenusResources.Keyboard_Shortcuts_Table_Title, 
                MenusResources.Keyboard_Shortcuts_Header_Shortcut);

            // Mnemonics table
            AddShortcutsTable(sb, 
                GetMnemonicRows(menuMain), 
                MenusResources.Keyboard_Mnemonics_Table_Title, 
                MenusResources.Keyboard_Mnemonics_Header_Mnemonic);

            // GridView shortcuts table
            AddShortcutsTable(sb, 
                GetGridViewShortcutRows(), 
                MenusResources.Keyboard_GridView_Table_Title, 
                MenusResources.Keyboard_GridView_Header_Action);
            
            sb.Append("</body></html>");
            return sb.ToString();
        }

        private static void AddShortcutsTable(StringBuilder sb, 
            List<(string MenuPath, string Shortcut)> rows, 
            string tableTitle, 
            string shortcutColumnHeader)
        {
            sb.Append("<h3>").Append(HtmlEncode(tableTitle)).AppendLine("</h3>");
            sb.AppendLine(@"<table border=""1"" cellspacing=""0"" cellpadding=""4"">");
            sb.Append("<thead><tr>");
            AddHeader(sb, MenusResources.Keyboard_Shortcuts_Header_Menu);  // Single header for both tables
            AddHeader(sb, shortcutColumnHeader);
            sb.AppendLine("</tr></thead><tbody>");

            foreach (var row in rows)
            {
                sb.Append("<tr>");
                AddCell(sb, row.MenuPath);
                AddCell(sb, row.Shortcut);
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</tbody></table>");
        }

        private static void AddHeader(StringBuilder sb, string headerText)
        {
            sb.Append("<th>").Append(HtmlEncode(headerText)).Append("</th>");
        }

        private static void AddCell(StringBuilder sb, string cellText)
        {
            sb.Append("<td>").Append(HtmlEncode(cellText)).Append("</td>");
        }

        public static List<(string MenuPath, string Shortcut)> GetShortcutRows(MenuStrip menuMain)
        {
            var rows = new List<(string MenuPath, string Shortcut)>();
            foreach (var topItem in menuMain.Items.OfType<ToolStripMenuItem>())
            {
                AppendMenuItems(rows, parentPath: CleanMenuText(topItem.Text), items: topItem.DropDownItems);
            }
            return rows;
        }

        public static List<(string MenuPath, string Shortcut)> GetMnemonicRows(MenuStrip menuMain)
        {
            var rows = new List<(string MenuPath, string Shortcut)>();
            foreach (var topItem in menuMain.Items.OfType<ToolStripMenuItem>())
            {
                AppendMenuItemsForMnemonics(rows, parentPath: CleanMenuText(topItem.Text), items: topItem.DropDownItems);
            }
            return rows;
        }

        public static List<(string MenuPath, string Shortcut)> GetGridViewShortcutRows()
        {
            var gridViewShortcuts = new List<(Keys Keys, string Action)>
            {
                (Keys.D | Keys.Control, MenusResources.GridView_Action_FillDown),
                (Keys.R | Keys.Control, MenusResources.GridView_Action_FillRight),
                (Keys.F2, MenusResources.GridView_Action_EnterEditMode),
                (Keys.Escape, MenusResources.GridView_Action_CancelEditMode),
                (Keys.Enter, MenusResources.GridView_Action_ConfirmEditMoveDown),
                (Keys.Tab, MenusResources.GridView_Action_ConfirmEditMoveRight),
                (Keys.Tab | Keys.Shift, MenusResources.GridView_Action_ConfirmEditMoveLeft),
                (Keys.A | Keys.Control, MenusResources.GridView_Action_SelectAll),
                (Keys.D0 | Keys.Control, MenusResources.GridView_Action_ClearCell),
                (Keys.Space | Keys.Shift, MenusResources.GridView_Action_SelectRow),
                (Keys.Down, MenusResources.GridView_Action_MoveDownOneCell),
                (Keys.Down | Keys.Control, MenusResources.GridView_Action_MoveToEndOfColumn),
                (Keys.Down | Keys.Control | Keys.Shift, MenusResources.GridView_Action_SelectToEndOfColumn),
                (Keys.Up, MenusResources.GridView_Action_MoveUpOneCell),
                (Keys.Up | Keys.Control, MenusResources.GridView_Action_MoveToBeginningOfColumn),
                (Keys.Up | Keys.Control | Keys.Shift, MenusResources.GridView_Action_SelectToBeginningOfColumn),
                (Keys.Left, MenusResources.GridView_Action_MoveLeftOneCell),
                (Keys.Left | Keys.Control, MenusResources.GridView_Action_MoveToBeginningOfRow),
                (Keys.Left | Keys.Control | Keys.Shift, MenusResources.GridView_Action_SelectToBeginningOfRow),
                (Keys.Right, MenusResources.GridView_Action_MoveRightOneCell),
                (Keys.Right | Keys.Control, MenusResources.GridView_Action_MoveToEndOfRow),
                (Keys.Right | Keys.Control | Keys.Shift, MenusResources.GridView_Action_SelectToEndOfRow),
                (Keys.Home, MenusResources.GridView_Action_MoveToFirstCellInRow),
                (Keys.Home | Keys.Control, MenusResources.GridView_Action_MoveToFirstCellInGrid),
                (Keys.End, MenusResources.GridView_Action_MoveToLastCellInRow),
                (Keys.End | Keys.Control, MenusResources.GridView_Action_MoveToLastCellInGrid)
            };

            return gridViewShortcuts.Select(item => (GetShortcut(item.Keys), item.Action)).ToList();
        }

        private static string GetShortcut(Keys keys)
        {
            var converter = new KeysConverter();
            var str = converter.ConvertToInvariantString(keys);
            return str?.Replace(", ", "+") ?? string.Empty;
        }

        private static void AppendMenuItems(List<(string MenuPath, string Shortcut)> rows, string parentPath, ToolStripItemCollection items)
        {
            if (items == null)
                return;

            foreach (var item in items)
            {
                if (item is ToolStripSeparator)
                    continue;

                var menuItem = item as ToolStripMenuItem;
                if (menuItem == null)
                    continue;

                var text = CleanMenuText(menuItem.Text);
                var fullPath = string.IsNullOrEmpty(parentPath) ? text : parentPath + " → " + text;

                // Recurse into children first to build full paths
                if (menuItem.HasDropDownItems)
                {
                    AppendMenuItems(rows, fullPath, menuItem.DropDownItems);
                }

                // Include only items with an explicit shortcut
                var shortcut = GetShortcut(menuItem);
                if (!string.IsNullOrEmpty(shortcut))
                {
                    rows.Add((fullPath, shortcut));
                }
            }
        }

        private static string GetShortcut(ToolStripMenuItem item)
        {
            if (!string.IsNullOrEmpty(item.ShortcutKeyDisplayString))
                return item.ShortcutKeyDisplayString;

            if (item.ShortcutKeys != Keys.None)
            {
                return GetShortcut(item.ShortcutKeys);
            }

            return string.Empty;
        }

        public static string CleanMenuText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Remove accelerator ampersands and trailing ellipses
            return text.Replace("&", string.Empty).TrimEnd('…', '.');
        }

        private static void AppendMenuItemsForMnemonics(List<(string MenuPath, string Shortcut)> rows, string parentPath, ToolStripItemCollection items)
        {
            if (items == null)
                return;

            foreach (var item in items)
            {
                if (item is ToolStripSeparator)
                    continue;

                var menuItem = item as ToolStripMenuItem;
                if (menuItem == null)
                    continue;

                var text = CleanMenuText(menuItem.Text);
                var fullPath = string.IsNullOrEmpty(parentPath) ? text : parentPath + " → " + text;

                // Recurse into children first to build full paths
                if (menuItem.HasDropDownItems)
                {
                    AppendMenuItemsForMnemonics(rows, fullPath, menuItem.DropDownItems);
                }

                // Include all menu items (not just those with shortcuts)
                var mnemonic = GetMnemonic(menuItem);
                if (!string.IsNullOrEmpty(mnemonic))
                {
                    rows.Add((fullPath, mnemonic));
                }
            }
        }

        public static string GetMnemonic(ToolStripMenuItem item)
        {
            var text = item.Text;
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Find the ampersand that indicates the mnemonic
            var ampersandIndex = text.IndexOf('&');
            if (ampersandIndex == -1 || ampersandIndex == text.Length - 1)
                return string.Empty;

            var mnemonicChar = text[ampersandIndex + 1];
            if (char.IsLetterOrDigit(mnemonicChar))
            {
                // Build the mnemonic sequence by walking up the menu hierarchy
                var mnemonicSequence = new List<char>();
                var current = item;
                
                while (current != null)
                {
                    var currentText = current.Text;
                    var currentAmpersandIndex = currentText.IndexOf('&');
                    if (currentAmpersandIndex != -1 && currentAmpersandIndex < currentText.Length - 1)
                    {
                        mnemonicSequence.Insert(0, char.ToUpper(currentText[currentAmpersandIndex + 1]));
                    }
                    current = current.OwnerItem as ToolStripMenuItem;
                }

                return "Alt+" + string.Join(",", mnemonicSequence);
            }

            return string.Empty;
        }

        private static string HtmlEncode(string str)
        {
            return HttpUtility.HtmlEncode(str);
        }
    }
}


