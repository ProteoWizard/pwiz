using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;

namespace pwiz.Skyline.Model.AuditLog
{
    public static class AuditLogToStringHelper
    {
        private static readonly Dictionary<Type, Func<object, string>> _conversionFuncs = new Dictionary<Type, Func<object, string>>
        {
            { typeof(Brush), BrushToString },
            {
                typeof(Uri), UriToString
            }
        };

        public static string InvariantToString(object obj)
        {
            if (Reflector.HasToString(obj))
            {
                var type = obj.GetType();
                var format = "\"{0}\""; // Not L10N
                if (type == typeof(double) || type == typeof(bool) || type == typeof(int))
                    format = "{{3:{0}}}"; // Not L10N

                return string.Format(CultureInfo.InvariantCulture, format, obj);
            }

            return null;
        }

        private static KeyValuePair<Type, Func<object, string>> GetConversion(object obj)
        {
            return _conversionFuncs.FirstOrDefault(conv => conv.Key.IsInstanceOfType(obj));
        }

        public static string KnownTypeToString(object obj)
        {
            if (!IsKnownType(obj))
                return null;

            var conversion = GetConversion(obj);
            return conversion.Value(obj);
        }

        public static bool IsKnownType(object obj)
        {
            return !GetConversion(obj).Equals(default(KeyValuePair<Type, Func<object, string>>));
        }

        // Conversion functions
        private static string BrushToString(object obj)
        {
            var solidBrush = obj as SolidBrush;
            var color = solidBrush == null ? new Pen((Brush)obj).Color : solidBrush.Color;
            return RgbHexColor.GetHex(color);
        }

        private static string UriToString(object obj)
        {
            return ((Uri)obj).ToString();
        }
    }
}
