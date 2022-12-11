/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests bringing up the ImportTransitionListColumnSelectDlg and EditPepModsDlg and
    /// exercising the LiteDropDownList controls on the forms.
    /// </summary>
    [TestClass]
    public class LiteDropdownListTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestLiteDropdownList()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            {
                Settings.Default.StaticModList.AddRange(UniMod.DictStructuralModNames.Values);
                Settings.Default.StaticModList.AddRange(UniMod.DictHiddenStructuralModNames.Values);
                Settings.Default.HeavyModList.AddRange(UniMod.DictIsotopeModNames.Values);
                Settings.Default.HeavyModList.AddRange(UniMod.DictHiddenIsotopeModNames.Values);
            });
            var columnSelectDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() =>
            {
                SkylineWindow.Paste(GetTransitionList('\t', 20));
            });
            RunUI(()=>
            {
                foreach (var comboBox in columnSelectDlg.ComboBoxes)
                {
                    comboBox.DroppedDown = true;
                    comboBox.DroppedDown = false;
                }
            });
            OkDialog(columnSelectDlg, columnSelectDlg.OkDialog);
            RunUI(() =>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
            });
            var editPepModsDlg = ShowDialog<EditPepModsDlg>(SkylineWindow.ModifyPeptide);
            RunUI(() =>
            {
                for (int indexAa = 0; indexAa < 5; indexAa++)
                {
                    var lightCombo = editPepModsDlg.GetComboBox(IsotopeLabelType.light, indexAa);
                    lightCombo.DroppedDown = true;
                    var heavyCombo = editPepModsDlg.GetComboBox(IsotopeLabelType.heavy, indexAa);
                    heavyCombo.DroppedDown = true;
                    Assert.IsFalse(lightCombo.DroppedDown);
                }
            });
            OkDialog(editPepModsDlg, editPepModsDlg.OkDialog);
        }

        /// <summary>
        /// Returns a transition list for the peptide sequence "ELVIS".
        /// </summary>
        /// <param name="separator">column separator (tab, comma or semicolon)</param>
        /// <param name="numberOfExtraColumns">Number of columns to add which contain data to be ignored</param>
        /// <returns></returns>
        protected string GetTransitionList(char separator, int numberOfExtraColumns)
        {
            var strSeparator = separator.ToString();
            var columns = new IList[]
            {
                Enumerable.Repeat("ELVIS", 20).ToArray(), // Peptide Sequence
                Enumerable.Repeat(280.66814, 10).Concat(Enumerable.Repeat(187.447852, 10)).ToArray(), // Precursor m/z
                new[]
                {
                    280.66814, 431.286411, 318.202347, 219.133933, 216.146844, 243.133933, 342.202347, 455.286411,
                    171.604812, 228.146844, 187.447852, 431.286411, 318.202347, 219.133933, 216.146844, 243.133933,
                    342.202347, 455.286411, 171.604812, 228.146844
                } // Product m/z
            };
            int rowCount = columns[0].Count;
            for (int i = 1; i < columns.Length; i++)
            {
                Assert.AreEqual(rowCount, columns[i].Count);
            }

            var rows = new List<string>();
            for (int iRow = 0; iRow < rowCount; iRow++)
            {
                var cells = columns.Select(col => DsvWriter.ToDsvField(separator, col[iRow].ToString())).ToList();
                cells.AddRange(Enumerable.Range(0, numberOfExtraColumns).Select(i=>"ExtraCol" + i + "Row" + iRow));
                rows.Add(string.Join(strSeparator, cells));
            }

            return TextUtil.LineSeparate(rows);
        }
    }
}
