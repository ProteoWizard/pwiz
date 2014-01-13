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
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SkylineTester
{
    public class TabOutput : TabBase
    {
        public Action AfterLoad { get; set; }

        private readonly List<string> _buildProblems = new List<string>();
        private readonly List<string> _failedTests = new List<string>();
        private readonly List<string> _leakingTests = new List<string>();
        private bool _addingErrors;
        private bool _loadDone;
 
        public override void Enter()
        {
            MainWindow.OutputOpenLog.Height = MainWindow.ComboOutput.Height + 2;
            _loadDone = false;

            MainWindow.InitLogSelector(MainWindow.ComboOutput, MainWindow.ButtonOpenOutput);
            if (MainWindow.NightlyRunDate.Items.Count == 0)
            {
                MainWindow.ButtonOpenOutput.Enabled = false;
            }
            else
            {
                int selectedIndex = 0;
                if (File.Exists(MainWindow.DefaultLogFile) && MainWindow.LastRunName != null)
                    MainWindow.ComboOutput.Items.Insert(0, MainWindow.LastRunName + " output");
                if (MainWindow.LastTabIndex == MainWindow.NightlyTabIndex && MainWindow.NightlyRunDate.SelectedIndex >= 0)
                    selectedIndex = MainWindow.NightlyRunDate.SelectedIndex + MainWindow.ComboOutput.Items.Count - MainWindow.NightlyRunDate.Items.Count;
                MainWindow.ComboOutput.SelectedIndex = selectedIndex;
                MainWindow.ButtonOpenOutput.Enabled = true;
            }

            MainWindow.DefaultButton = null;
        }

        public override int Find(string text, int position)
        {
            MainWindow.CommandShell.Focus();
            var shellText = MainWindow.CommandShell.Text;
            position = shellText.IndexOf(text, position, shellText.Length - position, StringComparison.CurrentCultureIgnoreCase);
            if (position < 0)
                return -1;
            MainWindow.CommandShell.Select(position, text.Length);
            return position + text.Length;
        }

        public void ClearErrors()
        {
            _buildProblems.Clear();
            _failedTests.Clear();
            _leakingTests.Clear();
        }

        public void LoadDone()
        {
            RunUI(() =>
            {
                ShowErrors();
                if (AfterLoad != null)
                {
                    AfterLoad();
                    AfterLoad = null;
                }
            });
        }

        private void ShowErrors()
        {
            _loadDone = true;
            _addingErrors = true;
            MainWindow.ErrorConsole.Clear();
            if (_buildProblems.Count > 0)
            {
                AddHeading("Build problems");
                foreach (var problem in _buildProblems)
                    MainWindow.ErrorConsole.AppendText("  {0}".With(problem));
                MainWindow.ErrorConsole.AppendText("\n");
            }
            if (_failedTests.Count > 0)
            {
                AddHeading("Failed tests");
                foreach (var test in _failedTests)
                    MainWindow.ErrorConsole.AppendText("  {0}\n".With(test.Split(' ')[1]));
                MainWindow.ErrorConsole.AppendText("\n");
            }
            if (_leakingTests.Count > 0)
            {
                AddHeading("Leaking tests");
                foreach (var test in _leakingTests)
                {
                    var parts = test.Split(' ');
                    var testName = parts[1];
                    var leakedBytes = parts[3];
                    MainWindow.ErrorConsole.AppendText("  {0,-46} {1,8} bytes\n".With(testName, leakedBytes));
                }
                MainWindow.ErrorConsole.AppendText("\n");
            }
            _addingErrors = false;

            MainWindow.OutputSplitContainer.Panel2Collapsed = MainWindow.ErrorConsole.TextLength == 0;
        }

        private void AddHeading(string heading)
        {
            MainWindow.ErrorConsole.AppendText(heading + "\n");
            SelectLine(MainWindow.ErrorConsole, MainWindow.ErrorConsole.TextLength - 1);
            MainWindow.ErrorConsole.SelectionFont = new Font("Georgia", 14, FontStyle.Italic);
            MainWindow.ErrorConsole.SelectionColor = Color.Black;
        }

        public void ProcessError(string line)
        {
            if (line.StartsWith("..."))
                _buildProblems.Add(line);
            else if (line.Contains(" LEAKED "))
                _leakingTests.Add(line);
            else
                _failedTests.Add(line);
            if (_loadDone)
                ShowErrors();
        }

        private void SelectLine(RichTextBox textBox, int position)
        {
            int start = position - 1;
            while (start >= 0 && textBox.Text[start] != '\n')
                start--;
            start++;
            int end = position;
            while (end < textBox.TextLength && textBox.Text[end] != '\n')
                end++;
            textBox.Select(start, end - start);
        }

        public void ErrorSelectionChanged()
        {
            if (_addingErrors)
                return;

            _addingErrors = true;
            try
            {
                SelectLine(MainWindow.ErrorConsole, MainWindow.ErrorConsole.SelectionStart);
                var searchText = MainWindow.ErrorConsole.SelectedText;
                if (searchText.StartsWith("  "))
                {
                    searchText = searchText.Trim();
                    if (!searchText.StartsWith("..."))
                        searchText = "!!! {0} ".With(searchText.Split(' ')[0]);
                    var outputPosition = MainWindow.CommandShell.Text.IndexOf(searchText, StringComparison.CurrentCulture);
                    if (outputPosition >= 0)
                    {
                        MainWindow.CommandShell.IgnorePaint++;
                        MainWindow.CommandShell.Select(outputPosition, 0);
                        MainWindow.CommandShell.ScrollToCaret();
                        MainWindow.CommandShell.IgnorePaint--;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            _addingErrors = false;
        }
    }
}
