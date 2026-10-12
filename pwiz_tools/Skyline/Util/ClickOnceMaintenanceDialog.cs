/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Answers the dialog that a ClickOnce uninstall command opens, so that removing a ClickOnce
    /// Skyline whose settings were just imported does not leave the user a question to answer.
    ///
    /// ClickOnce has no silent uninstall. Its command,
    /// <c>rundll32.exe dfshim.dll,ShArpMaintain Skyline-daily.application, Culture=neutral, ...</c>,
    /// hands the request to the ClickOnce service (dfsvc.exe) and exits, and the service shows a
    /// maintenance dialog offering to restore the previous version or remove the application.
    /// Its controls have no names, and their text is in the language of Windows, so the dialog is
    /// recognized by what does not change: it belongs to dfsvc, its heading is the deployment's
    /// name as the command gives it, and it has exactly five buttons, in the order Restore,
    /// Remove, OK, Cancel, More Information. Anything else is left for the user to answer.
    /// </summary>
    public class ClickOnceMaintenanceDialog
    {
        private const string UNINSTALL_HANDLER = @"dfshim.dll";
        private const string MAINTAIN_ENTRY_POINT = @"ShArpMaintain";
        private const string DEPLOYMENT_EXTENSION = @".application";
        private const string DEPLOYMENT_SERVICE_PROCESS = @"dfsvc";
        private const string BUTTON_CLASS = @"BUTTON";

        private const int BUTTON_COUNT = 5;
        private const int REMOVE_BUTTON = 1;
        private const int OK_BUTTON = 2;

        /// <summary>
        /// The deployment named by a ClickOnce uninstall command, e.g. Skyline-daily, or null when
        /// the command is not a ClickOnce uninstall.
        /// </summary>
        public static string GetDeploymentName(string commandLine)
        {
            if (string.IsNullOrEmpty(commandLine) ||
                commandLine.IndexOf(UNINSTALL_HANDLER, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return null;
            }
            int entryPoint = commandLine.IndexOf(MAINTAIN_ENTRY_POINT, StringComparison.OrdinalIgnoreCase);
            if (entryPoint < 0)
                return null;
            int nameStart = entryPoint + MAINTAIN_ENTRY_POINT.Length;
            int nameEnd = commandLine.IndexOf(DEPLOYMENT_EXTENSION + @",", nameStart, StringComparison.OrdinalIgnoreCase);
            if (nameEnd < 0)
                return null;
            string name = commandLine.Substring(nameStart, nameEnd - nameStart).Trim();
            return name.Length > 0 ? name : null;
        }

        public ClickOnceMaintenanceDialog(string deploymentName)
        {
            DeploymentName = deploymentName;
        }

        /// <summary>
        /// The deployment whose dialog is answered, which the dialog shows as its heading.
        /// </summary>
        public string DeploymentName { get; set; }

        /// <summary>
        /// How long to wait for the dialog. The ClickOnce service can take several seconds to start.
        /// </summary>
        public TimeSpan WaitTime { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Waits, on a thread of its own, for the dialog to appear, and then chooses Remove and OK.
        /// </summary>
        public void ChooseRemoveWhenShown()
        {
            ActionUtil.RunAsync(() =>
            {
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    while (stopwatch.Elapsed < WaitTime)
                    {
                        var buttons = FindButtons();
                        if (buttons != null)
                        {
                            // Restore is the choice when there is a previous version, so Remove is
                            // clicked even though it is often already chosen.
                            Click(buttons[REMOVE_BUTTON]);
                            Click(buttons[OK_BUTTON]);
                            return;
                        }
                        Thread.Sleep(250);
                    }
                }
                catch (Exception)
                {
                    // The dialog stays up for the user to answer.
                }
            }, @"ClickOnce uninstall");
        }

        /// <summary>
        /// The buttons of this deployment's maintenance dialog, or null when it is not showing.
        /// </summary>
        private IntPtr[] FindButtons()
        {
            var serviceProcessIds = Process.GetProcessesByName(DEPLOYMENT_SERVICE_PROCESS).Select(process =>
            {
                using (process)
                    return (uint) process.Id;
            }).ToHashSet();
            if (serviceProcessIds.Count == 0)
                return null;
            foreach (var window in User32.EnumWindows())
            {
                User32.GetWindowThreadProcessId(window, out uint processId);
                if (!serviceProcessIds.Contains(processId) || !User32.IsWindowVisible(window))
                    continue;
                var children = User32.EnumChildWindows(window).ToArray();
                if (!children.Any(child => User32.GetWindowTextNoBlock(child) == DeploymentName))
                    continue;
                var buttons = children.Where(child =>
                    User32.GetClassName(child).IndexOf(BUTTON_CLASS, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                if (buttons.Length == BUTTON_COUNT)
                    return buttons;
            }
            return null;
        }

        /// <summary>
        /// Posts the click, so that nothing here waits on the ClickOnce service. Clicks posted to
        /// the same dialog are handled in the order they were posted.
        /// </summary>
        private static void Click(IntPtr button)
        {
            User32.PostMessageA(button, User32.WinMessageType.BM_CLICK, 0, 0);
        }
    }
}
