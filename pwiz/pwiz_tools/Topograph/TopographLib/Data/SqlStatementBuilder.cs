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
using System.Text;
using NHibernate;
using NHibernate.Dialect;

namespace pwiz.Topograph.Data
{
    public class SqlStatementBuilder
    {
        public String QuoteValue(object value)
        {
            const string hexDigits = "0123456789ABCDEF";
            if (value == null || value is DBNull)
            {
                return "NULL";
            }
            if (value is String)
            {
                var strValue = (String)value;
                return "'" + strValue.Replace("'", "''") + "'";
            }
            if (value is byte[])
            {
                var bytes = (byte[])value;
                var result = new StringBuilder("X'");
                foreach (byte b in bytes)
                {
                    result.Append(hexDigits[b >> 4]);
                    result.Append(hexDigits[b & 0xf]);
                }
                result.Append("'");
                return result.ToString();
            }
            return value.ToString();
        }
        public SqlStatementBuilder(Dialect dialect)
        {
            Dialect = dialect;
        }
        public Dialect Dialect { get; private set;}
        public String GetInsertStatement(String table, IDictionary<String,object> values)
        {
            var statement = new StringBuilder("INSERT INTO ");
            statement.Append(Dialect.QuoteForTableName(table));
            statement.Append("(");
            var comma = "";
            foreach (var entry in values)
            {
                statement.Append(comma);
                comma = ",";
                statement.Append(Dialect.QuoteForColumnName(entry.Key));
            }
            statement.Append(")VALUES(");
            comma = "";
            foreach (var entry in values)
            {
                statement.Append(comma);
                comma = ",";
                statement.Append(QuoteValue(entry.Value));
            }
            statement.Append(")");
            return statement.ToString();
        }
        public String GetUpdateStatement(String table, IDictionary<string,object> values, IDictionary<String,object> key)
        {
            var statement = new StringBuilder("UPDATE ");
            statement.Append(Dialect.QuoteForTableName(table));
            statement.Append(" SET ");
            var comma = "";
            foreach (var entry in values)
            {
                statement.Append(comma);
                comma = ",";
                statement.Append(Dialect.QuoteForColumnName(entry.Key));
                statement.Append("=");
                statement.Append(QuoteValue(entry.Value));
            }
            statement.Append(" WHERE ");
            comma = "";
            foreach (var entry in key)
            {
                statement.Append(comma);
                comma = " AND ";
                statement.Append(Dialect.QuoteForColumnName(entry.Key));
                statement.Append(" = ");
                statement.Append(QuoteValue(entry.Value));
            }
            return statement.ToString();
        }
        private const int MaxStatementLength = 500000;
        public void ExecuteStatements(ISession session, IList<string> statements)
        {
            var combinedStatement = new StringBuilder();
            foreach (var statement in statements)
            {
                if (combinedStatement.Length > 0)
                {
                    if (combinedStatement.Length + statement.Length > MaxStatementLength)
                    {
                        var cmd = session.Connection.CreateCommand();
                        cmd.CommandText = combinedStatement.ToString();
                        cmd.ExecuteNonQuery();
                        combinedStatement = new StringBuilder();
                    }
                    else
                    {
                        combinedStatement.Append(";\n");
                    }
                }
                combinedStatement.Append(statement);
            }
            if (combinedStatement.Length > 0)
            {
                var cmd = session.Connection.CreateCommand();
                cmd.CommandText = combinedStatement.ToString();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
