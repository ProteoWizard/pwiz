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
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// An instantiation of a <see cref="ViewSpec" />.
    /// Whereas a ViewSpec identifies columns simply by a string, a ViewInfo
    /// has retrieved the relevant PropertyDescriptors from the DataSchema.
    /// </summary>
    public class ViewInfo
    {
        private readonly IDictionary<IdentifierPath, ColumnDescriptor> _columnDescriptors = new Dictionary<IdentifierPath, ColumnDescriptor>();
        
        public ViewInfo(DataSchema dataSchema, Type rootType, ViewSpec viewSpec) : this(new ColumnDescriptor(dataSchema, rootType), viewSpec)
        {
        }

        public ViewInfo(ColumnDescriptor parentColumn, ViewSpec viewSpec)
        {
            ParentColumn = parentColumn;
            DataSchema = parentColumn.DataSchema;
            Name = viewSpec.Name;
            var columnSpecs = viewSpec.Columns.ToDictionary(c => c.IdentifierPath, c => c);
            _columnDescriptors.Add(parentColumn.IdPath, parentColumn);
            ColumnDescriptors = Array.AsReadOnly(viewSpec.Columns
                .Select(c => GetColumnDescriptor(columnSpecs, c.IdentifierPath)).ToArray());
            SublistId = viewSpec.SublistId;
        }

        public DataSchema DataSchema { get; private set; }
        public ColumnDescriptor ParentColumn { get; private set; }
        public string Name { get; private set; }
        public IdentifierPath SublistId { get; private set; }
        public IList<ColumnDescriptor> ColumnDescriptors { get; private set; }
        public IEnumerable<ColumnDescriptor> AllColumnDescriptors { get { return _columnDescriptors.Values.ToArray(); } }
        public ICollection<ColumnDescriptor> GetCollectionColumns()
        {
            var unboundColumnSet = new HashSet<ColumnDescriptor>();
            foreach (var columnDescriptor in ColumnDescriptors)
            {
                for (var unboundParent = columnDescriptor.FirstUnboundParent(); unboundParent != null; unboundParent = unboundParent.Parent.FirstUnboundParent())
                {
                    unboundColumnSet.Add(unboundParent);
                }
            }
            return unboundColumnSet;
        }
        private ColumnDescriptor GetColumnDescriptor(IDictionary<IdentifierPath, ColumnSpec> columnSpecs, IdentifierPath idPath)
        {
            ColumnDescriptor columnDescriptor;
            if (_columnDescriptors.TryGetValue(idPath, out columnDescriptor))
            {
                return columnDescriptor;
            }
            var parent = GetColumnDescriptor(columnSpecs, idPath.Parent);
            if (parent == null)
            {
                throw new InvalidOperationException("Could not resolve path " + idPath);
            }
            if (idPath.Name != null)
            {
                columnDescriptor = new ColumnDescriptor(parent, idPath.Name);
            }
            else
            {
                var collectionInfo = DataSchema.GetCollectionInfo(parent.PropertyType);
                if (collectionInfo == null)
                {
                    throw new InvalidOperationException(parent.PropertyType + " is not a collection.");
                }
                columnDescriptor = new ColumnDescriptor(parent, collectionInfo);
            }
            ColumnSpec columnSpec;
            if (columnSpecs.TryGetValue(idPath, out columnSpec))
            {
                columnDescriptor = columnDescriptor.SetColumnSpec(columnSpec);
            }
            _columnDescriptors.Add(idPath, columnDescriptor);
            return columnDescriptor;
        }
    }
}
