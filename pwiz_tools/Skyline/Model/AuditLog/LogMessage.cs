/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.AuditLog
{
    [XmlRoot(XML_ROOT)]
    public class LogMessage : Immutable, IXmlSerializable
    {
        private readonly int[] _expectedNames = {2, 3, 2, 1, 1, 1, 2, 2, 2, 0, 0, 1, 1};
        public const string XML_ROOT = "message"; // Not L10N

        public LogMessage(LogLevel level, MessageType type, string reason, bool expanded, params string[] names)
        {
            if (GetExpectedNameCount(type) != names.Length)
                throw new ArgumentException();

            Level = level;
            Type = type;
            Names = ImmutableList.ValueOf(names);
            Reason = reason;
            Expanded = expanded;
        }

        public LogLevel Level { get; private set; }
        public MessageType Type { get; private set; }
        public string Reason { get; private set; }
        public bool Expanded { get; private set; }
        public IList<string> Names { get; private set; }

        public LogMessage ChangeReason(string reason)
        {
            return ChangeProp(ImClone(this), im => im.Reason = reason);
        }

        public static string Quote(string s)
        {
            if (s == null)
                return null;

            return "\"" + s + "\""; // Not L10N
        }

        public int GetExpectedNameCount(MessageType type)
        {
            return _expectedNames[(int) type];
        }

        public override string ToString()
        {
            var names = Names.Select(s => (object) LocalizeLogStringProperties(s)).ToArray();
            switch (Type)
            {
                case MessageType.is_:
                    return string.Format(AuditLogStrings.LogMessage_ToString__0__is__1_, names);
                case MessageType.changed_from_to:
                    return string.Format(AuditLogStrings.LogMessage_ToString__0__changed_from__1__to__2_, names);
                case MessageType.changed_to:
                    return string.Format(AuditLogStrings.LogMessage_ToString__0__changed_to__1_, names);
                case MessageType.changed:
                    return string.Format(AuditLogStrings.LogMessage_ToString__0__changed, names);
                case MessageType.removed:
                    return string.Format(AuditLogStrings.LogMessage_ToString__0__was_removed, names);
                case MessageType.added:
                    return string.Format(AuditLogStrings.LogMessage_ToString__0__was_added, names);
                case MessageType.contains:
                    return string.Format(AuditLogStrings.LogMessage_ToString__0___contains__1_, names);
                case MessageType.removed_from:
                    return string.Format(AuditLogStrings.LogMessage_ToString__0____1__was_removed, names);
                case MessageType.added_to:
                    return string.Format(AuditLogStrings.LogMessage_ToString__0____1__was_added, names);
                case MessageType.log_disabled:
                    return AuditLogStrings.LogMessage_ToString_Audit_logging_has_been_disabled;
                case MessageType.log_enabled:
                    return AuditLogStrings.LogMessage_ToString_Audit_logging_has_been_enabled;
                case MessageType.log_unlogged_changes:
                    return string.Format(AuditLogStrings.LogMessage_ToString__0__unlogged_changes, names);
                case MessageType.log_cleared:
                    return string.Format(AuditLogStrings.LogMessage_ToString__0__changes_cleared, names);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static readonly Func<string, string>[] LOCALIZER_FUNCTIONS =
        {
            s => PropertyNames.ResourceManager.GetString(s),
            s => PropertyElementNames.ResourceManager.GetString(s),
            s => AuditLogStrings.ResourceManager.GetString(s),
            LocalizeValue
        };

        private static string LocalizeValue(string s)
        {
            var result = s;

            bool b;
            int i;
            double d;
            if (bool.TryParse(s, out b))
                result = b ? AuditLogStrings.LogMessage_LocalizeValue_True : AuditLogStrings.LogMessage_LocalizeValue_False;
            else if (int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out i))
                result = i.ToString(CultureInfo.CurrentCulture);
            else if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out d))
                result = d.ToString(CultureInfo.CurrentCulture);

            return Quote(result);
        }

        // Replaces all unlocalized strings (e.g {0:PropertyName}) with their
        // corresponding localized string
        public static string LocalizeLogStringProperties(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            var expressionStartIndex = -1;

            for (var i = 0; i < str.Length; ++i)
            {
                if (str[i] == '{')
                {
                    expressionStartIndex = i;
                } 
                else if (str[i] == '}')
                {
                    if (expressionStartIndex >= 0 && i - expressionStartIndex - 1 > 0)
                    {
                        var subStr = str.Substring(expressionStartIndex + 1, i - expressionStartIndex - 1);

                        // The strings are formatted like this i:name, where i indicates the localizer function and
                        // name the name of the resource
                        int index;
                        if (int.TryParse(subStr[0].ToString(), out index) && index >= 0 &&
                            index < LOCALIZER_FUNCTIONS.Length)
                        {
                            var localized = LOCALIZER_FUNCTIONS[index](subStr.Substring(2));
                            if (localized != null)
                            {
                                str = str.Substring(0, expressionStartIndex) + localized + str.Substring(i + 1);
                                i = expressionStartIndex + localized.Length - 1;
                            }
                        } 
                    }

                    expressionStartIndex = -1;
                }
            }

            return str;
        }

        protected bool Equals(LogMessage other)
        {
            return Type == other.Type && CollectionUtil.EqualsDeep(Names, other.Names) &&
                   Expanded == other.Expanded;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((LogMessage) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (int) Type;
                hashCode = (hashCode * 397) ^ (Names != null ? Names.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Expanded.GetHashCode();
                return hashCode;
            }
        }

        #region Implementation of IXmlSerializable
        private LogMessage()
        {

        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        private enum ATTR
        {
            type
        }

        private enum EL
        {
            reason,
            name
        }

        public static LogMessage Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new LogMessage());
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttribute(ATTR.type, Type);

            if(!string.IsNullOrEmpty(Reason))
                writer.WriteElementString(EL.reason, Reason);

            foreach (var name in Names)
                writer.WriteElementString(EL.name, name);
        }

        public void ReadXml(XmlReader reader)
        {
            Type = (MessageType) Enum.Parse(typeof(MessageType), reader.GetAttribute(ATTR.type));
            reader.ReadStartElement();

            var names = new List<string>();
            
            Reason = reader.IsStartElement(EL.reason)
                ? reader.ReadElementString()
                : string.Empty;

            while (reader.IsStartElement(EL.name))
                names.Add(reader.ReadElementString());

            if (names.Count != GetExpectedNameCount(Type))
                throw new XmlException();

            Names = ImmutableList<string>.ValueOf(names);

            // Some messages have no names, so they don't have an end element
            if (reader.NodeType == XmlNodeType.EndElement && reader.Name == XML_ROOT)
                reader.ReadEndElement();
        }
        #endregion
    }
}