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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Collections;

namespace pwiz.Skyline.Model.Lists
{
    public class ListRowSource<TItem> : SkylineObjectList<ListItemId, TItem> where TItem : ListItem
    {
        public ListRowSource(SkylineDataSchema dataSchema) : base(dataSchema)
        {
            ListName = ListItemTypes.INSTANCE.GetListName<TItem>();
        }

        public string ListName { get; private set; }

        protected override TItem ConstructItem(ListItemId key)
        {
            var listData = DataSchema.Document.Settings.DataSettings.FindList(ListName);
            return (TItem) ListItem.ExistingRecord(listData, key);
        }

        public override IEnumerable GetItems()
        {
            var listData =
                DataSchema.Document.Settings.DataSettings.Lists.FirstOrDefault(list => list.ListDef.Name == ListName);
            if (listData == null)
            {
                return new TItem[0];
            }
            return Enumerable.Range(0, listData.RowCount)
                .Select(rowIndex => ListItem.ExistingRecord(typeof(TItem), listData, rowIndex));
        }

        protected override IEnumerable<ListItemId> ListKeys()
        {
            var listData =
                DataSchema.Document.Settings.DataSettings.Lists.FirstOrDefault(list => list.ListDef.Name == ListName);
            if (listData == null)
            {
                return new ListItemId[0];
            }
            return listData.ListItemIds;
        }
    }
}
