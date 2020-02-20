/*
 * Original author: Yuval Boss <yuval .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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

using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Themes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class EditCustomThemeTest : AbstractFunctionalTest
    {
        private static readonly int _rgbColIndex = 0;
        private static readonly int _hexColIndex = 1;

        [TestMethod]
        public void TestEditCustomTheme()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestEditCustomThemes();
        }

        protected void TestEditCustomThemes()
        {
            var DefaultColorCount = 4;
            var toolOptionsUI = ShowDialog<ToolOptionsUI>(SkylineWindow.ShowToolOptionsUI);
            var combo = toolOptionsUI.getColorCB();
            // Assert dri ver is working properly 
            Assert.AreEqual(combo.Items.Count, DefaultColorCount + 3); // +3 for Add, Edit Current, Edit List items
            var driver = toolOptionsUI.getColorDrive();
            Assert.AreEqual(driver.List.Count, DefaultColorCount);
            Assert.AreEqual(Settings.Default.ColorSchemes.Count, DefaultColorCount);

            Assert.AreEqual(Settings.Default.CurrentColorScheme, ColorSchemeList.DEFAULT.Name); // Default color scheme is correct
            Assert.IsNull(ColorScheme.ColorSchemeDemo); // Not in demo mode

            RunUI(() => combo.SelectedIndex = 2);
            Assert.AreEqual(Settings.Default.CurrentColorScheme, ColorScheme.CurrentColorScheme.Name);
            RunUI(() => combo.SelectedIndex = 0);

            {
                var editCustomThemeDlg =
                    ShowDialog<EditCustomThemeDlg>(() => combo.SelectedIndex = DefaultColorCount + 1);
                var grid = editCustomThemeDlg.getGrid();
                Assert.AreEqual(grid.Rows.Count - 1,
                    ColorSchemeList.DEFAULT.TransitionColors.Count); // -1 because new row in grid
                RunUI(() => editCustomThemeDlg.changeCateogry(EditCustomThemeDlg.ThemeCategory.precursors));
                Assert.AreEqual(grid.Rows.Count - 1,
                    ColorSchemeList.DEFAULT.PrecursorColors.Count); // -1 because new row in grid
                Assert.AreEqual("255, 0, 0", grid.Rows[0].Cells[_rgbColIndex].Value); // check rgb
                Assert.AreEqual("#FF0000", grid.Rows[0].Cells[_hexColIndex].Value); // check hex
                Assert.IsFalse(grid.Columns[_hexColIndex].Visible); // hex hidden
                RunUI(() => editCustomThemeDlg.changeToHex());
                Assert.IsFalse(grid.Columns[_rgbColIndex].Visible); // rgb hidden
                // Test changing colors on a few rows using button
                // Can't actually click button, but will call same function as button does
                ChangeColorByButtonAndValidate(0, editCustomThemeDlg, grid);
                ChangeColorByButtonAndValidate(1, editCustomThemeDlg, grid);
                ChangeColorByButtonAndValidate(2, editCustomThemeDlg, grid);
                ChangeColorByButtonAndValidate(3, editCustomThemeDlg, grid);
                // Test changing colors by changing rgb/hex values
                changeColorByManualAndValidate(0, editCustomThemeDlg, grid);
                changeColorByManualAndValidate(1, editCustomThemeDlg, grid);
                RunUI(() => editCustomThemeDlg.changeName("New Name!"));
                OkDialog(editCustomThemeDlg, editCustomThemeDlg.cancel); // Now cancel changes
                Assert.IsNull(ColorScheme.ColorSchemeDemo);
                Assert.AreEqual(Settings.Default.CurrentColorScheme, ColorSchemeList.DEFAULT.Name); // assert cancel did not save
                // open form again, save first 4 group colors to blue, change name, and test to make sure saves correctly
            }

            RunUI(() => combo.SelectedIndex = 0);

            {
                var editCustomThemeDlg =
                    ShowDialog<EditCustomThemeDlg>(() => combo.SelectedIndex = DefaultColorCount + 1);
                var grid = editCustomThemeDlg.getGrid();
                ChangeColorByButtonAndValidate(0, editCustomThemeDlg, grid);
                ChangeColorByButtonAndValidate(4, editCustomThemeDlg, grid);
                RunUI(() => editCustomThemeDlg.changeName("Yuval is GREAT"));
                OkDialog(editCustomThemeDlg, editCustomThemeDlg.save); // Now save changes
                Assert.AreEqual(Settings.Default.CurrentColorScheme, "Yuval is GREAT");
                Assert.IsNull(ColorScheme.ColorSchemeDemo);
                AssertColor(ColorScheme.CurrentColorScheme.PrecursorColors[0], Color.Blue);
                AssertColor(ColorScheme.CurrentColorScheme.PrecursorColors[4], Color.Blue);
            }

            // Test add new
            RunUI(() => combo.SelectedIndex = 0);

            {
                var editCustomThemeDlg = ShowDialog<EditCustomThemeDlg>(() => combo.SelectedIndex = DefaultColorCount);
                var grid = editCustomThemeDlg.getGrid();
                Assert.AreEqual(grid.Rows[0].Cells[_rgbColIndex].Value, "128, 128, 128");
                Assert.AreEqual(grid.Rows.Count, 2); // 1 row + 1 for new empty row
                var errorDlg = ShowDialog<MessageDlg>(editCustomThemeDlg.save); // Empty name
                OkDialog(errorDlg, errorDlg.OkDialog);
                RunUI(() => editCustomThemeDlg.changeName("Yuval is GREAT"));
                errorDlg = ShowDialog<MessageDlg>(editCustomThemeDlg.save); // Duplicate name error
                OkDialog(errorDlg, errorDlg.OkDialog);
                RunUI(() => grid.Rows.RemoveAt(0));
                // Check even that no rows in grid default color of grey is still set in scheme
                Assert.AreEqual(grid.Rows.Count, 1);
                Assert.IsNotNull(ColorScheme.ColorSchemeDemo);
                Assert.AreEqual(ColorScheme.ColorSchemeDemo.PrecursorColors.Count, 1);
                Assert.AreEqual(ColorScheme.ColorSchemeDemo.TransitionColors.Count, 1);
                RunUI(() => editCustomThemeDlg.changeName("Yuval is more greater"));
                OkDialog(editCustomThemeDlg, editCustomThemeDlg.save); // Now save changes
            }

            // Test Pasting
            RunUI(() => combo.SelectedIndex = 0);

            {
                var editCustomThemeDlg = ShowDialog<EditCustomThemeDlg>(() => combo.SelectedIndex = DefaultColorCount + 2); // + 2 now because we added a new theme
                var pasteText = "#808080\r\n#808080\r\n#808080\r\n#808080";
                RunUI(() =>
                {
                    editCustomThemeDlg.setBindingPosition(1);
                    SetClipboardText(pasteText);
                    editCustomThemeDlg.DoPaste();
                });
                Assert.AreEqual(ColorScheme.ColorSchemeDemo.TransitionColors.Count, 19);
                var colors = ColorScheme.ColorSchemeDemo.TransitionColors.ToArray();
                AssertColor(colors[0], Color.Blue);
                AssertColor(colors[1], Color.Gray);
                AssertColor(colors[2], Color.Gray);
                AssertColor(colors[4], Color.Gray);
                AssertColor(colors[5], Color.BlueViolet);
                AssertColor(colors[18], Color.RoyalBlue);
                OkDialog(editCustomThemeDlg, editCustomThemeDlg.save); // Now save changes
                Assert.IsNull(ColorScheme.ColorSchemeDemo);
                AssertColor(ColorScheme.CurrentColorScheme.TransitionColors[0], Color.Blue);
                AssertColor(ColorScheme.CurrentColorScheme.TransitionColors[1], Color.Gray);
                OkDialog(toolOptionsUI, toolOptionsUI.OkDialog);
            }
        }

        private void changeColorByManualAndValidate(int rowIndex, EditCustomThemeDlg editCustomThemeDlg,
            DataGridView grid)
        {
            RunUI(editCustomThemeDlg.changeToRGB);
            RunUI(() => editCustomThemeDlg.changeCateogry(EditCustomThemeDlg.ThemeCategory.precursors));
            RunUI(() => grid.Rows[rowIndex].Cells[_rgbColIndex].Value = ("255, 0,0"));
            Assert.AreEqual(grid.Rows[rowIndex].Cells[_hexColIndex].Value, "#FF0000");
            AssertColor(ColorScheme.ColorSchemeDemo.PrecursorColors.First(), Color.Red);
            RunUI(() => editCustomThemeDlg.changeCateogry(EditCustomThemeDlg.ThemeCategory.transitions));
            RunUI(() => grid.Rows[rowIndex].Cells[_hexColIndex].Value = ("#0000FF"));
            Assert.AreEqual(grid.Rows[rowIndex].Cells[_rgbColIndex].Value, "0, 0, 255");
            AssertColor(ColorScheme.ColorSchemeDemo.TransitionColors.First(), Color.Blue); // check hex 
            RunUI(() => editCustomThemeDlg.changeCateogry(EditCustomThemeDlg.ThemeCategory.precursors));
            AssertColor(ColorScheme.ColorSchemeDemo.PrecursorColors.First(), Color.Red);
            RunUI(() => editCustomThemeDlg.changeCateogry(EditCustomThemeDlg.ThemeCategory.transitions));
            AssertColor(ColorScheme.ColorSchemeDemo.TransitionColors.First(), Color.Blue); // check hex 
        }

        private void AssertColor(Color color, Color expected)
        {
            Assert.AreEqual(color.R, expected.R);
            Assert.AreEqual(color.G, expected.G);
            Assert.AreEqual(color.B, expected.B);
        }

        private void ChangeColorByButtonAndValidate(int rowIndex, EditCustomThemeDlg editCustomThemeDlg, DataGridView grid)
        {
            RunUI(() => editCustomThemeDlg.changeCateogry(EditCustomThemeDlg.ThemeCategory.precursors));
            RunUI(() => editCustomThemeDlg.changeRowColor(rowIndex, Color.Blue));
            Assert.AreEqual(ColorScheme.ColorSchemeDemo.Name, ColorSchemeList.DEFAULT.Name); // Not in demo mode
            Assert.AreEqual(grid.Rows[rowIndex].Cells[_rgbColIndex].Value, "0, 0, 255"); // check rgb
            Assert.AreEqual(grid.Rows[rowIndex].Cells[_hexColIndex].Value, "#0000FF"); // check hex  
            RunUI(() => editCustomThemeDlg.changeCateogry(EditCustomThemeDlg.ThemeCategory.transitions));
            RunUI(() => editCustomThemeDlg.changeCateogry(EditCustomThemeDlg.ThemeCategory.precursors));
            Assert.AreEqual(grid.Rows[rowIndex].Cells[_rgbColIndex].Value, "0, 0, 255"); // check rgb
            Assert.AreEqual(grid.Rows[rowIndex].Cells[_hexColIndex].Value, "#0000FF"); // check hex  
            Assert.AreEqual(ColorScheme.ColorSchemeDemo.PrecursorColors.First(), Color.Blue); // check hex 
        }
    }
}
