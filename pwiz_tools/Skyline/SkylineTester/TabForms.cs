/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using TestRunnerLib;

namespace SkylineTester
{
    public class TabForms : TabBase
    {
        private Timer _updateTimer;

        public override void Enter()
        {
            MainWindow.DefaultButton = MainWindow.RunForms;
            UpdateForms();
        }

        public override bool Run()
        {
            StartLog("Forms");

            var args = new StringBuilder("loop=1 offscreen=off language=");
            args.Append(MainWindow.GetCulture(MainWindow.FormsLanguage));
                
            // Create list of forms the user wants to see.
            var formList = GetFormList();
            args.Append(" form=");
            args.Append(string.Join(",", formList));
            if (MainWindow.ShowFormNames.Checked)
                args.Append(" showformnames=on");

            _updateTimer = new Timer { Interval = 1000 };
            _updateTimer.Tick += (s, a) => UpdateForms();
            _updateTimer.Start();

            MainWindow.AddTestRunner(args.ToString());
            MainWindow.RunCommands();

            return true;
        }

        public override bool Stop(bool success)
        {
            _updateTimer.Stop();
            _updateTimer = null;
            return true;
        }

        public void UpdateForms()
        {
            var formSeen = new FormSeen();

            RunUI(() =>
            {
                int formsCount = MainWindow.FormsGrid.RowCount;
                if (formsCount < 1)
                    return;

                int allSeen = 0;
                for (int i = 0; i < formsCount; i++)
                {
                    int seenCount = formSeen.GetSeenCount(MainWindow.FormsGrid.Rows[i].Cells[0].Value.ToString());
                    if (seenCount > 0)
                        allSeen++;
                    MainWindow.FormsGrid.Rows[i].Cells[2].Value = seenCount;
                }

                MainWindow.FormsSeenPercent.Text = string.Format("{0}% of {1} forms seen", 100*allSeen/formsCount, formsCount);
            });
        }

        public override int Find(string text, int position)
        {
            text = text.Trim();
            for (int i = position; i < MainWindow.FormsGrid.RowCount; i++)
            {
                var value = MainWindow.FormsGrid.Rows[i].Cells[0].Value;
                if (value != null &&
                    value.ToString().IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    MainWindow.FormsGrid.ClearSelection();
                    MainWindow.FormsGrid.Rows[i].Selected = true;
                    ScrollGrid();
                    return i + 1;
                }
            }
            return -1;
        }

        private void ScrollGrid()
        {
            int halfWay = (MainWindow.FormsGrid.DisplayedRowCount(false) / 2);
            if (MainWindow.FormsGrid.FirstDisplayedScrollingRowIndex + halfWay > MainWindow.FormsGrid.SelectedRows[0].Index ||
                (MainWindow.FormsGrid.FirstDisplayedScrollingRowIndex + MainWindow.FormsGrid.DisplayedRowCount(false) - halfWay) <= MainWindow.FormsGrid.SelectedRows[0].Index)
            {
                int targetRow = MainWindow.FormsGrid.SelectedRows[0].Index;

                targetRow = Math.Max(targetRow - halfWay, 0);
                MainWindow.FormsGrid.FirstDisplayedScrollingRowIndex = targetRow;
            }
        }

        public static IEnumerable<string> GetFormList()
        {
            var formList = new List<string>();
            foreach (DataGridViewRow row in MainWindow.FormsGrid.SelectedRows)
            {
                formList.Add(row.Cells[0].Value.ToString());
            }
            return formList;
        }

        private static bool IsForm(Type type)
        {
            if (type.IsAbstract)
                return false;
            if (type.IsSubclassOf(typeof (Form)))
                return !SkylineTesterWindow.Implements(type, "IMultipleViewProvider");
            return SkylineTesterWindow.Implements(type, "IFormView");
        }

        public void CreateFormsGrid()
        {
            // Avoid doing this twice
            if (MainWindow.FormsGrid.RowCount > 0)
                return;

            // Remove excessive underlines from Form and Test links.
            ((DataGridViewLinkColumn) MainWindow.FormsGrid.Columns[0]).LinkBehavior = LinkBehavior.NeverUnderline;
            ((DataGridViewLinkColumn) MainWindow.FormsGrid.Columns[1]).LinkBehavior = LinkBehavior.NeverUnderline;

            var skylinePath = Path.Combine(MainWindow.ExeDir, "Skyline.exe");
            var skylineDailyPath = Path.Combine(MainWindow.ExeDir, "Skyline-daily.exe"); // Keep -daily
            skylinePath = File.Exists(skylinePath) ? skylinePath : skylineDailyPath;
            var assembly = LoadFromAssembly.Try(skylinePath);
            var types = assembly.GetTypes().ToList();
            var commonPath = Path.Combine(MainWindow.ExeDir, "pwiz.Common.dll");
            var dll = LoadFromAssembly.Try(commonPath);
            types.AddRange(dll.GetTypes());
            var formLookup = new FormLookup();

            foreach (var type in types)
            {
                if (IsForm(type))
                {
                    var typeName = SkylineTesterWindow.Implements(type, "IFormView") && type.DeclaringType != null
                        ? type.DeclaringType.Name + "." + type.Name
                        : type.Name;
                    var test = formLookup.GetTest(typeName);
                    if (test == "*")
                        continue;
                    // Skip subclassed forms unless there is an explicit test for them
                    if (HasSubclasses(types, type) && string.IsNullOrEmpty(test))
                        continue;
                    MainWindow.FormsGrid.Rows.Add(typeName, test, 0);
                }
            }

            MainWindow.FormsGrid.Sort(MainWindow.FormsGrid.Columns[0], ListSortDirection.Ascending);
            MainWindow.FormsGrid.ClearSelection();
            MainWindow.FormsGrid.Rows[0].Selected = true;
            UpdateForms();
        }

        private static bool HasSubclasses(IEnumerable<Type> types, Type baseType)
        {
            return types.Count(type => type.IsSubclassOf(baseType)) > 0;
        }
    }
}
