using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Topograph.Data.Snapshot
{
    public class Snapshot
    {
        public static String ToIdList(IList<long> ids)
        {
            var result = new StringBuilder();
            var comma = "";
            foreach (var id in ids)
            {
                result.Append(id);
                result.Append(comma);
                comma = ",";
            }
            return result.ToString();
        }
    }
}
