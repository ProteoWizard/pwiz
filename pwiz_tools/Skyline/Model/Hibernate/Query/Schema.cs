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
using System.Reflection;
using NHibernate;
using NHibernate.Metadata;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Hibernate.Query
{
    public class Schema
    {
        private readonly ISessionFactory _sessionFactory;
        private readonly HashSet<string> _annotationDefNames;
        public Schema(ISessionFactory sessionFactory, DataSettings dataSettings)
        {
            _sessionFactory = sessionFactory;
            _annotationDefNames = new HashSet<string>();
            if (dataSettings != null)
            {
                foreach (var annotationDef in dataSettings.AnnotationDefs)
                {
                    _annotationDefNames.Add(AnnotationDef.GetKey(annotationDef.Name));
                }
            }
        }

        public IClassMetadata GetClassMetadata(Type type)
        {
            return _sessionFactory.GetClassMetadata(type);
        }

        public ColumnInfo GetColumnInfo(Type table, String column)
        {
            var columnInfo = new ColumnInfo
            {
                ReportColumn = new ReportColumn(table, new Identifier(column)),
                Caption = column
            };
            if (AnnotationDef.IsAnnotationProperty(column))
            {
                var classMetadata = GetClassMetadata(table);
                columnInfo.Caption = AnnotationDef.GetColumnDisplayName(column);
                columnInfo.ColumnType = classMetadata.GetPropertyType(column).ReturnedClass;
                columnInfo.IsHidden = !_annotationDefNames.Contains(
                    AnnotationDef.GetColumnKey(column));
            }
            else if (RatioPropertyAccessor.IsRatioOrRdotpProperty(column))
            {
                var classMetadata = GetClassMetadata(table);
                columnInfo.Caption = RatioPropertyAccessor.GetDisplayName(column);
                columnInfo.ColumnType = classMetadata.GetPropertyType(column).ReturnedClass;
                if (RatioPropertyAccessor.IsRatioGsProperty(column))
                    columnInfo.Format = Formats.GLOBAL_STANDARD_RATIO;
                else if (RatioPropertyAccessor.IsRatioProperty(column))
                    columnInfo.Format = Formats.STANDARD_RATIO;
                else if (RatioPropertyAccessor.IsRdotpProperty(column))
                    columnInfo.Format = Formats.STANDARD_RATIO;
            }
            else
            {
                PropertyInfo propertyInfo = table.GetProperty(column);
                columnInfo.ColumnType = propertyInfo.PropertyType;
                foreach (QueryColumn attr in propertyInfo.GetCustomAttributes(typeof(QueryColumn), true))
                {
                    columnInfo.Caption = attr.FullName ?? columnInfo.Caption;
                    columnInfo.Format = attr.Format ?? columnInfo.Format;
                    columnInfo.IsHidden = attr.IsHidden;
                }
            }
            return columnInfo;
        }

        public ColumnInfo GetColumnInfo(ReportColumn reportColumn)
        {
            Type lastTable;
            String columnName;
            Resolve(reportColumn.Table, reportColumn.Column, out lastTable, out columnName);
            ColumnInfo result = GetColumnInfo(lastTable, columnName);
            result.ReportColumn = reportColumn;
            return result;
        }

        public IList<Type> GetTables()
        {
            List<Type> result = new List<Type>();
            foreach (var entry in _sessionFactory.GetAllClassMetadata())
            {
                result.Add(entry.Value.GetMappedClass(EntityMode.Poco));
            }
            return result;
        }

        public bool Resolve(Type table, Identifier identifier, out Type resultTable, out String column)
        {
            if (identifier.Parts.Count == 1)
            {
                resultTable = table;
                column = identifier.Parts[0];
                return true;
            }
            PropertyInfo propertyInfo = table.GetProperty(identifier.Parts[0]);
            return Resolve(propertyInfo.PropertyType, identifier.RemovePrefix(1), out resultTable, out column);
        }
    }
}
