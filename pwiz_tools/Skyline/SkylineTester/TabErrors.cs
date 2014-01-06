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

using System.IO;

namespace SkylineTester
{
    public class TabErrors : TabBase
    {
        public override void Open()
        {
            MainWindow.InitLogSelector(MainWindow.ComboErrors, MainWindow.ButtonOpenErrors);
        }

        public void OpenLog()
        {
            MainWindow.OpenSelectedLog(MainWindow.ComboErrors);
        }

        public void SelectLog()
        {
            var errorsShell = MainWindow.ErrorsShell;
            errorsShell.ClearLog();

            var file = MainWindow.GetSelectedLog(MainWindow.ComboErrors);
            if (file == null)
                return;

            foreach (var line in File.ReadAllLines(file))
            {
                if (line.StartsWith("!!! ") ||
                    line.StartsWith("...skipped ") ||
                    line.StartsWith("...failed "))
                {
                    errorsShell.Log(line);
                }
            }
            errorsShell.DoLiveUpdate = true;
            errorsShell.UpdateLog();
            errorsShell.DoLiveUpdate = MainWindow.ComboErrors.SelectedIndex == 0;
        }
    }
}
