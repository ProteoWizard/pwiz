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
using System.Text;
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
        associated_peptides_with_proteins,
        associated_peptides_with_protein_groups,
        canceled_import,
        changed_peak_bounds,
        changed_peak_bounds_of,
        changed_peak_end,
        changed_peak_end_all,
        changed_peak_start,
        changed_peak_start_all,
        changed_quantitative,
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
        removed_peaks,
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
        start_log_existing_doc,
        edited_ion_mobility_library,
        permuted_isotope_label_simple,
        permuted_isotope_label_complete,
    } // N.B. as you add to this, consider whether or not the human-readable message may want to refuse the "peptide"->"molecule" translation for small molecule UI (see ModeUIInvariantMesdsageTypes below)

    /// <summary>
    /// Container for type and parameters of message. The names
    /// are used as format arguments for the string corresponding the the message type
    /// </summary>
    public class MessageInfo : Immutable
    {
        public MessageInfo(MessageType type, SrmDocument.DOCUMENT_TYPE docType, params object[] names)
        {
            Type = type;
            DocumentType = ModeUIInvariantMessageTypes.Contains(type) ? SrmDocument.DOCUMENT_TYPE.none : docType; // Ignore doc type if message should never be given peptide->molecule treatment
            Names = ImmutableList.ValueOf(names.Select(obj => (obj == null ? string.Empty : obj.ToString())));
        }

        public LogMessage ToMessage(LogLevel logLevel)
        {
            return new LogMessage(logLevel, Type, DocumentType, false, Names.Select(s => (object) s).ToArray());
        }


        public MessageType Type { get; private set; }
        public SrmDocument.DOCUMENT_TYPE DocumentType { get; private set; } // Determines whether human readable form gets the "peptide"->"molecule" translation treatment - not part of hash
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

        // This is the set of messages that never want the "peptide"->"molecule" treatment even when the document isn't purely proteomic
        public static HashSet<MessageType> ModeUIInvariantMessageTypes = new HashSet<MessageType>()
        {
        MessageType.accept_peptides,
        MessageType.accepted_peptide,
        MessageType.accepted_protein,
        MessageType.accepted_proteins,
        MessageType.added_new_peptide_group_from_background_proteome,
        MessageType.added_peptide_decoy,
        MessageType.added_peptide_decoys,
        MessageType.added_peptides_to_peptide_group_from_background_proteome,
        MessageType.associated_peptides_with_proteins,
        MessageType.associated_peptides_with_protein_groups,
        MessageType.imported_fasta,
        MessageType.imported_fasta_paste,
        MessageType.inserted_proteins_fasta,
        MessageType.matched_modifications_of_library,
        MessageType.sort_protein_accession,
        MessageType.sort_protein_gene,
        MessageType.sort_protein_name,
        MessageType.sort_protein_preferred_name,
        MessageType.upgraded_background_proteome,
        };

        public static MessageInfo ReadXml(XmlReader reader)
        {
            var typeStr = reader.ReadElementString();
            var type = (MessageType)Enum.Parse(typeof(MessageType), typeStr);
            var names = new List<object>();
            while (reader.IsStartElement(EL.name))
                names.Add(reader.ReadElementString());

            return new MessageInfo(type, SrmDocument.DOCUMENT_TYPE.none, names.ToArray()); // Caller needs to go back in and set document type along with level
        }

        public void WriteXml(XmlWriter writer)
        {
            var type = Type.ToString();
            writer.WriteElementString(EL.type, type);
            foreach (var name in Names)
                writer.WriteElementString(EL.name, name);
        }

        public MessageInfo ChangeDocumentType(SrmDocument.DOCUMENT_TYPE documentType)
        {
            return Equals(DocumentType, documentType) ? this : ChangeProp(ImClone(this), im => im.DocumentType = documentType);
        }

        protected bool Equals(MessageInfo other)
        {
            return Type == other.Type && DocumentType == other.DocumentType && Equals(Names, other.Names);
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
                var hashCode = (int) Type;
                hashCode = (hashCode * 397) ^ (int) DocumentType;
                hashCode = (hashCode * 397) ^ (Names != null ? Names.GetHashCode() : 0);
                return hashCode;
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
        public const string XML_ROOT = "message";
        public static string MISSING = AuditLogParseHelper.GetParseString(ParseStringType.audit_log_strings, @"Missing");
        public static string EMPTY = AuditLogParseHelper.GetParseString(ParseStringType.audit_log_strings, @"Empty");
        public static string NONE = AuditLogParseHelper.GetParseString(ParseStringType.audit_log_strings, @"None");

        private const int MIN_EXPR_LEN = 3; // i + : + x...
        private const int EXTRA_SPACE = 2; // { + }

        // These are referred to by index in log strings.
        // For instance, the string "{2:Missing}" (above) would get localized by
        // passing "Missing" into the function at index 2.
        private static readonly Func<string, LogLevel, CultureInfo, SrmDocument.DOCUMENT_TYPE, string>[] PARSE_FUNCTIONS =
        {
            (s,l,c,t) => ParsePropertyName(s, c, t),
            (s,l,c,t) => ParsePropertyElementName(s, c, t),
            (s,l,c,t) => ParseAuditLogString(s, c, t),
            (s,l,c,t) => ParsePrimitive(s, c, t),
            (s,l,c,t) => ParsePath(s, l),
            (s,l,c,t) => ParseColumnCaption(s, c, t),
            (s,l,c,t) => ParseEnum(s, c, t)
        };

        public LogMessage(LogLevel level, MessageInfo info, bool expanded)
        {
            Level = level;
            MessageInfo = info;
            Expanded = expanded;
        }

        public LogMessage(LogLevel level, MessageType type, SrmDocument.DOCUMENT_TYPE docType, bool expanded, params object[] names) :
            this(level, new MessageInfo(type, docType, names), expanded)
        {
        }

        public MessageInfo MessageInfo { get; protected set; }
        public IList<string> Names { get { return MessageInfo.Names; } }
        public MessageType Type { get { return MessageInfo.Type; } }
        public SrmDocument.DOCUMENT_TYPE DocumentType { get { return MessageInfo.DocumentType; } }
        public LogLevel Level { get; private set; }
        public bool Expanded { get; private set; }

        private string _enExpanded;
        public string EnExpanded
        {
            get
            {
                return _enExpanded ?? (_enExpanded = ToString(CultureInfo.InvariantCulture));
            }
            protected set { _enExpanded = value; }
        }

        public LogMessage ResetEnExpanded()
        {
            return ChangeProp(ImClone(this), im => im.EnExpanded = null);
        }

        private static string ParsePath(string s, LogLevel logLevel)
        {
            if (logLevel == LogLevel.all_info && !AuditLogEntry.ConvertPathsToFileNames)
                return s;

            return new DirectoryInfo(s).Name;
        }

        private static string ParseColumnCaption(string s, CultureInfo cultureUI, SrmDocument.DOCUMENT_TYPE docType)
        {
            var val =  new DataSchemaLocalizer(CultureInfo.CurrentCulture, cultureUI, ColumnCaptions.ResourceManager)
                .LookupColumnCaption(new ColumnCaption(s));
            return Helpers.PeptideToMoleculeTextMapper.Translate(val, docType); // Perform "peptide"->"molecule" translation as needed
        }

        private static string ParseEnum(string s, CultureInfo cultureUI, SrmDocument.DOCUMENT_TYPE docType)
        {
            var val = EnumNames.ResourceManager.GetString(s, cultureUI);
            return Helpers.PeptideToMoleculeTextMapper.Translate(val, docType); // Perform "peptide"->"molecule" translation as needed
        }

        public LogMessage ChangeLevel(LogLevel level)
        {
            return ChangeProp(ImClone(this), im => im.Level = level);
        }

        public LogMessage ChangeDocumentType(SrmDocument.DOCUMENT_TYPE documentType)
        {
            return Equals(documentType, DocumentType) ? this : ChangeProp(ImClone(this), im => im.MessageInfo = im.MessageInfo.ChangeDocumentType(documentType));
        }

        public static string Quote(string s)
        {
            if (s == null)
                return null;

            const string q = "\"";
            return q + s + q;
        }

        public virtual byte[] GetBytesForHash(Encoding encoding, CultureInfo ci)
        {
            return encoding.GetBytes(EnExpanded);
        }

        public string ToString(CultureInfo cultureUI)
        {
            var names = Names.Select(s => (object)ParseLogString(s, Level, cultureUI, DocumentType)).ToArray();

            // If the string could not be found, list the names in brackets and separated by commas
            // TODO: consider throwing exception instead
            var format = AuditLogStrings.ResourceManager.GetString(Type.ToString(), cultureUI);
            format = Helpers.PeptideToMoleculeTextMapper.Translate(format, DocumentType); // Give it the "peptide" -> "molecule" treatment if document type requires it

            return string.IsNullOrEmpty(format)
                ? string.Format(@"[" + string.Join(@", ", Enumerable.Range(0, names.Length).Select(i => @"{" + i + @"}")) + @"]", names)
                : string.Format(format, names);
        }

        public override string ToString()
        {
            return ToString(CultureInfo.CurrentUICulture);
        }

        public static string RoundDecimal<T>(T? d, int decimalPlaces = 1) where T : struct, IFormattable
        {
            if (!d.HasValue)
                return MISSING;

            return RoundDecimal(d.Value, decimalPlaces);
        }

        public static string RoundDecimal<T>(T d, int decimalPlaces = 1) where T : IFormattable
        {
            return d.ToString(@"0." + new string('0', decimalPlaces), CultureInfo.CurrentCulture);
        }

        // bools, ints and doubles are localized
        private static string ParsePrimitive(string s, CultureInfo cultureUI, SrmDocument.DOCUMENT_TYPE docType)
        {
            var result = s;

            if (bool.TryParse(s, out bool b))
                return AuditLogStrings.ResourceManager.GetString(
                    b ? @"LogMessage_LocalizeValue_True" : @"LogMessage_LocalizeValue_False", cultureUI); // Don't quote
            else if (int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out int i))
                result = i.ToString(cultureUI);
            else if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                result = d.ToString(cultureUI);

            return Quote(result);
        }

        private static string ParsePropertyName(string s, CultureInfo cultureUI, SrmDocument.DOCUMENT_TYPE docType)
        {
            var result = PropertyNames.ResourceManager.GetString(s, cultureUI);
            return Helpers.PeptideToMoleculeTextMapper.Translate(result, docType);
        }

        private static string ParsePropertyElementName(string s, CultureInfo cultureUI, SrmDocument.DOCUMENT_TYPE docType)
        {
            var result = PropertyElementNames.ResourceManager.GetString(s, cultureUI);
            return Helpers.PeptideToMoleculeTextMapper.Translate(result, docType);
        }
        private static string ParseAuditLogString(string s, CultureInfo cultureUI, SrmDocument.DOCUMENT_TYPE docType)
        {
            var result = AuditLogStrings.ResourceManager.GetString(s, cultureUI);
            return Helpers.PeptideToMoleculeTextMapper.Translate(result, docType);
        }

        public class ExpansionToken
        {
            public ExpansionToken(int startIndex, int length, int parseIndex, string parseInput)
            {
                StartIndex = startIndex;
                Length = length;
                ParseIndex = parseIndex;
                ParseInput = parseInput;
            }

            public static IEnumerable<ExpansionToken> EnumerateTokens(string str)
            {
                if (string.IsNullOrEmpty(str))
                    yield break;
                
                var expr = new StringBuilder();

                var inExpr = false;
                for (var i = 0; i < str.Length; ++i)
                {
                    switch (str[i])
                    {
                        case '{':
                            inExpr = true;
                            // There is no such thing as nested tokens. If there are other curly braces, we simply ignore
                            // what came before this opening curly brace
                            expr.Clear();
                            break;
                        case '}':
                        {
                            if (expr.Length >= MIN_EXPR_LEN)
                            {
                                var expression = expr.ToString();
                                // TODO: replace with meaningful constants. Consider allowing n-digit parse indices
                                if (expression[1] == ':' && int.TryParse(expression[0].ToString(), out var index))
                                    yield return new ExpansionToken(i - expression.Length - 1, expression.Length + EXTRA_SPACE, index,
                                        expression.Substring(2));
                            }

                            expr.Clear();
                            inExpr = false;
                            break;
                        }
                        default:
                        {
                            if (inExpr)
                                expr.Append(str[i]);
                            break;
                        }
                    }
                }
            }

            public string Parse(string s, LogLevel logLevel, CultureInfo cultureUI, SrmDocument.DOCUMENT_TYPE docType)
            {
                if (ParseIndex >= 0 && ParseIndex < PARSE_FUNCTIONS.Length)
                {
                    var parsed = PARSE_FUNCTIONS[ParseIndex](ParseInput, logLevel, cultureUI, docType);
                    if (parsed != null)
                        return parsed;
                }

                return s.Substring(StartIndex, Length);
            }

            public int StartIndex { get; private set; }
            public int Length { get; private set; }
            public int ParseIndex { get; private set; }
            public string ParseInput { get; private set; }

            public override string ToString() // For debugging convenience
            {
                return string.Format(@"si:{0} l:{1} pi:{2} in:{3}", StartIndex, Length, ParseIndex, ParseInput);
            }
        }

        /// <summary>
        /// Replaces all unlocalized strings (e.g {0:PropertyName}) with their
        /// corresponding localized string
        /// </summary>
        /// <param name="str">string to localize</param>
        /// <param name="logLevel">Log level used when parsing log level dependent values such as Paths</param>
        /// <param name="cultureUI">CultureInfo to be used when looking up resources and parsing numbers</param>
        /// <param name="docType">may need to swap "peptide" for "molecule" depending on UI mode at time of event</param>
        /// <returns>localized string</returns>
        public static string ParseLogString(string str, LogLevel logLevel, CultureInfo cultureUI, SrmDocument.DOCUMENT_TYPE docType)
        {
            var result = new StringBuilder();

            var tokens = new Queue<ExpansionToken>(ExpansionToken.EnumerateTokens(str));
            if (tokens.Count == 0)
                return str;
       
            var token = tokens.Dequeue();

            // Append text before first token
            result.Append(str.Substring(0, token.StartIndex));

            result.Append(token.Parse(str, logLevel, cultureUI, docType));

            while (tokens.Count > 0)
            {
                var prevToken = token;
                token = tokens.Dequeue();

                // Append text between tokens
                var start = prevToken.StartIndex + prevToken.Length;
                result.Append(str.Substring(start, token.StartIndex - start));

                result.Append(token.Parse(str, logLevel, cultureUI, docType));
                
            }

            // Append text after last token
            result.Append(str.Substring(token.StartIndex + token.Length));
            return result.ToString();
        }

        public static string ParseLogString(string str, LogLevel logLevel, SrmDocument.DOCUMENT_TYPE docType)
        {
            return ParseLogString(str, logLevel, CultureInfo.CurrentUICulture, docType);
        }

        protected bool Equals(LogMessage other)
        {
            return Equals(MessageInfo, other.MessageInfo) && Level == other.Level &&
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
                var hashCode = (MessageInfo != null ? MessageInfo.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (int) Level;
                hashCode = (hashCode * 397) ^ Expanded.GetHashCode();
                return hashCode;
            }
        }

        #region Implementation of IXmlSerializable
        protected LogMessage()
        {
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        protected enum EL
        {
            en_expanded
        }

        public static LogMessage Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new LogMessage());
        }

        public virtual void WriteXml(XmlWriter writer)
        {
            MessageInfo.WriteXml(writer);

            // Write text, even if it does not contain expansion tokens
            writer.WriteElementString(EL.en_expanded, EnExpanded);
        }

        public virtual void ReadXml(XmlReader reader)
        {
            reader.ReadStartElement();

            MessageInfo = MessageInfo.ReadXml(reader);

            EnExpanded = reader.IsStartElement(EL.en_expanded)
                ? reader.ReadElementString()
                : null;

            reader.ReadEndElement();
        }
        #endregion
    }

    public class DetailLogMessage : LogMessage
    {
        public static DetailLogMessage FromLogMessage(LogMessage logMessage)
        {
            return new DetailLogMessage(logMessage.Level, logMessage.MessageInfo, string.Empty, logMessage.Expanded);
        }

        public DetailLogMessage(LogLevel level, MessageInfo info, string reason, bool expanded) : base(level, info, expanded)
        {
            Reason = reason;
        }

        public DetailLogMessage(LogLevel level, MessageType type, SrmDocument.DOCUMENT_TYPE docType, string reason, bool expanded, params object[] names) : base(level, type, docType, expanded, names)
        {
            Reason = reason;
        }

        public string Reason { get; private set; }

        public DetailLogMessage ChangeReason(string reason)
        {
            return ChangeProp(ImClone(this), im => im.Reason = reason);
        }

        public override byte[] GetBytesForHash(Encoding encoding, CultureInfo ci)
        {
            return base.GetBytesForHash(encoding, ci).Concat(encoding.GetBytes(Reason)).ToArray();
        }

        protected bool Equals(DetailLogMessage other)
        {
            return base.Equals(other) && Reason == other.Reason;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((DetailLogMessage)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode() * 397) ^ (Reason != null ? Reason.GetHashCode() : 0);
            }
        }

        #region Implementation of IXmlSerializable

        private enum EL2
        {
            reason
        }

        private DetailLogMessage()
        {

        }

        public new static DetailLogMessage Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new DetailLogMessage());
        }

        public override void WriteXml(XmlWriter writer)
        {
            MessageInfo.WriteXml(writer);

            if (!string.IsNullOrEmpty(Reason))
                writer.WriteElementString(EL2.reason, Reason);

            // Write text, even if it does not contain expansion tokens
            writer.WriteElementString(EL.en_expanded, EnExpanded);
        }

        public override void ReadXml(XmlReader reader)
        {
            reader.ReadStartElement();

            MessageInfo = MessageInfo.ReadXml(reader);

            Reason = reader.IsStartElement(EL2.reason)
                ? reader.ReadElementString()
                : string.Empty;

            EnExpanded = reader.IsStartElement(EL.en_expanded)
                ? reader.ReadElementString()
                : null;

            reader.ReadEndElement();
        }
        #endregion
    }
}
