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
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.AuditLog
{
    public enum MessageType
    {
        none,

        // Settings
        is_,
        changed_from_to,
        changed_to,
        changed,
        contains,
        removed_from,
        added_to,

        // Log
        log_disabled,
        log_enabled,
        log_unlogged_changes,
        log_cleared,

        // Targets window
        deleted_targets,
        pasted_targets,
        picked_children,
        inserted_data,
        edited_note,
        set_standard_type,
        set_standard_type_peptides,
        modified,

        // Document grid
        edited_document_grid,
        pasted_document_grid,

        // Refine
        remove_empty_proteins,
        remove_empty_peptides,
        remove_duplicate_peptides,
        remove_repeated_peptides,
        remove_missing_results,
        renamed_proteins,
        renamed_single_protein,
        inserted_fasta,
        inserted_proteins,
        inserted_peptides,
        inserted_transitions,
        accepted_proteins,
        accept_peptides,
        accept_peptides_no_options,
        sort_protein_name,
        sort_protein_accession,
        sort_protein_preferred_name,
        sort_protein_gene,
        added_peptide_decoys,
        refined_targets,
        associated_proteins_fasta,
        associated_proteins_bg,
        associated_peptide_with_protein,

        // File > Import
        imported_results,
        imported_peak_boundaries,
        removed_unrecognized_peptides,
        removed_unrecognized_files,
        removed_unrecognized_charge_states
    }

    [XmlRoot(XML_ROOT)]
    public class LogMessage : Immutable, IXmlSerializable
    {
        public const string XML_ROOT = "message"; // Not L10N
        public const string MISSING = "{2:Missing}"; // Not L10N

        public LogMessage(LogLevel level, MessageType type, string reason, bool expanded, params string[] names)
        {
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

        public LogMessage ChangeType(MessageType type)
        {
            return ChangeProp(ImClone(this), im => im.Type = type);
        }

        public LogMessage ChangeReason(string reason)
        {
            return ChangeProp(ImClone(this), im => im.Reason = reason);
        }

        public LogMessage ChangeNames(IList<string> names)
        {
            return ChangeProp(ImClone(this), im => im.Names = names);
        }

        public static string Quote(string s)
        {
            if (s == null)
                return null;

            return string.Format("\"{0}\"", s);
        }

        public override string ToString()
        {
            var names = Names.Select(s => (object) LocalizeLogStringProperties(s)).ToArray();

            var format = AuditLogStrings.ResourceManager.GetString(Type.ToString());
            if (string.IsNullOrEmpty(format))
                return string.Format("<" + string.Join(", ", Enumerable.Range(0, names.Length).Select(i => "{" + i + "}")) + ">", names); // Not L10N

            return string.Format(format, names);
        }

        private static readonly Func<string, string>[] LOCALIZER_FUNCTIONS =
        {
            s => PropertyNames.ResourceManager.GetString(s),
            s => PropertyElementNames.ResourceManager.GetString(s),
            s => AuditLogStrings.ResourceManager.GetString(s),
            LocalizeValue,
            s => Resources.ResourceManager.GetString(s)
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
                int hashCode = Type.GetHashCode();
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

            Names = ImmutableList.ValueOf(names);

            // Some messages have no names, so they don't have an end element
            if (reader.NodeType == XmlNodeType.EndElement && reader.Name == XML_ROOT)
                reader.ReadEndElement();
        }
        #endregion
    }
}