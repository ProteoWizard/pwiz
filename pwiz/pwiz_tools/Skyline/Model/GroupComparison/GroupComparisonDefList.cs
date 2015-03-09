/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Collections.Generic;
using System.Windows.Forms;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.GroupComparison
{
    public class GroupComparisonDefList : SettingsList<GroupComparisonDef>
    {
        public override IEnumerable<GroupComparisonDef> GetDefaults(int revisionIndex)
        {
            return new GroupComparisonDef[0];
        }

        public override string Title
        {
            get { return GroupComparisonStrings.GroupComparisonDefList_Title_Define_Group_Comparisons; }
        }

        public override string Label
        {
            get { return GroupComparisonStrings.GroupComparisonDefList_Label_Group_Comparisons; }
        }

        public override GroupComparisonDef EditItem(Control owner, GroupComparisonDef item, IEnumerable<GroupComparisonDef> existing, object tag)
        {
            using (var dlg = new EditGroupComparisonDlg(tag as IDocumentContainer, item ?? GroupComparisonDef.EMPTY, existing))
            {
                if (dlg.ShowDialog(owner) == DialogResult.OK)
                {
                    return dlg.GroupComparisonDef;
                }
            }
            return null;
        }

        public override GroupComparisonDef CopyItem(GroupComparisonDef item)
        {
            return item.ChangeName(string.Empty);
        }

        public override bool AllowReset { get { return false; } }
    }
}
