using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Topograph.Query
{
    public class ColumnInfo
    {
        public Identifier Identifier { get; set; }
        public String Format { get; set; }
        public String Caption { get; set; }
        public Type ColumnType { get; set; }
        private bool _isHidden;
        public bool IsHidden
        {
            get { return _isHidden || Caption == null; }
            set { _isHidden = value; }
        }
        public bool IsNumeric
        {
            get
            {
                return Equals(ColumnType, typeof(int)) ||
                       Equals(ColumnType, typeof(double)) ||
                       Equals(ColumnType, typeof(int?)) ||
                       Equals(ColumnType, typeof(double?));
            }
        }
    }
}
