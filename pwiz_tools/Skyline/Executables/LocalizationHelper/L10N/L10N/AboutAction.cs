/*
 * Original author: Yuval Boss <yuval .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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

using JetBrains.ActionManagement;
using JetBrains.Application.DataContext;
using JetBrains.ReSharper.Feature.Services.Menu;
using JetBrains.UI.ActionsRevised;
using JetBrains.UI.ActionSystem.Actions;
using JetBrains.UI.MenuGroups;
using JetBrains.Util;

namespace YuvalBoss.L10N
{
    [Action("About L10N", Id = 10802)]
    public class AboutAction : IExecutableAction, IInsertAfter<HelpMenu, ShowHelpAction>

    {
        public bool Update(IDataContext context, ActionPresentation presentation, DelegateUpdate nextUpdate)
        {
            // return true or false to enable/disable this action
            return true;
        }

        public void Execute(IDataContext context, DelegateExecute nextExecute)
        {
            MessageBox.ShowMessageBox(
              "Localization Helper\nYuval\n\nHelps Localize",
              "About Localization Helper",
              MbButton.MB_OK,
              MbIcon.MB_ICONASTERISK);
        }
    }
} 