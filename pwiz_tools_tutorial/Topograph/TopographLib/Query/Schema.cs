using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NHibernate;
using NHibernate.Metadata;

namespace pwiz.Topograph.Query
{
    public class Schema
    {
        private readonly ISessionFactory _sessionFactory;
        public Schema(ISessionFactory sessionFactory)
        {
            _sessionFactory = sessionFactory;
        }

        public IClassMetadata GetClassMetadata(Type type)
        {
            return _sessionFactory.GetClassMetadata(type);
        }

        private PropertyInfo GetProperty(Type type, String property)
        {
            var propertyInfo = type.GetProperty(property);
            if (propertyInfo != null)
            {
                return propertyInfo;
            }
            if (type.BaseType != null)
            {
                return GetProperty(type.BaseType, property);
            }
            return null;
        }

        public ColumnInfo GetColumnInfo(Type table, String column)
        {
            PropertyInfo propertyInfo = GetProperty(table, column);
            ColumnInfo columnInfo = 
                new ColumnInfo
                {
                    Identifier = new Identifier(column),
                    Caption = column,
                    ColumnType = propertyInfo.PropertyType
                };
            var attributes = propertyInfo.GetCustomAttributes(typeof (QueryColumn), true);
            if (attributes != null)
            {
                foreach (QueryColumn attr in propertyInfo.GetCustomAttributes(typeof(QueryColumn), true))
                {
                    columnInfo.Caption = attr.FullName ?? columnInfo.Caption;
                    columnInfo.Format = attr.Format ?? columnInfo.Format;
                }
            }
            return columnInfo;
        }
        public ColumnInfo GetColumnInfo(Type table, Identifier column)
        {
            Type lastTable;
            String columnName;
            Resolve(table, column, out lastTable, out columnName);
            ColumnInfo result = GetColumnInfo(lastTable, columnName);
            result.Identifier = column;
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
