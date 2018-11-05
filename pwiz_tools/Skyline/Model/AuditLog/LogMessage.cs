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
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.AuditLog
{
    // Each type has a corresponding string in AuditLogStrings.resx
    public enum MessageType
    {
        none,

        test_only,

        added_to,
        changed,
        changed_from_to,
        changed_to,
        contains,
        empty_single_arg,
        is_,
        log_cleared,
        log_disabled,
        log_enabled,
        log_unlogged_changes,
        removed_all,
        removed_from,

        accept_peptides,
        accepted_peptide,
        accepted_protein,
        accepted_proteins,
        added_all_peptides_from_library,
        added_irt_standard_peptides,
        added_new_peptide_group,
        added_new_peptide_group_from_background_proteome,
        added_peptide_decoy,
        added_peptide_decoys,
        added_peptide_from_library,
        added_peptides_to_peptide_group,
        added_peptides_to_peptide_group_from_background_proteome,
        added_small_molecule,
        added_small_molecule_precursor,
        added_small_molecule_transition,
        added_spectral_library,
        applied_peak_all,
        applied_peak_subsequent,
        associated_peptides_with_protein,
        associated_peptides_with_proteins,
        canceled_import,
        changed_peak_bounds,
        changed_peak_bounds_of,
        changed_peak_end,
        changed_peak_end_all,
        changed_peak_start,
        changed_peak_start_all,
        cleared_cell_in_document_grid,
        cleared_document_grid,
        cleared_document_grid_single,
        deleted_target,
        deleted_targets,
        drag_and_dropped_node,
        drag_and_dropped_nodes,
        edited_note,
        excluded_peptides,
        fill_down_document_grid,
        fill_down_document_grid_single,
        imported_annotations,
        imported_assay_library,
        imported_assay_library_from_file,
        imported_doc,
        imported_docs,
        imported_fasta,
        imported_fasta_paste,
        imported_peptide_list,
        imported_peak_boundaries,
        imported_peptide_search,
        imported_result,
        imported_results,
        imported_spectral_library_intensities,
        imported_transition_list,
        imported_transition_list_from_file,
        inserted_data,
        inserted_peptide,
        inserted_peptides,
        inserted_protein,
        inserted_proteins,
        inserted_proteins_fasta,
        inserted_transition,
        inserted_transitions,
        kept_empty_protein,
        kept_empty_proteins,
        log_cleared_single,
        log_error,
        log_error_old_msg,
        log_unlogged_change,
        managed_results,
        matched_modifications_of_library,
        modified,
        pasted_document_grid,
        pasted_document_grid_single,
        pasted_single_small_molecule_transition,
        pasted_small_molecule_transition_list,
        pasted_targets,
        picked_child,
        picked_children,
        picked_peak,
        refined_targets,
        reintegrated_peaks,
        removed_all_libraries,
        removed_all_peaks_from,
        removed_all_replicates,
        removed_below_cutoffs,
        removed_duplicate_peptide,
        removed_duplicate_peptides,
        removed_empty_peptide,
        removed_empty_peptides,
        removed_empty_protein,
        removed_empty_proteins,
        removed_library_run,
        removed_missing_results,
        removed_peak_from,
        removed_peptide_above_cutoff,
        removed_peptides_above_cutoff,
        removed_repeated_peptide,
        removed_repeated_peptides,
        removed_replicate,
        removed_rt_outlier,
        removed_rt_outliers,
        removed_single_below_cutoffs,
        removed_unrecognized_charge_state,
        removed_unrecognized_file,
        removed_unrecognized_peptide,
        renamed_node,
        renamed_proteins,
        renamed_single_protein,
        set_excluded_standard,
        set_included_standard,
        set_standard_type,
        set_standard_type_peptides,
        set_to_in_document_grid,
        sort_protein_accession,
        sort_protein_gene,
        sort_protein_name,
        sort_protein_preferred_name,
        upgraded_background_proteome,
        excluded_peptide,
        renamed_replicate,
        undocumented_change,
        modified_outside_of_skyline,
        start_log_existing_doc
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
            return new LogMessage(logLevel, Type, string.Empty, false, Names.Select(s => (object) s).ToArray());
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
        public static string MISSING = AuditLogParseHelper.GetParseString(ParseStringType.audit_log_strings, "Missing"); // Not L10N
        public static string EMPTY = AuditLogParseHelper.GetParseString(ParseStringType.audit_log_strings, "Empty"); // Not L10N


        // These are referred to by index in log strings.
        // For instance, the string "{2:Missing}" (above) would get localized by
        // passing "Missing" into the function at index 2.
        private static readonly Func<string, LogLevel, string>[] PARSE_FUNCTIONS =
        {
            (s,l) => PropertyNames.ResourceManager.GetString(s),
            (s,l) => PropertyElementNames.ResourceManager.GetString(s),
            (s,l) => AuditLogStrings.ResourceManager.GetString(s),
            (s,l) => ParsePrimitive(s),
            ParsePath,
            (s,l) => ParseColumnCaption(s),
            (s, l) => ParseEnum(s)
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

        private static string ParsePath(string s, LogLevel logLevel)
        {
            if (logLevel == LogLevel.all_info && !AuditLogEntry.ConvertPathsToFileNames)
                return s;

            return new DirectoryInfo(s).Name;
        }

        private static string ParseColumnCaption(string s)
        {
            return new DataSchemaLocalizer(CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture, ColumnCaptions.ResourceManager)
                .LookupColumnCaption(new ColumnCaption(s));
        }

        private static string ParseEnum(string s)
        {
            return EnumNames.ResourceManager.GetString(s);
        }

        public LogMessage ChangeLevel(LogLevel level)
        {
            return ChangeProp(ImClone(this), im => im.Level = level);
        }

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
            var names = Names.Select(s => (object)ParseLogString(s, Level)).ToArray();

            // If the string could not be found, list the names in brackets and separated by commas
            // TODO: consider throwing exception instead
            var format = AuditLogStrings.ResourceManager.GetString(Type.ToString());
            return string.IsNullOrEmpty(format)
                ? string.Format("[" + string.Join(", ", Enumerable.Range(0, names.Length).Select(i => "{" + i + "}")) + "]", names) // Not L10N
                : string.Format(format, names);
        }

        public static string RoundDecimal<T>(T? d, int decimalPlaces = 1) where T : struct, IFormattable
        {
            if (!d.HasValue)
                return MISSING;

            return RoundDecimal(d.Value, decimalPlaces);
        }

        public static string RoundDecimal<T>(T d, int decimalPlaces = 1) where T : IFormattable
        {
            return d.ToString("0." + new string('0', decimalPlaces), CultureInfo.CurrentCulture); // Not L10N
        }

        // bools, ints and doubles are localized
        private static string ParsePrimitive(string s)
        {
            var result = s;

            if (bool.TryParse(s, out bool b))
                return b ? AuditLogStrings.LogMessage_LocalizeValue_True : AuditLogStrings.LogMessage_LocalizeValue_False; // Don't quote
            else if (int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var i))
                result = i.ToString(CultureInfo.CurrentCulture);
            else if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                result = d.ToString(Program.FunctionalTest ? @"R" : null, CultureInfo.CurrentCulture);

            return Quote(result);
        }

        /// <summary>
        /// Replaces all unlocalized strings (e.g {0:PropertyName}) with their
        /// corresponding localized string
        /// </summary>
        /// <param name="str">string to localize</param>
        /// <param name="logLevel">Log level used when parsing log level dependent values such as Paths</param>
        /// <returns>localized string</returns>
        public static string ParseLogString(string str, LogLevel logLevel)
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
                            index < PARSE_FUNCTIONS.Length)
                        {
                            var parsed = PARSE_FUNCTIONS[index](subStr.Substring(2), logLevel);
                            if (parsed != null)
                            {
                                str = str.Substring(0, expressionStartIndex) + parsed + str.Substring(i + 1);
                                i = expressionStartIndex + parsed.Length - 1;
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