using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace pwiz.Topograph.Query
{
    public class ParsedQuery
    {
        private ParsedQuery()
        {
            Columns = new List<SelectColumn>();
        }
        public ParsedQuery(String hql) : this()
        {
            Parse(hql);
        }
        public ParsedQuery(Type table) : this()
        {
            From = "FROM " + table.Name + " T";
            TableName = table.Name;
            TableAlias = "T";
        }
        public String GetSourceHql()
        {
            return GetHql(false);
        }

        public String GetExecuteHql()
        {
            return GetHql(true);
        }

        private String GetHql(bool toExecute)
        {
            var result = new StringBuilder("SELECT ");
            String strComma = "";
            foreach (var column in Columns)
            {
                result.Append(strComma);
                strComma = ",\r\n\t";
                result.Append(column.Expression);
                if (!toExecute && column.Alias != null)
                {
                    result.Append(" AS ");
                    result.Append(column.Alias);
                }
            }
            result.Append("\r\n");
            result.Append(From);
            return result.ToString();
        }
        public void SetHql(String hql)
        {
            Parse(hql);
        }
        private const string patColumn = @"[^',]*('(''|[^'])*')*[^',]*";
        private const string patAlias = @"(.*[^\w])AS\s+([\w]+)\s*";

        private static readonly Regex regexFrom = new Regex(@"\W(FROM)\W", RegexOptions.IgnoreCase);
        private static readonly Regex regexTableAlias = new Regex(@"(\w+(\.\w+)*)\s+(\w+)");
        private static readonly Regex regexColumn = new Regex(patColumn, RegexOptions.IgnoreCase);
        private static readonly Regex regexAlias = new Regex(patAlias, RegexOptions.IgnoreCase);
        private void Parse(String hql)
        {
            Columns = new List<SelectColumn>();
            TableName = null;
            TableAlias = null;
            var matchFrom = regexFrom.Match(hql);
            if (!matchFrom.Success)
            {
                return;
            }
            String strColumns = hql.Substring(0, matchFrom.Groups[1].Index);
            strColumns = strColumns.Trim();
            if (!strColumns.ToLower().StartsWith("select"))
            {
                return;
            }
            strColumns = strColumns.Substring(6);
            From = hql.Substring(matchFrom.Groups[1].Index);
            var matchTableName = regexTableAlias.Match(From.Substring(4));
            if (!matchTableName.Success)
            {
                return;
            }
            
            TableName = matchTableName.Groups[1].ToString();
            TableAlias = matchTableName.Groups[3].ToString();
            Match match;
            while ((match = regexColumn.Match(strColumns)).Success)
            {
                if (match.Index != 0)
                {
                    break;
                }
                String strExpression, strAlias;
                Match matchAlias = regexAlias.Match(match.Groups[0].ToString());
                if (matchAlias.Success)
                {
                    strExpression = matchAlias.Groups[1].ToString().Trim();
                    strAlias = matchAlias.Groups[2].ToString();
                }
                else
                {
                    strExpression = match.Groups[0].ToString().Trim();
                    strAlias = null;
                }
                Columns.Add(new SelectColumn(this){Expression = strExpression, Alias = strAlias});
                strColumns = strColumns.Substring(match.Length);
                if (!strColumns.StartsWith(","))
                {
                    break;
                }
                strColumns = strColumns.Substring(1);
            }
        }
        public String From { get; private set; }
        public String TableName { get; private set; }
        public String TableAlias { get; private set; }
        public IList<SelectColumn> Columns
        {
            get; private set;
        }
        public void SetColumns(IList<SelectColumn> columns)
        {
            var hql = new StringBuilder("SELECT ");
            var comma = "";
            foreach (var column in columns)
            {
                hql.Append(comma);
                comma = ",\r\n\t";
                hql.Append(column.ToHql());
            }
            hql.Append("\r\n");
            hql.Append(From);
            Parse(hql.ToString());
        }
        public class SelectColumn
        {
            public SelectColumn(ParsedQuery parsedQuery)
            {
                ParsedQuery = parsedQuery;
            }
            public ParsedQuery ParsedQuery { get; private set; }
            public String Expression { get; set; }
            public String Alias { get; set; }
            public override String ToString()
            {
                if (Alias != null)
                {
                    return Alias;
                }
                Identifier id = Identifier.Parse(Expression);
                if (id.Parts[0] == ParsedQuery.TableAlias)
                {
                    return id.RemovePrefix(1).ToString();
                }
                return id.ToString();
            }
            public String ToHql()
            {
                if (Alias == null)
                {
                    return Expression;
                }
                return Expression + " AS " + Alias;
            }
            public String GetColumnName()
            {
                return ToString();
            }
        }
    }
}
