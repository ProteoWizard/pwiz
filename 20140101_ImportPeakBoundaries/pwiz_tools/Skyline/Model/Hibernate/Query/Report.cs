/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Hibernate.Query
{
    public enum ReportType
    {
// ReSharper disable InconsistentNaming
        SIMPLE,
        PIVOT
// ReSharper restore InconsistentNaming
    }
    public abstract class Report
    {
        public abstract ReportSpec GetReportSpec(String name);
        public abstract ResultSet Execute(Database database);
        public static Report Load(ReportSpec reportSpec)
        {
            SimpleReport simpleReport;
            if (reportSpec.CrossTabHeaders != null)
            {
                PivotReport pivotReport = new PivotReport
                                              {
                                                  GroupByColumns = reportSpec.GroupBy,
                                                  CrossTabHeaders = reportSpec.CrossTabHeaders,
                                                  CrossTabValues = reportSpec.CrossTabValues
                                              };
                simpleReport = pivotReport;
            }
            else
            {
                simpleReport = new SimpleReport();
            }
            simpleReport.Columns = reportSpec.Select;
            return simpleReport;
        }
    }
    /// <summary>
    /// A report which selects columns from a single table.
    /// </summary>
    public class SimpleReport : Report
    {
        public IList<ReportColumn> Columns { get; set; }
        public SimpleReport()
        {
            Columns = new List<ReportColumn>();
        }

        public override ReportSpec GetReportSpec(String name)
        {
            return new ReportSpec(name, new QueryDef
                                            {
                                                Select = Columns
                                            });
        }
        public override ResultSet Execute(Database database)
        {
            return database.ExecuteQuery(Columns);
        }
    }
}
