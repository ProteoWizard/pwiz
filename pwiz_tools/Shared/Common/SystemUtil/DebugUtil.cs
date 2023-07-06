using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using pwiz.Common.Collections;

namespace pwiz.Common.SystemUtil
{
    public class DebugUtil
    {
        public static string ObjectToString(object obj)
        {
            return ObjectTreeToString(obj, new HashSet<object>(new IdentityEqualityComparer<object>()));
        }

        private static string ObjectTreeToString(object obj, HashSet<object> visited)
        {
            if (obj == null)
            {
                return "(null)";
            }
            if (obj is string s)
            {
                return "\"" + s.Replace("\"", "\\\"") + "\"";
            }

            if (obj.GetType().IsPrimitive)
            {
                return LocalizationHelper.CallWithCulture(CultureInfo.InvariantCulture, obj.ToString);
            }
            var parts = new List<string>();
            bool enumerateProperties;
            parts.Add("Type=" + obj.GetType());
            if (obj.GetType().IsClass)
            {
                parts.Add("RuntimeHelpers.GetHashCode()=" + RuntimeHelpers.GetHashCode(obj));
                parts.Add("GetHashCode()=" + obj.GetHashCode());
                enumerateProperties = visited.Add(obj);
            }
            else
            {
                enumerateProperties = true;
            }

            if (enumerateProperties)
            {
                foreach (var field in obj.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                              BindingFlags.NonPublic | BindingFlags.FlattenHierarchy))
                {
                    parts.Add(field.Name + "=" + ObjectTreeToString(field.GetValue(obj), visited));
                }
            }

            return "{" + string.Join(",", parts) + "}";
        }
    }
}
