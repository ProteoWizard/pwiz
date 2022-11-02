/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Databinding
{
    public class DataGridType
    {
        private Func<string> _titleFunc;
        public static DataGridType DOCUMENT_GRID = new DataGridType(@"DocumentGrid", ()=>Resources.DataGridType_DOCUMENT_GRID_Document_Grid);
        public static DataGridType RESULTS_GRID = new DataGridType(@"ResultsGrid", ()=>Resources.DataGridType_RESULTS_GRID_Results_Grid);
        public static DataGridType GROUP_COMPARISON = new DataGridType(@"GroupComparison", ()=>Resources.DataGridType_GROUP_COMPARISON_Group_Comparison);
        public static DataGridType LIST = new DataGridType(Resources.DataGridType_LIST_List, ()=>Resources.DataGridType_LIST_List);
        public static DataGridType AUDIT_LOG = new DataGridType(@"AuditLog", ()=>Resources.DataGridType_AUDIT_LOG_Audit_Log);

        public static IEnumerable<DataGridType> All
        {
            get
            {
                yield return DOCUMENT_GRID;
                yield return RESULTS_GRID;
                yield return GROUP_COMPARISON;
                yield return LIST;
                yield return AUDIT_LOG;
            }
        }
        public static DataGridType FromName(string name)
        {
            return All.FirstOrDefault(type => type.Name == name);
        }

        public DataGridType(string name, Func<string> titleFunc)
        {
            Name = name;
            _titleFunc = titleFunc;
        }

        public string Name { get; }

        public string Title
        {
            get { return _titleFunc(); }
        }
    }

    public class DataGridId
    {
        public DataGridId(DataGridType type, string name)
        {
            DataGridType = type;
            Name = name;
        }

        public DataGridType DataGridType { get; private set; }
        public string Name { get; private set; }

        public override string ToString()
        {
            string title = DataGridType.Title;
            if (string.IsNullOrEmpty(Name))
            {
                return title;
            }

            return TextUtil.SpaceSeparate(title, Name);
        }

        public PersistentString ToPersistedString()
        {
            return PersistentString.FromParts(DataGridType.Name, Name);
        }

        public static DataGridId MakeDataGridId(string typeName, string instanceName)
        {
            var dataGridType = DataGridType.FromName(typeName);
            if (dataGridType == null)
            {
                return null;
            }
            return new DataGridId(dataGridType, instanceName);
        }

        public static DataGridId FromPersistentString(PersistentString persistentString, out PersistentString remainingParts)
        {
            remainingParts = PersistentString.EMPTY;
            if (persistentString.Parts.Count < 2)
            {
                return null;
            }
            var dataGridType = DataGridType.FromName(persistentString.Parts[0]);
            if (dataGridType == null)
            {
                return null;
            }

            remainingParts = persistentString.Skip(2);
            return new DataGridId(dataGridType, persistentString.Parts[1]);
        }

        protected bool Equals(DataGridId other)
        {
            return Equals(DataGridType, other.DataGridType) && Name == other.Name;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((DataGridId) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((DataGridType != null ? DataGridType.GetHashCode() : 0) * 397) ^ (Name != null ? Name.GetHashCode() : 0);
            }
        }
    }
}
