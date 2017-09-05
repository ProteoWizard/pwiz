/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Linq;

namespace pwiz.Common.DataBinding.Internal
{
    internal interface IRowSourceWrapper
    {
        IRowSource WrappedRowSource { get; }
        IEnumerable<RowItem> ListRowItems();
        void StartQuery(IQueryRequest queryRequest);
        event Action RowSourceChanged;
    }

    internal static class RowSourceWrappers
    {
        public static IRowSourceWrapper Wrap(IRowSource items)
        {
            if (null == items)
            {
                return new RowSourceWrapper(StaticRowSource.EMPTY);
            }
            return new RowSourceWrapper(items);
        }
    }

    internal class RowSourceWrapper : AbstractRowSourceWrapper
    {
        public static readonly RowSourceWrapper Empty = new RowSourceWrapper(StaticRowSource.EMPTY);

        public RowSourceWrapper(IRowSource list) : base(list)
        {
        }

        public override void StartQuery(IQueryRequest queryRequest)
        {
            if (null == queryRequest.EventTaskScheduler)
            {
                new ForegroundQuery(this, queryRequest).Start();
            }
            else
            {
                new BackgroundQuery(this, queryRequest.EventTaskScheduler, queryRequest).Start();
            }
        }

        public override QueryResults MakeLive(QueryResults rowItems)
        {
            return rowItems;
        }

        public override IEnumerable<RowItem> ListRowItems()
        {
            return WrappedRowSource.GetItems().Cast<object>().Select(item => new RowItem(item));
        }
    }
}
