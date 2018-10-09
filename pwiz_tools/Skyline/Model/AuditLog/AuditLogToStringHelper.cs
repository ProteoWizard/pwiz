﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using pwiz.Common.SystemUtil;

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

        private static string InvariantToString(object obj)
        {
            if (Reflector.HasToString(obj))
            {
                var objStr = string.Format(CultureInfo.InvariantCulture, "{0}", obj);
                var type = obj.GetType();
                if (type == typeof(double) || type == typeof(bool) || type == typeof(int))
                    return AuditLogParseHelper.GetParseString(ParseStringType.primitive, objStr);
                else if (type.IsEnum)
                    return LogMessage.Quote(AuditLogParseHelper.GetParseString(ParseStringType.enum_fn, type.Name + '_' + objStr));
                return LogMessage.Quote(objStr); // Not L10N
            }

            return null;
        }

        public class AuditLogToStringException : Exception
        {
            public AuditLogToStringException(object obj) : base(
                string.Format("Failed to convert object of type \"{0}\" to a string", obj.GetType().Name)) // Not L10N
            {
            }
        }

        public static string ToString(object obj, Func<object, string> defaultToString)
        {
            return InvariantToString(obj) ??
                   KnownTypeToString(obj) ??
                   defaultToString(obj) ??
                   throw new AuditLogToStringException(obj);

        }

        private static KeyValuePair<Type, Func<object, string>> GetConversion(object obj)
        {
            return _conversionFuncs.FirstOrDefault(conv => conv.Key.IsInstanceOfType(obj));
        }

        // Use for non user defined types or types where the AuditLogText would look the same
        // for all derived types

        private static string KnownTypeToString(object obj)
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
