/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Databinding
{
    public static class ViewSettings
    {
        public static event EventHandler SettingsChange;

        public static ViewSpecList ViewSpecList
        {
            get
            {
                return Settings.Default.ViewSpecList;
            }
            set
            {
                bool changed = false;
                var viewNames = new HashSet<string>(value.ViewSpecs.Select(view => view.Name));
                var newReports =
                    Settings.Default.ReportSpecList.Where(reportSpec => viewNames.Contains(reportSpec.Name)).ToArray();
                if (newReports.Length != Settings.Default.ReportSpecList.Count)
                {
                    var newReportSpecList = new ReportSpecList
                    {
                        RevisionIndex = Settings.Default.ReportSpecList.RevisionIndex
                    };
                    newReportSpecList.AddRange(newReports);
                    Settings.Default.ReportSpecList = newReportSpecList;
                    changed = true;
                }
                if (!changed && Equals(ViewSpecList, value))
                {
                    return;
                }
                Settings.Default.ViewSpecList = value;
                if (null != SettingsChange)
                {
                    SettingsChange(null, new EventArgs());
                }
            }
        }
    }
}
