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
using System.Text;
using NHibernate;

namespace pwiz.Topograph.Data
{
    public class SettingDef<T>
    {
        public SettingDef(String name, T defaultValue)
        {
            Name = name;
            DefaultValue = defaultValue;
        }
        public String Name { get; private set; }
        public T DefaultValue { get; private set; }
        public T GetValue(DbWorkspace workspace)
        {
            foreach (DbSetting setting in workspace.Settings)
            {
                if (setting.Name == Name)
                {
                    return (T) Convert.ChangeType(setting.Value, typeof(T));
                }
            }
            return DefaultValue;
        }
        public void SetValue(ISession session, DbWorkspace workspace, T value)
        {
            DbSetting setting = workspace.GetSetting(Name);
            if (setting == null)
            {
                setting = new DbSetting
                              {
                                  Workspace = workspace, 
                                  Name = Name
                              };
                workspace.Settings.Add(setting);
            }
            setting.Value = value.ToString();
            session.SaveOrUpdate(setting);
        }

        public static readonly SettingDef<double> MASS_ACCURACY 
            = new SettingDef<double>("MassAccuracy", 200000);

        public static readonly SettingDef<double> RESOLUTION
            = new SettingDef<double>("Resolution", 50000);
    }
}
