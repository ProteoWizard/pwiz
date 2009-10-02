using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace pwiz.Topograph.Query
{
    [XmlRoot]
    public class QueryDef
    {
        private String _hql;
        [XmlElement]
        public String Hql { get
        {
            return _hql;
        } 
            set
            {
                _hql = value;
                if (_hql != null)
                {
                    _hql = _hql.Replace("\r\n", "\n").Replace("\n", "\r\n");
                }
            }
        }
    }
}
