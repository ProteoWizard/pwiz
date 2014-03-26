/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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

namespace pwiz.Common.DataBinding
{
    public class FilterInfo
    {
        public FilterInfo(FilterSpec filterSpec, ColumnDescriptor columnDescriptor, ColumnDescriptor collectionColumn)
        {
            FilterSpec = filterSpec;
            ColumnDescriptor = columnDescriptor;
            CollectionColumn = collectionColumn; 
            try
            {
                Predicate = MakePredicate();
            }
            catch (Exception e)
            {
                Error = e.Message;
            }
        }
        public FilterSpec FilterSpec { get; private set; }
        public ColumnDescriptor ColumnDescriptor { get; private set; }
        public ColumnDescriptor CollectionColumn { get; private set; }
        private Predicate<object> Predicate { get; set; }
        public String Error { get; private set; }
        public RowItem ApplyFilter(RowItem rowItem)
        {
            var predicate = Predicate;
            if (CollectionColumn.PropertyPath.IsRoot || rowItem.RowKey.Contains(CollectionColumn.PropertyPath))
            {
                if (predicate(ColumnDescriptor.GetPropertyValue(rowItem, null)))
                {
                    return rowItem;
                }
                return null;
            }
            if (rowItem.PivotKeys.Count == 0)
            {
                return FilterSpec.Operation == FilterOperations.OP_IS_BLANK ? rowItem : null;
            }
            List<PivotKey> newPivotKeys = new List<PivotKey>();
            bool anyPivotKeysLeft = false;
            foreach (var pivotKey in rowItem.PivotKeys)
            {
                if (!pivotKey.Contains(CollectionColumn.PropertyPath))
                {
                    newPivotKeys.Add(pivotKey);
                }
                else
                {
                    object value = ColumnDescriptor.GetPropertyValue(rowItem, pivotKey);
                    if (predicate(value)) 
                    {
                        newPivotKeys.Add(pivotKey);
                        anyPivotKeysLeft = true;
                    }
                }
            }
            if (newPivotKeys.Count == rowItem.PivotKeys.Count)
            {
                return rowItem;
            }
            if (!anyPivotKeysLeft)
            {
                return null;
            }
            return rowItem.SetPivotKeys(new HashSet<PivotKey>(newPivotKeys));
        }

        private Predicate<object> MakePredicate()
        {
            var op = FilterSpec.Operation ?? FilterOperations.OP_HAS_ANY_VALUE;
            return op.MakePredicate(ColumnDescriptor, FilterSpec.Operand);
        }
    }
}
