/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using NHibernate;
using NHibernate.Criterion;
using pwiz.Common.Collections;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model.Data;

namespace pwiz.Topograph.Model
{
    public class WorkspaceSettings : AbstractSettings<string, string>
    {
        public WorkspaceSettings(Workspace workspace) : base(workspace)
        {
        }
        public T GetSetting<T>(SettingEnum settingEnum, T defaultValue)
        {
            string stringValue;
            if (!Data.TryGetValue(settingEnum.ToString(), out stringValue))
            {
                return defaultValue;
            }
            if (null == stringValue)
            {
                return default(T);
            }
            if (typeof(T).IsEnum)
            {
                return (T)Enum.Parse(typeof(T), stringValue);
            }
            return (T)Convert.ChangeType(stringValue, typeof(T));
        }

        public void SetSetting<T>(SettingEnum settingEnum, T value)
        {
            var strName = settingEnum.ToString();
            var strValue = Equals(value, default(T)) ? null : value.ToString();
            var newValues = Data.Where(pair=>!Equals(pair.Key, strName)).ToList();
            newValues.Add(new KeyValuePair<string, string>(strName, strValue));
            Data = ImmutableSortedList.FromValues(newValues);
        }

        protected override void Diff(WorkspaceChangeArgs workspaceChange, ImmutableSortedList<string, string> newValues, ImmutableSortedList<string, string> oldValues)
        {
            var differences = new HashSet<KeyValuePair<string, string>>(newValues);
            differences.SymmetricExceptWith(oldValues);
            var diffKeys = new HashSet<string>(differences.Select(pair => pair.Key));
            if (diffKeys.Contains(SettingEnum.mass_accuracy.ToString()))
            {
                workspaceChange.AddPeakPickingChange();
            }
            if (diffKeys.Contains(SettingEnum.max_isotope_retention_time_shift.ToString()))
            {
                workspaceChange.AddTurnoverChange();
            }
            if (diffKeys.Contains(SettingEnum.err_on_side_of_lower_abundance.ToString()))
            {
                workspaceChange.AddTurnoverChange();
            }
            if (diffKeys.Any())
            {
                workspaceChange.AddSettingChange();
            }
        }

        public override bool Save(ISession session, DbWorkspace dbWorkspace)
        {
            var existingSettings = session.CreateCriteria<DbSetting>()
                                  .Add(Restrictions.Eq("Workspace", dbWorkspace))
                                  .List<DbSetting>()
                                  .ToDictionary(dbSetting => dbSetting.Name);
            return SaveChangedEntities(session, existingSettings, 
                dbSetting=>dbSetting.Value,
                (dbSetting, value)=>dbSetting.Value = value,
                key=>new DbSetting
                         {
                             Name = key,
                             Workspace = dbWorkspace,
                         });
        }

        public const string QueryPrefix = "query:";
        protected override ImmutableSortedList<string, string> GetData(WorkspaceData workspaceData)
        {
            return workspaceData.Settings;
        }

        protected override WorkspaceData SetData(WorkspaceData workspaceData, ImmutableSortedList<string, string> value)
        {
            return workspaceData.SetSettings(value);
        }
    }
}