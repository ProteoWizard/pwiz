/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Skyline.Controls.Lists;
using pwiz.Skyline.Model.Lists;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Properties
{
    public class ListDefList : SettingsList<ListData>, IListSerializer<ListData>
    {
        public override ListData CopyItem(ListData item)
        {
            return new ListData((ListDef) item.ListDef.ChangeName(String.Empty), item.Columns);
        }

        public override ListData EditItem(Control owner, ListData item, IEnumerable<ListData> existing, object tag)
        {
            using (var dlg = new ListDesigner(item, existing))
            {
                if (dlg.ShowDialog(owner) == DialogResult.OK)
                {
                    return dlg.GetListDef();
                }
            }
            return null;
        }

        public override IEnumerable<ListData> GetDefaults(int revisionIndex)
        {
            return new ListData[0];
        }

        public override string Label
        {
            get { return PropertiesResources.ListDefList_Label_Lists; }
        }

        public override string Title
        {
            get { return PropertiesResources.ListDefList_Title_Define_Lists; }
        }

        public ICollection<ListData> CreateEmptyList()
        {
            return new ListDefList();
        }

        public Type DeserialType { get { return SerialType; } }
        public Type SerialType { get { return typeof(ListDef); } }
    }
}
