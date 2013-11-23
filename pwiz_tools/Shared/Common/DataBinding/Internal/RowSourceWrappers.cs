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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using pwiz.Common.DataBinding.RowSources;

namespace pwiz.Common.DataBinding.Internal
{
    internal interface IRowSourceWrapper
    {
        IEnumerable WrappedRowSource { get; }
        IEnumerable<RowItem> ListRowItems();
        void StartQuery(IQueryRequest queryRequest);
        event ListChangedEventHandler RowSourceChanged;
        QueryResults MakeLive(QueryResults rowItems);
    }

    internal static class RowSourceWrappers
    {
        public static IRowSourceWrapper Wrap(IEnumerable items)
        {
            if (null == items)
            {
                return new RowSourceWrapper(new object[0]);
            }
            var type = items.GetType();
            var cloneableListType = type.FindInterfaces(IsCloneableListType, null).FirstOrDefault();
            if (null != cloneableListType)
            {
                var wrapperType = typeof(CloneableRowSourceWrapper<,>).MakeGenericType(cloneableListType.GetGenericArguments());

                var contructor = wrapperType.GetConstructor(new[] { cloneableListType });
                Debug.Assert(contructor != null);
                // ReSharper disable PossibleNullReferenceException
                return (IRowSourceWrapper)contructor.Invoke(new object[] { items });
                // ReSharper restore PossibleNullReferenceException
            }
            return new RowSourceWrapper(items);
        }
        public static bool IsCloneableListType(Type type, object args)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICloneableList<,>);
        }
    }

    internal class RowSourceWrapper : AbstractRowSourceWrapper
    {
        public static readonly RowSourceWrapper Empty = new RowSourceWrapper(new object[0]);

        public RowSourceWrapper(IEnumerable list) : base(list)
        {
        }

        public override void StartQuery(IQueryRequest queryRequest)
        {
            new ForegroundQuery(this, queryRequest).Start();
        }

        public override QueryResults MakeLive(QueryResults rowItems)
        {
            return rowItems;
        }

        public override IEnumerable<RowItem> ListRowItems()
        {
            return WrappedRowSource.Cast<object>().Select(item => new RowItem(item));
        }
    }
}
