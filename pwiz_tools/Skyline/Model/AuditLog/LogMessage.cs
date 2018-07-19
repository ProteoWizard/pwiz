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

        empty_single_arg,

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
        set_to_in_document_grid,
        pasted_document_grid,
        cleared_document_grid,
        fill_down_document_grid,

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
        removed_unrecognized_charge_states,
        imported_fasta,
        kept_empty_proteins,
        removed_empty_proteins,
        imported_assay_library,
        imported_transition_list,
        imported_spectral_library_intensities,
        imported_docs,
        imported_annotations,

        set_included_standard,
        set_excluded_standard,
        canceled_import,
        removed_below_cutoffs,
        excluded_peptides,
        added_spectral_library,
        added_small_molecule_precursor,
        added_small_molecule_transition,
        added_small_molecule,
        added_irt_standard_peptides,
        renamed_node,
        added_new_peptide_group,
        added_peptides_to_peptide_group,
        drag_and_dropped_nodes,
        pasted_small_molecule_transition_list,
        applied_peak_subsequent,
        applied_peak_all,
        removed_all_peaks_from,
        removed_peak_from,
        picked_peak,
        changed_peak_start,
        changed_peak_end,
        changed_peak_bounds,
        changed_peak_start_all,
        changed_peak_end_all,
        removed_rt_outliers,
        removed_peptides_above_cutoff,
        added_peptide_from_library,
        added_all_peptides_from_library,
        matched_modifications_of_library,
        reintegrated_peaks,
        imported_peptide_search,
        cleared_cell_in_document_grid,
        managed_results,
        upgraded_background_proteome,
        added_new_peptide_group_from_background_proteome,
        added_peptides_to_peptide_group_from_background_proteome,
        imported_single_document,
        imported_assay_library_from_file,
        imported_transition_list_from_file,

        log_error,
        log_error_old_msg,
        deleted_target,
        imported_doc
    }

    /// <summary>
    /// Container for type and parameters of message. The names
    /// are used as format arguments for the string corresponding the the message type
    /// </summary>
    public class MessageInfo : Immutable
    {
        public MessageInfo(MessageType type, params object[] names)
        {
            Type = type;
            Names = ImmutableList.ValueOf(names.Select(obj => (obj == null ? string.Empty : obj.ToString())));
        }

        public LogMessage ToMessage(LogLevel logLevel)
        {
            return new LogMessage(logLevel, Type, string.Empty, false, Names.Select(s => (object)s).ToArray());
        }

        public MessageType Type { get; private set; }
        public IList<string> Names { get; private set; }

        public XmlSchema GetSchema()
        {
            return null;
        }

        private enum EL
        {
            type,
            name
        }

        public static MessageInfo ReadXml(XmlReader reader)
        {
            var type = (MessageType)Enum.Parse(typeof(MessageType), reader.ReadElementString());

            var names = new List<object>();
            while (reader.IsStartElement(EL.name))
                names.Add(reader.ReadElementString());

            return new MessageInfo(type, names.ToArray());
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteElementString(EL.type, Type.ToString());

            foreach (var name in Names)
                writer.WriteElementString(EL.name, name);
        }

        protected bool Equals(MessageInfo other)
        {
            return Type == other.Type && ArrayUtil.EqualsDeep(Names, other.Names);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((MessageInfo) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int) Type * 397) ^ (Names != null ? Names.GetHashCode() : 0);
            }
        }
    }

    /// <summary>
    /// Log message that gets written to the skyline document and displayed
    /// in the audit log form. (Corresponds to a single row)
    /// </summary>
    [XmlRoot(XML_ROOT)]
    public class LogMessage : Immutable, IXmlSerializable
    {
        public const string XML_ROOT = "message"; // Not L10N
        public const string MISSING = "{2:Missing}"; // Not L10N
        public const string EMPTY = "{2:Empty}"; // Not L10N

        // These are referred to by index in log strings.
        // For instance, the string "{2:Missing}" (above) would get localized by
        // passing "Missing" into the function at index 2.
        private static readonly Func<string, string>[] LOCALIZER_FUNCTIONS =
        {
            s => PropertyNames.ResourceManager.GetString(s),
            s => PropertyElementNames.ResourceManager.GetString(s),
            s => AuditLogStrings.ResourceManager.GetString(s),
            LocalizeValue,
            s => Resources.ResourceManager.GetString(s)
        };

        public LogMessage(LogLevel level, MessageInfo info, string reason, bool expanded)
        {
            Level = level;
            MessageInfo = info;
            Reason = reason;
            Expanded = expanded;
            
        }

        public LogMessage(LogLevel level, MessageType type, string reason, bool expanded, params object[] names) :
            this(level, new MessageInfo(type, names), reason, expanded)
        {
        }

        public MessageInfo MessageInfo { get; private set; }
        public IList<string> Names { get { return MessageInfo.Names; } }
        public MessageType Type { get { return MessageInfo.Type; } }
        public LogLevel Level { get; private set; }
        public string Reason { get; private set; }
        public bool Expanded { get; private set; }

        public LogMessage ChangeReason(string reason)
        {
            return ChangeProp(ImClone(this), im => im.Reason = reason);
        }

        public static string Quote(string s)
        {
            if (s == null)
                return null;

            return string.Format("\"{0}\"", s);
        }

        public override string ToString()
        {
            var names = Names.Select(s => (object)LocalizeLogStringProperties(s)).ToArray();

            // If the string could not be found, list the names in brackets and separated by commas
            var format = AuditLogStrings.ResourceManager.GetString(Type.ToString());
            return string.IsNullOrEmpty(format)
                ? string.Format("[" + string.Join(", ", Enumerable.Range(0, names.Length).Select(i => "{" + i + "}")) + "]", names) // Not L10N
                : string.Format(format, names);
        }

        // bools, ints and doubles are localized
        // TODO: localize enums
        private static string LocalizeValue(string s)
        {
            var result = s;

            bool b; int i; double d;
            if (bool.TryParse(s, out b))
                result = b ? AuditLogStrings.LogMessage_LocalizeValue_True : AuditLogStrings.LogMessage_LocalizeValue_False;
            else if (int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out i))
                result = i.ToString(CultureInfo.CurrentCulture);
            else if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out d))
                result = d.ToString(CultureInfo.CurrentCulture);

            return Quote(result);
        }

        /// <summary>
        /// Replaces all unlocalized strings (e.g {0:PropertyName}) with their
        /// corresponding localized string
        /// </summary>
        /// <param name="str">string to localize</param>
        /// <returns>localized string</returns>
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
            return Equals(MessageInfo, other.MessageInfo) && Level == other.Level &&
                   string.Equals(Reason, other.Reason) && Expanded == other.Expanded;
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
                var hashCode = (MessageInfo != null ? MessageInfo.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (int) Level;
                hashCode = (hashCode * 397) ^ (Reason != null ? Reason.GetHashCode() : 0);
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

        private enum EL
        {
            reason,
        }

        public static LogMessage Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new LogMessage());
        }

        public void WriteXml(XmlWriter writer)
        {
            MessageInfo.WriteXml(writer);

            if(!string.IsNullOrEmpty(Reason))
                writer.WriteElementString(EL.reason, Reason);
        }

        public void ReadXml(XmlReader reader)
        {
            reader.ReadStartElement();

            MessageInfo = MessageInfo.ReadXml(reader);

            Reason = reader.IsStartElement(EL.reason)
                ? reader.ReadElementString()
                : string.Empty;

            reader.ReadEndElement();
        }
        #endregion
    }
}