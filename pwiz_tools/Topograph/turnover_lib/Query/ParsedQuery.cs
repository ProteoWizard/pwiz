using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace pwiz.Topograph.Query
{
    public class ParsedQuery
    {
        private String _hql;
        private ParsedQuery()
        {
            Columns = new List<Identifier>();
        }
        public ParsedQuery(String hql) : this()
        {
            Hql = hql;
        }
        public ParsedQuery(Type table) : this()
        {
            From = "FROM " + table + " T";
            TableName = table.ToString();
            TableAlias = "T";
        }
        public String Hql
        {
            get
            {
                return _hql;
            }
            set
            {
                _hql = value;
                Parse();
            }
        }
        private const string patColumn = "[^',]*('(''|[^'])*')*[^',]*";

        private static readonly Regex regexSelect = new Regex(
            @"\s*SELECT\s+(" + patColumn + "(," + patColumn + @")*)\s*(FROM\s+(\w+(\.\w+)*)\s+(\w*))", RegexOptions.IgnoreCase);
        private static readonly Regex regexColumn = new Regex(patColumn);
        private void Parse()
        {
            Columns = new List<Identifier>();
            TableName = null;
            TableAlias = null;
            var matchSelect = regexSelect.Match(_hql);
            if (!matchSelect.Success)
            {
                return;
            }
            String strColumns = matchSelect.Groups[1].ToString();
            TableName = matchSelect.Groups[8].ToString();
            TableAlias = matchSelect.Groups[10].ToString();
            From = _hql.Substring(matchSelect.Groups[7].Index);
            Match match;
            while ((match = regexColumn.Match(strColumns)).Success)
            {
                if (match.Index != 0)
                {
                    break;
                }
                String strColumn = match.ToString().Trim();
                Columns.Add(Identifier.Parse(strColumn));
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
        public IList<Identifier> Columns
        {
            get; private set;
        }
        public void SetColumns(IList<Identifier> columns)
        {
            var hql = new StringBuilder("SELECT ");
            var comma = "";
            foreach (var column in columns)
            {
                hql.Append(comma);
                comma = ",\r\n\t";
                hql.Append(column);
            }
            hql.Append("\r\n");
            hql.Append(From);
            Hql = hql.ToString();
            Columns = new List<Identifier>(columns);
        }
    }
}
