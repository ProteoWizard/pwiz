using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using pwiz.Common.Collections;

namespace pwiz.Common.SystemUtil
{
    public class DebugUtil
    {
        public static string ObjectToString(object obj)
        {
            return ObjectTreeToString(obj, new HashSet<object>(new IdentityEqualityComparer<object>()), 0);
        }

        private static string ObjectTreeToString(object obj, HashSet<object> visited, int indentLevel)
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

            var lines = new List<string>();
            bool enumerateProperties;
            string line = "Type=" + obj.GetType();
            if (obj.GetType().IsClass)
            {
                line += ",RuntimeHelpers.GetHashCode()=" + RuntimeHelpers.GetHashCode(obj);
                line += ",GetHashCode()=" + obj.GetHashCode();
                enumerateProperties = visited.Add(obj);
            }
            else
            {
                enumerateProperties = true;
            }
            lines.Add(line);

            if (enumerateProperties)
            {
                for (var type = obj.GetType(); type != null; type = type.BaseType)
                {
                    foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                         BindingFlags.NonPublic | BindingFlags.FlattenHierarchy))
                    {
                        string fieldName = field.Name;
                        if (type != obj.GetType())
                        {
                            fieldName = type.Name + "." + fieldName;
                        }
                        lines.Add(fieldName + "=" + ObjectTreeToString(field.GetValue(obj), visited, indentLevel + 1));
                    }
                }
            }

            return "{" + Environment.NewLine + new string('\t', indentLevel + 1)
                   + string.Join("," + Environment.NewLine + new string('\t', indentLevel + 1), lines)
                   + Environment.NewLine + new string('\t', indentLevel) + "}";
        }
    }
}
