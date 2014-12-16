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
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;
using EnvDTE;

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
            MainWindow.ButtonOpenLog.Height = MainWindow.ComboOutput.Height + 2;
            _loadDone = false;

            MainWindow.InitLogSelector(MainWindow.ComboOutput, MainWindow.ButtonOpenLog);
            if (File.Exists(MainWindow.DefaultLogFile) && MainWindow.LastRunName != null)
                MainWindow.ComboOutput.Items.Insert(0, MainWindow.LastRunName + " output");
            MainWindow.ComboOutput.SelectedIndex =
                (MainWindow.LastTabIndex == MainWindow.NightlyTabIndex && MainWindow.NightlyRunDate.SelectedIndex >= 0 && MainWindow.LastRunName != "Zip")
                ? MainWindow.NightlyRunDate.SelectedIndex + MainWindow.ComboOutput.Items.Count - MainWindow.NightlyRunDate.Items.Count
                : (MainWindow.ComboOutput.Items.Count > 0 ? 0 : -1);
            MainWindow.ButtonOpenLog.Enabled = MainWindow.ComboOutput.Items.Count > 0;

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
            _jumpList = new List<JumpToPattern>
            {
                new JumpToPattern("# Nightly started"),
                new JumpToPattern("# Checking out Skyline"),
                new JumpToPattern("# Building Skyline"),
                new JumpToPattern("# Pass 0:"),
                new JumpToPattern("# Pass 1:"),
                new JumpToPattern("# Pass 2+:"),
                new JumpToPattern("# Nightly finished"),
            };
            _findIndex = 0;

            MainWindow.OutputJumpTo.Items.Clear();
            MainWindow.OutputJumpTo.Items.Add("Jump to:");
            MainWindow.OutputJumpTo.SelectedIndex = 0;
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
            }
            if (_failedTests.Count > 0)
            {
                if (MainWindow.ErrorConsole.TextLength > 0)
                    MainWindow.ErrorConsole.AppendText("\n");
                AddHeading("Failed tests");
                foreach (var test in _failedTests)
                    MainWindow.ErrorConsole.AppendText("  {0}\n".With(test.Split(' ')[1]));
            }
            if (_leakingTests.Count > 0)
            {
                if (MainWindow.ErrorConsole.TextLength > 0)
                    MainWindow.ErrorConsole.AppendText("\n");
                AddHeading("Leaking tests");
                foreach (var test in _leakingTests)
                {
                    var parts = test.Split(' ');
                    var testName = parts[1];
                    var leakedBytes = parts[3];
                    MainWindow.ErrorConsole.AppendText("  {0,-46} {1,8} bytes\n".With(testName, leakedBytes));
                }
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
            if (_jumpList == null)
                return;
            if (line.StartsWith("..."))
                _buildProblems.Add(line);
            else if (line.Contains(" LEAKED "))
                _leakingTests.Add(line);
            else
                _failedTests.Add(line);
            _jumpList.Add(new JumpToPattern(line));
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
            if (end < 0)
                return;
            while (end < textBox.TextLength && textBox.Text[end] != '\n')
                end++;
            textBox.Select(start, end - start);
        }

        public void CommandShellMouseClick()
        {
            if (MainWindow.CommandShell.SelectionLength > 0)
                return;

            // Scan forward to line end.
            var text = MainWindow.CommandShell.Text;
            int end = text.Length;
            if (end == 0)
                return;
            int lineEnd = MainWindow.CommandShell.SelectionStart;
            while (lineEnd < end && text[lineEnd] != '\n')
                lineEnd++;
            int lineNumberStart = lineEnd - 1;
            if (!Char.IsDigit(text, lineNumberStart))
                return;
            while (lineNumberStart >= 0 && Char.IsDigit(text, lineNumberStart))
                lineNumberStart--;
            if (lineNumberStart < 0)
                return;
            lineNumberStart++;
            const string fileLinePattern = ":line ";
            if (lineNumberStart < fileLinePattern.Length)
                return;
            var testFileLinePattern = text.Substring(lineNumberStart - fileLinePattern.Length, fileLinePattern.Length);
            if (testFileLinePattern != fileLinePattern)
                return;
            var fileStart = text.LastIndexOf(" in ", lineNumberStart, StringComparison.CurrentCulture);
            if (fileStart < 0)
                return;
            fileStart += " in ".Length;
            var file = text.Substring(fileStart, lineNumberStart - fileLinePattern.Length - fileStart);
            var lineNumberText = text.Substring(lineNumberStart, lineEnd - lineNumberStart);

            var dte = GetDTE(file, lineNumberText);
            if (dte == null)
                return;
            try
            {
                // Open in Visual Studio and go to the indicated line number.
                dte.ExecuteCommand("File.OpenFile", file);
                dte.ExecuteCommand("Edit.GoTo", lineNumberText);
                ((TextSelection)dte.ActiveDocument.Selection).SelectLine();

                // Bring Visual Studio to the foreground.
                SetForegroundWindow((IntPtr)dte.MainWindow.HWnd);
            }
            catch (COMException)
            {
                MessageBox.Show(MainWindow, "Failure attempting to communicate with Visual Studio.");
            }

            Marshal.ReleaseComObject(dte);
        }

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(uint reserved, out IBindCtx ppbc);

        // from http://blogs.msdn.com/b/kirillosenkov/archive/2011/08/10/how-to-get-dte-from-visual-studio-process-id.aspx
        public static DTE GetDTE(string file, string lineNumberText)
        {
            var dte = FindDTE(file);
            if (dte != null)
                return dte;

            // Couldn't find an instance of Visual Studio with a solution containing this file.
            // Try finding a Skyline solution we can use and open that one.
            var parentDirectory = Path.GetDirectoryName(file);
            while (parentDirectory != null)
            {
                var skylineSln = Path.Combine(parentDirectory, "Skyline.sln");
                if (File.Exists(skylineSln))
                {
                    System.Diagnostics.Process.Start(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft Visual Studio 12.0\Common7\IDE\devenv.exe"),
                        @"{0} {1} /command ""Edit.Goto {2}""".With(skylineSln, file, lineNumberText));
                    return null;
                }
                parentDirectory = Path.GetDirectoryName(parentDirectory);
            }

            return null;
        }

        private static DTE FindDTE(string file)
        {
            const string id = "!VisualStudio.DTE.";

            IBindCtx bindCtx = null;
            IRunningObjectTable rot = null;
            IEnumMoniker enumMonikers = null;

            try
            {
                Marshal.ThrowExceptionForHR(CreateBindCtx(0, out bindCtx));
                bindCtx.GetRunningObjectTable(out rot);
                rot.EnumRunning(out enumMonikers);

                IMoniker[] moniker = new IMoniker[1];
                IntPtr numberFetched = IntPtr.Zero;
                while (enumMonikers.Next(1, moniker, numberFetched) == 0)
                {
                    IMoniker runningObjectMoniker = moniker[0];

                    string name = null;

                    try
                    {
                        if (runningObjectMoniker != null)
                            runningObjectMoniker.GetDisplayName(bindCtx, null, out name);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Do nothing, there is something in the ROT that we do not have access to.
                    }

                    if (!string.IsNullOrEmpty(name) && name.StartsWith(id, StringComparison.Ordinal))
                    {
                        object runningObject;
                        Marshal.ThrowExceptionForHR(rot.GetObject(runningObjectMoniker, out runningObject));
                        var dte = runningObject as DTE;
                        if (dte != null && dte.Solution.FindProjectItem(file) != null)
                        {
                            Console.WriteLine(dte.Solution.FindProjectItem(file).Name);
                            return dte;
                        }
                    }
                }
            }
            finally
            {
                if (enumMonikers != null)
                    Marshal.ReleaseComObject(enumMonikers);
                if (rot != null)
                    Marshal.ReleaseComObject(rot);
                if (bindCtx != null)
                    Marshal.ReleaseComObject(bindCtx);
            }

            return null;
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

        private class JumpToPattern
        {
            public readonly string Pattern;
            public int Index;

            public JumpToPattern(string pattern)
            {
                Pattern = "\n" + pattern;
            }
        }

        private List<JumpToPattern> _jumpList;
        private int _findIndex;

        public void PrepareJumpTo()
        {
            var text = MainWindow.CommandShell.Text;
            int findCount = 0;
            foreach (var jumpItem in _jumpList)
            {
                if (jumpItem.Index == 0)
                    findCount++;
            }

            while (findCount > 0)
            {
                if (_findIndex == text.Length)
                    break;
                int index = text.IndexOf('\n', _findIndex);
                if (index < 0)
                    break;
                foreach (var jumpItem in _jumpList)
                {
                    if (jumpItem.Index == 0 &&
                        text.Length - index >= jumpItem.Pattern.Length &&
                        text.IndexOf(jumpItem.Pattern, index, jumpItem.Pattern.Length, StringComparison.CurrentCulture) == index)
                    {
                        jumpItem.Index = index;
                        findCount--;
                        break;
                    }
                }
                _findIndex = index + 1;
            }

            _jumpList = _jumpList.OrderBy(item => item.Index).ToList();


            MainWindow.OutputJumpTo.Items.Clear();
            MainWindow.OutputJumpTo.Items.Add("Jump to:");
            foreach (var jumpItem in _jumpList)
            {
                if (jumpItem.Index > 0)
                    MainWindow.OutputJumpTo.Items.Add("    " + jumpItem.Pattern);
            }
            MainWindow.OutputJumpTo.Items.Add("    End");
            MainWindow.OutputJumpTo.SelectedIndex = 0;
        }

        public void JumpTo(int jumpToIndex)
        {
            if (jumpToIndex == 0)
                return;
            MainWindow.OutputJumpTo.SelectedIndex = 0;
            var pattern = "\n" + ((string) MainWindow.OutputJumpTo.Items[jumpToIndex]).TrimStart();
            var index = _jumpList.FindIndex(
                jumpToPattern => jumpToPattern.Pattern == pattern);
            MainWindow.CommandShell.Select(index >= 0 ? _jumpList[index].Index + 1 : MainWindow.CommandShell.TextLength, 0);
            MainWindow.CommandShell.ScrollToCaret();
            MainWindow.CommandShell.Focus();
        }
    }
}
