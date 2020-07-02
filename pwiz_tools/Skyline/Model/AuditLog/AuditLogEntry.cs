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
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.AuditLog
{

    public class AuditLogException : Exception
    {
        public AuditLogException(string pMessage) : base(pMessage)
        {
        }
        public AuditLogException(string pMessage, Exception pInner) : base(pMessage, pInner)
        {
        }

        /// <summary>
        /// Checks if the given exception was thrown due to problems with audit log processing
        /// </summary>
        /// <param name="ex">Exception to check</param>
        /// <returns>True if this or any nested exception is of <see cref="AuditLogException"> AuditLogException </see> type.</returns>
        public static bool IsAuditLogInvolved(Exception ex)
        {
            do      //traverse the nested exception chain to look for AuditLogException
            {
                if (ex is AuditLogException) return true;
                ex = ex.InnerException;
            } while (ex != null);

            return false;
        }

        /// <summary>
        /// Extracts messages from all the nested exceptions and concatenates them into a single string.
        /// </summary>
        /// <param name="ex">Exception to extract the message from.</param>
        /// <returns>String with all the nested exception messages separated by new lines and exception separator --> </returns>
        public static string GetMultiLevelMessage(Exception ex)
        {
            var msgStrings = new List<string>();

            do
            {
                //Make the error dialog a bit more user friendly by showing the full chain of nested exceptions in the message
                //so they don't have to dig through the stack traces to find the root cause.
                if (msgStrings.Count > 0)
                    msgStrings.Add(Resources.ExceptionDialog_Caused_by_____);
                msgStrings.Add(ex.Message);
                ex = ex.InnerException;

            } while (ex != null);

            return TextUtil.LineSeparate(msgStrings);
        }
    }

    [XmlRoot(XML_ROOT)]
    public class AuditLogList : Immutable, IXmlSerializable
    {
        public const string XML_ROOT = "audit_log";
        public const string EXT = ".skyl";

        public const string DOCUMENT_ROOT = "audit_log_root";

        public static bool IgnoreTestChecks { get; set; }

        public AuditLogList(AuditLogEntry entries)
        {
            AuditLogEntries = entries;
            RootHash = new AuditLogHash()
                .ChangeActualHash(CalculateRootHash());
            FormatVersion = DocumentFormat.CURRENT;
        }

        public AuditLogList() : this(AuditLogEntry.ROOT)
        {
        }

        public AuditLogEntry AuditLogEntries { get; private set; }
        public AuditLogHash RootHash { get; private set; }

        public DocumentFormat? FormatVersion { get; private set; }

        private byte[] CalculateRootHash()
        {
            if (AuditLogEntries.Count == 0)
                return null;

            // Calculate root hash
            using (var sha1 = new SHA1CryptoServiceProvider())
            {
                var blockHash = new BlockHash(sha1);
                AuditLogEntries.Enumerate().ForEach(e => blockHash.ProcessBytes(Encoding.UTF8.GetBytes(e.Hash.ActualHash.HashString)));
                blockHash.FinalizeHashBytes();
                return blockHash.HashBytes;
            }
        }


        public static SrmDocument ToggleAuditLogging(SrmDocument doc, bool enable)
        {
            var newDoc = doc.ChangeSettings(
                doc.Settings.ChangeDataSettings(doc.Settings.DataSettings.ChangeAuditLogging(enable)));

            return newDoc;
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public static AuditLogList Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new AuditLogList());
        }

        private AuditLogEntry ReadEntries(XmlReader reader)
        {
            var entries = new List<AuditLogEntry>();

            while (reader.IsStartElement(AuditLogEntry.XML_ROOT))
            {
                entries.Add(reader.DeserializeElement<AuditLogEntry>());
            }

            AuditLogEntry result = AuditLogEntry.ROOT;
            for (var i = entries.Count; i-- > 0;)
            {
                result = entries[i].ChangeParent(result);
            }
            return result;
        }

        public void ReadXml(XmlReader reader)
        {
            var isEmpty = reader.IsEmptyElement;
            reader.ReadStartElement();

            AuditLogEntries = ReadEntries(reader);

            if (!isEmpty)
                reader.ReadEndElement();

            Validate();
        }

        public void Validate()
        {
            if (ReferenceEquals(AuditLogEntries, AuditLogEntry.ROOT))
                return;

            var time = DateTime.MaxValue;
            var logIndex = int.MinValue;

            //make sure the entries timestamp in the log is always increasing 
            foreach (var entry in AuditLogEntries.Enumerate())
            {
                if (entry.TimeStampUTC > time || entry.LogIndex <= logIndex)
                {
                    string msg = string.Format(
                        AuditLogStrings.AuditLogList_Validate_Audit_log_is_corrupted__Audit_log_entry_time_stamps_and_indices_should_be_decreasing,
                        entry.LogIndex, entry.TimeStampUTC, logIndex, time);
                    throw new AuditLogException(msg);
                }
                time = entry.TimeStampUTC;
                logIndex = entry.LogIndex;
            }
        }

        public void WriteXml(XmlWriter writer)
        {
            AuditLogEntries.Enumerate().ForEach(writer.WriteElement);
        }

        private enum EL
        {
            document_hash,
            root_hash
        }

        private enum ATTR
        {
            format_version
        }

        public void WriteToFile(string fileName, string documentHash)
        {
            using (var fileSaver = new FileSaver(fileName))
            {
                using (var writer = new XmlTextWriter(fileSaver.SafeName, Encoding.UTF8) { Formatting = Formatting.Indented })
                {
                    WriteToXmlWriter(writer, documentHash);
                }

                fileSaver.Commit();
            }
        }

        private void WriteToXmlWriter(XmlWriter writer, string documentHash = null)
        {
            writer.WriteStartDocument();
            writer.WriteStartElement(DOCUMENT_ROOT);
            writer.WriteAttributeString(ATTR.format_version, DocumentFormat.CURRENT.ToString());
            if (!string.IsNullOrEmpty(documentHash))
                writer.WriteElementString(EL.document_hash, documentHash);
            if (RootHash != null)
                writer.WriteElementString(EL.root_hash, RootHash.ActualHash);
            writer.WriteElement(this);
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        public static bool ReadFromFile(string fileName, out string loggedSkylineDocumentHash, out AuditLogList result)
        {
            try
            {
                using (var reader = new XmlTextReader(fileName))
                {
                    return ReadFromXmlTextReader(reader, out loggedSkylineDocumentHash, out result);
                }
            }
            catch(Exception ex)
            {

                throw new AuditLogException(
                    string.Format(
                        AuditLogStrings.AuditLogList_ReadFromFile_An_exception_occured_while_reading_the_audit_log,
                        fileName),
                    ex);
            }
        }

        public static bool ReadFromXmlTextReader(XmlTextReader reader, out string loggedSkylineDocumentHash, out AuditLogList result)
        {
            reader.ReadToFollowing(DOCUMENT_ROOT);
            DocumentFormat? docFormat = null;
            if (reader.HasAttributes)
            {
                var docFormatString = reader.GetAttribute(ATTR.format_version);
                if (double.TryParse(docFormatString, NumberStyles.Float, CultureInfo.InvariantCulture, out var format))
                    docFormat = new DocumentFormat(format);
            }

            reader.ReadStartElement();

            loggedSkylineDocumentHash = reader.ReadElementString(EL.document_hash.ToString());
            if (docFormat == null && !string.IsNullOrEmpty(loggedSkylineDocumentHash))
            {
                // If the docFormat is null, this is an old audit log and the document hash was formatted using byte.ToString(@"X2") instead of Base64
                try
                {
                    var bytes = new List<byte>();
                    var s = loggedSkylineDocumentHash;
                    foreach (var bbHex in Enumerable.Range(0, loggedSkylineDocumentHash.Length / 2)
                        .Select(i => s.Substring(i * 2, 2)))
                    {
                        bytes.Add(Convert.ToByte(bbHex, 16));
                    }
                    loggedSkylineDocumentHash = Convert.ToBase64String(bytes.ToArray());
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch
                {
                }
            }

            string rootHash = null;
            if (reader.IsStartElement(EL.root_hash.ToString()))
                rootHash = reader.ReadElementString(EL.root_hash.ToString());

            result = reader.DeserializeElement<AuditLogList>();
            result.FormatVersion = docFormat;
            result.RootHash = new AuditLogHash()
                .ChangeActualHash(result.CalculateRootHash())
                .ChangeSkylHash(Hash.FromBase64(rootHash));

            // yell if audit log entry hash does not match.
            // If the docFormat is null, this is an old audit log and there won't be any entry hashes.
            // We can't just always ignore non-existent hashes, otherwise people could just delete the hash elements
            // and get Skyline to successfully load the audit log
            var modifiedEntries =
                result.AuditLogEntries.Enumerate().Where(entry => !entry.Hash.SkylAndActualHashesEqual()).ToArray();

            if (docFormat != null && (!result.RootHash.SkylAndActualHashesEqual() || modifiedEntries.Length > 0))
            {
                throw new AuditLogException(
                    AuditLogStrings.AuditLogList_ReadFromFile_The_following_audit_log_entries_were_modified +
                    TextUtil.LineSeparate(modifiedEntries.Select(entry => entry.UndoRedo.ToString())));
            }

            reader.ReadEndElement();
            return true;
        }
    }

    public class DocumentNodeCounts : AuditLogOperationSettings<DocumentNodeCounts>
    {
        public DocumentNodeCounts(SrmDocument doc)
        {
            IsPeptideOnly = doc.IsEmptyOrHasPeptides; // Treat empty as proteomic per tradition
            MoleculeGroupCount = doc.GetCount((int) SrmDocument.Level.MoleculeGroups);
            MoleculeCount = doc.GetCount((int)SrmDocument.Level.Molecules);
            PrecursorCount = doc.GetCount((int)SrmDocument.Level.TransitionGroups);
            TransitionCount = doc.GetCount((int)SrmDocument.Level.Transitions);
        }

        public bool IsPeptideOnly { get; private set; }

        [Track(customLocalizer:typeof(MoleculeGroupCountLocalizer))]
        public int MoleculeGroupCount { get; private set; }
        [Track(customLocalizer: typeof(MoleculeCountLocalizer))]
        public int MoleculeCount { get; private set; }
        [Track]
        public int PrecursorCount { get; private set; }
        [Track]
        public int TransitionCount { get; private set; }

        private class PeptideSmallMoleculeLocalizer : CustomPropertyLocalizer
        {
            private string _propertyName;

            protected PeptideSmallMoleculeLocalizer(string propertyName) : base(PropertyPath.Parse(nameof(IsPeptideOnly)), true)
            {
                _propertyName = propertyName;
            }

            private string _smallMoleculeName
            {
                get { return _propertyName + @"_smallmol"; }
            }

            protected override string Localize(ObjectPair<object> objectPair)
            {
                return (bool)objectPair.NewObject ? _propertyName : _smallMoleculeName;
            }

            public override string[] PossibleResourceNames
            {
                get { return new[] { _propertyName, _smallMoleculeName }; }
            }
        }

        private class MoleculeGroupCountLocalizer : PeptideSmallMoleculeLocalizer
        {
            public MoleculeGroupCountLocalizer() : base(nameof(MoleculeGroupCount)) { }
        }

        private class MoleculeCountLocalizer : PeptideSmallMoleculeLocalizer
        {
            public MoleculeCountLocalizer() : base(nameof(MoleculeCount)) { }
        }
    }

    public class AuditLogEntryCreator
    {
        public AuditLogEntryCreator(Func<SrmDocumentPair, AuditLogEntry> create)
        {
            Create = create;
        }

        public Func<SrmDocumentPair, AuditLogEntry> Create { get; private set; }
    }

    public class AuditLogEntryCreatorList
    {
        public AuditLogEntryCreatorList()
        {
            EntryCreators = new List<AuditLogEntryCreator>();
        }

        public void Add(Func<SrmDocumentPair, AuditLogEntry> fn)
        {
            Add(new AuditLogEntryCreator(fn));
        }

        public void Add(params MessageInfo[] allInfoMessages)
        {
            Add(docPair => AuditLogEntry.CreateEmptyEntry().ChangeAllInfo(allInfoMessages));
        }

        public void Add(AuditLogEntryCreator entryCreator)
        {
            EntryCreators.Add(entryCreator);
        }

        public IEnumerable<AuditLogEntry> CreateEntries(SrmDocumentPair docPair)
        {
            foreach (var entryCreator in EntryCreators)
            {
                var entry = entryCreator.Create(docPair);
                if (entry != null)
                    yield return entry;
            }
        }

        public List<AuditLogEntryCreator> EntryCreators { get; private set; }
    }

    public class LogException : Exception
    {
        public LogException(Exception innerException, string oldUndoRedoMessage = null) : base(null, innerException)
        {
            OldUndoRedoMessage = oldUndoRedoMessage;
        }

        public override string Message
        {
            get
            {
                if (OldUndoRedoMessage == null)
                {
                    return AuditLogStrings.LogException_Message_An_error_occured_while_creating_a_log_entry__The_document_was_still_successfully_modified;
                }
                else
                {
                    return string.Format(
                        AuditLogStrings.LogException_Message_An_error_occured_while_creating_a_log_entry__Action___0___was_still_successfull,
                        OldUndoRedoMessage);
                }
            }
        }

        public string OldUndoRedoMessage { get; private set; }
    }

    public class MessageArgs
    {
        public MessageArgs(params object[] args)
        {
            Args = args;
        }

        public static MessageArgs Create(params object[] args)
        {
            return new MessageArgs(args);
        }

        public static MessageArgs DefaultSingular(object obj)
        {
            return Create(obj);
        }

        public object[] Args { get; set; }
    }

    [XmlRoot(XML_ROOT)]
    public class AuditLogEntry : Immutable, IXmlSerializable, IValidating
    {
        public const string XML_ROOT = "audit_log_entry";

        private ImmutableList<DetailLogMessage> _allInfo;
        private Action<AuditLogEntry> _undoAction;
        private static int _logIndexCounter;

        public static string _user = WindowsIdentity.GetCurrent().Name; // This won't change during app's run, so cache it

        public static string _skylineVersion = // This won't change during app's run, so cache it
            (string.IsNullOrEmpty(Install.Version)
               ? string.Format(@"Developer build, document format {0}",DocumentFormat.CURRENT) // CONSIDER: can we be more informative?
               : Install.Version)
               + (Install.Is64Bit ? @" (64-Bit)" : string.Empty);

        public static AuditLogEntry ROOT = new AuditLogEntry { Count = 0, LogIndex = int.MaxValue };

        public bool IsRoot
        {
            get { return ReferenceEquals(this, ROOT); }
        }

        public static AuditLogEntry SKIP = new AuditLogEntry { Count = -1, LogIndex = int.MinValue };

        public static AuditLogEntry SkipChange(SrmDocumentPair pair)
        {
            return SKIP;
        }

        public bool IsSkip
        {
            get { return ReferenceEquals(this, SKIP); }
        }

        private AuditLogEntry(DateTime timeStampUTC, string reason, SrmDocument.DOCUMENT_TYPE docType,
            string extraInfo = null) : this()
        {
            LogIndex = Interlocked.Increment(ref _logIndexCounter);

            SkylineVersion = _skylineVersion;

            DocumentType = docType == SrmDocument.DOCUMENT_TYPE.none ? SrmDocument.DOCUMENT_TYPE.proteomic : docType;

            Assume.IsTrue(timeStampUTC.Kind == DateTimeKind.Utc); // We only deal in UTC
            TimeStampUTC = timeStampUTC;
            TimeZoneOffset = TimeZoneInfo.Local.GetUtcOffset(TimeStampUTC); // UTC offset e.g. -8 for Seattle whn not on DST
            
            ExtraInfo = extraInfo;

            using (var identity = WindowsIdentity.GetCurrent())
            {
                // ReSharper disable once PossibleNullReferenceException
                User = identity.Name;
            }

            Reason = reason ?? string.Empty;
        }

        /// Parent node, topmost node will be <see cref="ROOT" />
        public AuditLogEntry Parent { get; private set; }
        // The number of nodes in this linked list, including this node itself
        public int Count { get; private set; }

        public string SkylineVersion { get; private set; } // Skyline version at the time of original event creation
        public DateTime TimeStampUTC { get; private set; } // UTC time of creation
        public TimeSpan TimeZoneOffset { get; private set; } // UTC offset at time of creation e.g. -8 for Seattle when not on DST, -7 when DST  
        public SrmDocument.DOCUMENT_TYPE DocumentType { get; private set; } // Document type and/or UI mode at time of creation

        public static Dictionary<SrmDocument.DOCUMENT_TYPE, string> DocumentTypeSerializationValues =
            new Dictionary<SrmDocument.DOCUMENT_TYPE, string> // Must agree with audit logging XSD
            {
                {SrmDocument.DOCUMENT_TYPE.proteomic, @"p"},
                {SrmDocument.DOCUMENT_TYPE.small_molecules, @"m"},
                {SrmDocument.DOCUMENT_TYPE.mixed, @"x"}
            };
        public string User { get; private set; }
        public string Reason { get; private set; }
        public string ExtraInfo { get; private set; }
        private string _enExtraInfo;
        public string EnExtraInfo
        {
            get
            {
                return _enExtraInfo ?? (_enExtraInfo = LogMessage
                           .ParseLogString(ExtraInfo, LogLevel.all_info, CultureInfo.InvariantCulture, DocumentType)?
                           .EscapeNonPrintableChars());
            }
            private set { _enExtraInfo = value; }
        }
        public LogMessage UndoRedo { get; private set; }
        public LogMessage Summary { get; private set; }


        // The hash should not be used before the audit log entry is fully constructed,
        // since when using the private setters for changing properties the hash does not
        // get recalculated (unlike when using ChangeProp).
        private AuditLogHash _hash;
        public AuditLogHash Hash
        {
            get
            {
                if (_hash == null)
                    return _hash = new AuditLogHash(this, null);
                if (_hash.ActualHash == null)
                    return _hash = new AuditLogHash(this, _hash.SkylHash);
                return _hash;
            }
            private set { _hash = value; }
        }

        public bool InsertUndoRedoIntoAllInfo
        {
            get { return UndoRedo != null; }
        }

        public MessageType? CountEntryType { get; private set; }

        public Action UndoAction
        {
            get
            {
                if (_undoAction == null)
                    return null;

                return () => _undoAction(this);
            }
        }

        private IEnumerable<DetailLogMessage> _mergeAllInfo
        {
            get
            {
                return !InsertUndoRedoIntoAllInfo
                    ? _allInfo
                    : (_allInfo.Count == 1
                        ? new[] {(DetailLogMessage) _allInfo[0].ChangeLevel(LogLevel.all_info)}
                        : _allInfoNoUndoRedo);
            }
        }

        private IEnumerable<DetailLogMessage> _allInfoNoUndoRedo
        {
            get { return InsertUndoRedoIntoAllInfo ? _allInfo.Skip(1) : _allInfo; }
        }

        public IList<DetailLogMessage> AllInfo
        {
            get { return _allInfo; }
            private set
            {
                _allInfo = ImmutableList.ValueOf(InsertUndoRedoIntoAllInfo
                    ? ImmutableList.Singleton(DetailLogMessage.FromLogMessage(UndoRedo)).Concat(value)
                    : value);
            }
        }

        public bool HasSingleAllInfoRow
        {
            get { return _allInfo != null && _allInfo.Count == (InsertUndoRedoIntoAllInfo ? 2 : 1); }
        }

        public int LogIndex { get; private set; }

        public IEnumerable<AuditLogEntry> Enumerate()
        {
            var entry = this;
            while (!entry.IsRoot)
            {
                yield return entry;
                entry = entry.Parent;
            }
        }

        #region Property change functions

        public AuditLogEntry ChangeParent(AuditLogEntry parent)
        {
            // We don't use ChangeProp here because these changing these values won't alter the hash,
            // so we don't need to call Validate() which just clears the hash on the chance that it might change
            var result = ImClone(this);
            result.Parent = parent;
            result.Count = parent.Count + 1;
            return result;
        }

        public AuditLogEntry ChangeReason(string reason)
        {
            return ChangeProp(ImClone(this), im => im.Reason = reason);
        }

        public AuditLogEntry ChangeUndoRedo(MessageInfo undoRedo)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.UndoRedo = undoRedo.ToMessage(LogLevel.undo_redo);
                // Since the all info list might contain the undo redo message,
                // changing it requires updating the all info
                if (InsertUndoRedoIntoAllInfo)
                    im.AllInfo = ImmutableList.ValueOf(im._allInfoNoUndoRedo);
            });
        }

        public AuditLogEntry ChangeSummary(MessageInfo summary)
        {
            return ChangeSummary(summary.ToMessage(LogLevel.summary));
        }

        public AuditLogEntry ChangeSummary(LogMessage summary)
        {
            return ChangeProp(ImClone(this), im => im.Summary = summary);
        }

        public AuditLogEntry ChangeAllInfo(IList<MessageInfo> allInfo)
        {
            return ChangeAllInfo(allInfo
                .Select(info => DetailLogMessage.FromLogMessage(info.ToMessage(LogLevel.all_info))).ToList());
        }

        public AuditLogEntry ChangeAllInfo(IList<DetailLogMessage> allInfo)
        {
            return ChangeProp(ImClone(this), im => im.AllInfo = allInfo);
        }

        public AuditLogEntry AppendAllInfo(IEnumerable<DetailLogMessage> allInfo)
        {
            return ChangeProp(ImClone(this), im => im.AllInfo = _allInfoNoUndoRedo.Concat(allInfo).ToList());
        }

        public AuditLogEntry AppendAllInfo(IEnumerable<MessageInfo> allInfo)
        {
            return ChangeProp(ImClone(this),
                im => im.AllInfo = _allInfoNoUndoRedo
                    .Concat(allInfo.Select(msgInfo =>
                        DetailLogMessage.FromLogMessage(msgInfo.ToMessage(LogLevel.all_info)))).ToList());
        }

        public AuditLogEntry ClearAllInfo()
        {
            return ChangeAllInfo(new DetailLogMessage[0]);
        }

        public AuditLogEntry ChangeUndoAction(Action<AuditLogEntry> undoAction)
        {
            return ChangeProp(ImClone(this), im => im._undoAction = undoAction);
        }

        public AuditLogEntry ChangeExtraInfo(string extraInfo)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im._enExtraInfo = null;
                im.ExtraInfo = extraInfo;
            });
        }

        #endregion

        #region Functions to create log entries

        private static MessageInfo GetLogClearedInfo(int clearedCount)
        {
            return new MessageInfo(clearedCount > 1 ? MessageType.log_cleared : MessageType.log_cleared_single, SrmDocument.DOCUMENT_TYPE.none,
                clearedCount);
        }

        private static MessageInfo GetUnloggedMessages(int unloggedCount)
        {
            return new MessageInfo(
                unloggedCount == 1 ? MessageType.log_unlogged_change : MessageType.log_unlogged_changes, SrmDocument.DOCUMENT_TYPE.none,
                unloggedCount);
        }

        /*
        private AuditLogEntry CreateUnloggedEntry(SrmDocument doc, out bool replace)
        {
            var countEntry = doc.AuditLog.AuditLogEntries;

            replace = countEntry != null && countEntry.CountEntryType == MessageType.log_unlogged_changes;
            if (!replace)
            {
                countEntry = CreateSimpleEntry(MessageType.log_unlogged_changes, 0)
                    // This entry needs a non-undoredo all info message to work properly
                    .ChangeAllInfo(ImmutableList.Singleton(new MessageInfo(MessageType.log_unlogged_changes, 0)));
                countEntry.CountEntryType = MessageType.log_unlogged_changes;
            }

            return countEntry?.ChangeUndoRedo(GetUnloggedMessages(int.Parse(countEntry.UndoRedo.Names[0]) + 1))
                .ChangeSummary(GetUnloggedMessages(int.Parse(countEntry.Summary.Names[0]) + 1))
                .ChangeAllInfo(ImmutableList.Singleton(GetUnloggedMessages(
                    int.Parse(countEntry._allInfoNoUndoRedo.First().Names[0]) + _allInfoNoUndoRedo.Count())));
        }
        */

        public static AuditLogEntry GetAuditLoggingStartExistingDocEntry(SrmDocument doc, SrmDocument.DOCUMENT_TYPE defaultDocumentType)
        {
            // Don't want to have these entries in tests (except for the AuditLogSaving test which actually tests this type of entry)
            if (Program.FunctionalTest && !AuditLogList.IgnoreTestChecks)
                return null;

            var defaultDoc = new SrmDocument(SrmSettingsList.GetDefault());
            var docPair = SrmDocumentPair.Create(defaultDoc, doc, defaultDocumentType);

            var changeFromDefaultSettings = SettingsLogFunction(docPair);
            var initialNodeCounts = doc.Children.Count > 0 ? new DocumentNodeCounts(doc).EntryCreator.Create(docPair) : null;

            var entry = CreateSimpleEntry(MessageType.start_log_existing_doc, 
                    doc.DocumentType == SrmDocument.DOCUMENT_TYPE.none ? defaultDocumentType : doc.DocumentType)
                .Merge(initialNodeCounts).Merge(changeFromDefaultSettings);

            if (changeFromDefaultSettings != null || initialNodeCounts != null)
                return entry;

            return null;
        }

        public static AuditLogEntry CreateUndocumentedChangeEntry()
        {
            return CreateSimpleEntry(MessageType.undocumented_change, SrmDocument.DOCUMENT_TYPE.none);
        }

        /// <summary>
        /// Creates a log entry that indicated the log was cleared and how many changes were cleared
        /// </summary>
        public static AuditLogEntry ClearLogEntry(SrmDocument doc)
        {
            var entries = doc.AuditLog.AuditLogEntries;
            var undoRedoCount = entries.Count;
            var allInfoCount = 0;

            foreach (var e in entries.Enumerate())
            {
                if (e.CountEntryType == MessageType.log_cleared ||
                    e.CountEntryType == MessageType.log_unlogged_changes)
                {
                    undoRedoCount += int.Parse(e.UndoRedo.Names[0]) - 1;
                    allInfoCount += int.Parse(e._allInfoNoUndoRedo.First().Names[0]) - 1;
                }

                allInfoCount += e._allInfoNoUndoRedo.Count();
            }

            var entry = CreateEmptyEntry();
            entry.CountEntryType = MessageType.log_cleared;

            var msgInfoUndoRedo = GetLogClearedInfo(undoRedoCount);

            return entry.ChangeUndoRedo(msgInfoUndoRedo)
                .ChangeSummary(msgInfoUndoRedo)
                .ChangeAllInfo(ImmutableList.Singleton(GetLogClearedInfo(allInfoCount)));
        }

        /// <summary>
        /// Creates an empty entry that can be useful when making entries
        /// that will get merged into another entry
        /// </summary>
        public static AuditLogEntry CreateEmptyEntry()
        {
            return new AuditLogEntry(DateTime.UtcNow, string.Empty, SrmDocument.DOCUMENT_TYPE.none);
        }

        /// <summary>
        /// Creates a simple entry only containing one message in each category with the given type and names
        /// extra info
        /// </summary>
        public static AuditLogEntry CreateSimpleEntry(MessageType type, SrmDocument.DOCUMENT_TYPE docType, params object[] args)
        {
            return CreateSingleMessageEntry(new MessageInfo(type, docType, args));
        }

        /// <summary>
        /// Creates a simple entry only containing one message in each category with the given type and names
        /// extra info
        /// </summary>
        public static AuditLogEntry CreateTestOnlyEntry(DateTime timestampUTC, SrmDocument.DOCUMENT_TYPE docType, params object[] args)
        {
            var info = new MessageInfo(MessageType.test_only, docType, args);
            var result = new AuditLogEntry(timestampUTC, string.Empty, docType)
            {
                UndoRedo = info.ToMessage(LogLevel.undo_redo),
                Summary = info.ToMessage(LogLevel.summary),
                AllInfo = new DetailLogMessage[0]
            };

            return result;
        }

        /// <summary>
        /// Creates an entry that depends on whether there are 1 or multiple elements
        /// in a collection.
        /// </summary>
        /// <param name="singular">Message to show if there's 1 element in the collection. Only element gets passed as argument to the message</param>
        /// <param name="plural">Message to show if there are multiple elements. The count gets passed to the message</param>
        /// <param name="docType">Needed for potential "peptide"->"molecule" translation in human readable form</param>
        /// <param name="items">Items to consider</param>
        /// <param name="singularArgsFunc">Converts the element to MessageArgs that get passed to the singular message</param>
        /// <param name="pluralArgs">Args to be passed to plural. If null, count is passed as single arg</param>
        public static AuditLogEntry CreateCountChangeEntry<T>(MessageType singular,
            MessageType plural, SrmDocument.DOCUMENT_TYPE docType, ICollection<T> items, Func<T, MessageArgs> singularArgsFunc, MessageArgs pluralArgs)
        {
            var singularArgs = items.Count == 1 ? singularArgsFunc(items.FirstOrDefault()) : null;
            return CreateCountChangeEntry(singular, plural, docType, items.Count, singularArgs,
                pluralArgs);
        }

        /// <summary>
        /// Creates an entry that depends on whether there are 1 or multiple elements
        /// in a collection.
        /// </summary>
        /// <param name="singular">Message to show if there's 1 element in the collection. Only element gets passed as argument to the message</param>
        /// <param name="plural">Message to show if there are multiple elements. The count gets passed to the message</param>
        /// <param name="docType">Needed for potential "peptide"->"molecule" translation in human readable form</param>
        /// <param name="items">Items to consider</param>
        /// <param name="count">Number of elements in IEnumerable. If null, all items are enumerated</param>
        /// <param name="singularArgsFunc">Converts the element to MessageArgs that get passed to the singular message</param>
        /// <param name="pluralArgs">Args to be passed to plural. If null, count is passed as single arg</param>
        public static AuditLogEntry CreateCountChangeEntry<T>(MessageType singular,
            MessageType plural, SrmDocument.DOCUMENT_TYPE docType, IEnumerable<T> items, int? count, Func<T, MessageArgs> singularArgsFunc,
            MessageArgs pluralArgs)
        {
            if (!count.HasValue)
            {
                var collection = items as ICollection<T> ?? items.ToArray();
                return CreateCountChangeEntry(singular, plural, docType, collection, singularArgsFunc, pluralArgs);
            }

            var singularArgs = count.Value == 1 ? singularArgsFunc(items.FirstOrDefault()) : null;
            return CreateCountChangeEntry(singular, plural, docType, count.Value, singularArgs, pluralArgs);
        }

        // Overload for common case
        public static AuditLogEntry CreateCountChangeEntry(MessageType singular,
            MessageType plural, SrmDocument.DOCUMENT_TYPE docType, ICollection<string> items)
        {
            return CreateCountChangeEntry(singular, plural, docType, items, MessageArgs.DefaultSingular, null);
        }

        // Overload for common case
        public static AuditLogEntry CreateCountChangeEntry(MessageType singular,
            MessageType plural, SrmDocument.DOCUMENT_TYPE docType, IEnumerable<string> items, int? count)
        {
            if (!count.HasValue)
            {
                var collection = items as ICollection<string> ?? items.ToArray();
                return CreateCountChangeEntry(singular, plural, docType, collection, MessageArgs.DefaultSingular, null);
            }

            return CreateCountChangeEntry(singular, plural, docType, items, count, MessageArgs.DefaultSingular, null);
        }

        private static AuditLogEntry CreateCountChangeEntry(MessageType singular,
            MessageType plural, SrmDocument.DOCUMENT_TYPE docType, int count, MessageArgs singularArgs, MessageArgs pluralArgs)
        {
            switch (count)
            {
                case 1:
                    return CreateSimpleEntry(singular, docType, singularArgs.Args);
                default:
                    return CreateSimpleEntry(plural, docType,
                        pluralArgs != null ? pluralArgs.Args : MessageArgs.Create(count).Args);
            }
        }

        /// <summary>
        /// Creates a simple entry only containing one message in each category with the given type and names and
        /// extra info
        /// </summary>
        public static AuditLogEntry CreateSingleMessageEntry(MessageInfo info, string extraInfo = null)
        {
            var result = new AuditLogEntry(DateTime.UtcNow, string.Empty, info.DocumentType, extraInfo)
            {
                UndoRedo = info.ToMessage(LogLevel.undo_redo),
                Summary = info.ToMessage(LogLevel.summary),
                AllInfo = new DetailLogMessage[0]//new[] { info.ToMessage(LogLevel.all_info) }
            };

            return result;
        }

        // Creates a new PropertyName with top most node removed
        private static PropertyName RemoveTopmostParent(PropertyName name)
        {
            if (name == PropertyName.ROOT || name.Parent == PropertyName.ROOT)
                return name;

            if (name.Parent.Parent == PropertyName.ROOT)
                return PropertyName.ROOT.SubProperty(name);

            return RemoveTopmostParent(name.Parent).SubProperty(name);
        }

        /// <summary>
        /// Creates a log entry representing the changes in the diff tree
        /// </summary>
        /// <param name="tree">Tree that should be logged</param>
        /// <param name="docType">Used for potentially translating "peptide" to "molecule" in human readable version of log</param>
        /// <param name="extraInfo">Text that should be displayed when clicking the magnifying glass in the audit log form</param>
        /// <returns></returns>
        public static AuditLogEntry CreateSettingsChangeEntry(DiffTree tree, SrmDocument.DOCUMENT_TYPE docType, string extraInfo = null)
        {
            if (tree?.Root == null)
                return null;

            var result = new AuditLogEntry(tree.TimeStamp, string.Empty, docType, extraInfo);

            var nodeNamePair = tree.Root.FindFirstMultiChildParent(tree, PropertyName.ROOT, true, false);
            // Remove "Settings" from property name if possible
            if (nodeNamePair.Name != null && nodeNamePair.Name.Parent != PropertyName.ROOT)
            {
                var name = nodeNamePair.Name;
                while (name.Parent.Parent != PropertyName.ROOT)
                    name = name.Parent;

                if (name.Parent.Name == @"{0:Settings}")
                {
                    name = RemoveTopmostParent(nodeNamePair.Name);
                    nodeNamePair = nodeNamePair.ChangeName(name);
                }
            }

            result.UndoRedo = nodeNamePair.ToMessage(LogLevel.undo_redo);
            result.Summary = tree.Root.FindFirstMultiChildParent(tree, PropertyName.ROOT, false, false)
                .ToMessage(LogLevel.summary);
            result.AllInfo = tree.Root.FindAllLeafNodes(tree, PropertyName.ROOT, true)
                .Select(n => DetailLogMessage.FromLogMessage(n.ToMessage(LogLevel.all_info))).ToArray();
            
            return result;
        }

        private static bool IsTransitionDiff(Type type)
        {
            if (type == typeof(TransitionDocNode) || type == typeof(TransitionGroupDocNode))
                return true;

            if (type.GenericTypeArguments.Length == 1 &&
                typeof(IEnumerable<>).MakeGenericType(type.GenericTypeArguments[0]).IsAssignableFrom(type))
                type = type.GenericTypeArguments[0];
            else
                return false;
            return IsTransitionDiff(type);
        }

        public static AuditLogEntry DiffDocNodes(MessageType action, SrmDocumentPair documentPair,
            bool ignoreTransitions, params object[] actionParameters)
        {
            var property = RootProperty.Create(typeof(Targets));
            var objInfo = new ObjectInfo<object>(documentPair.OldDoc.Targets, documentPair.NewDoc.Targets,
                documentPair.OldDoc, documentPair.NewDoc, documentPair.OldDoc, documentPair.NewDoc);

            var docType = documentPair.NewDoc.DocumentType == SrmDocument.DOCUMENT_TYPE.none
                ? documentPair.OldDocumentType
                : documentPair.NewDocumentType;

            var diffTree = DiffTree.FromEnumerator(
                Reflector<Targets>.EnumerateDiffNodes(objInfo, property, docType, false,
                    ignoreTransitions
                        ? (Func<DiffNode, bool>) (node => !IsTransitionDiff(node.Property.PropertyType))
                        : null),
                DateTime.UtcNow);

            if (diffTree.Root != null)
            {
                var message = new MessageInfo(action, docType, actionParameters);
                var entry = CreateSettingsChangeEntry(diffTree, docType)
                    .ChangeUndoRedo(message);
                return entry;
            }

            return null;
        }

        public static AuditLogEntry DiffDocNodes(MessageType action, SrmDocumentPair documentPair, params object[] actionParameters)
        {
            return DiffDocNodes(action, documentPair, false, actionParameters);
        }

        /// <summary>
        /// Creates a log entry indicating that logging was enabled or disabled
        /// </summary>
        public static AuditLogEntry CreateLogEnabledDisabledEntry(SrmDocument document)
        {
            var result = new AuditLogEntry(DateTime.UtcNow, string.Empty, document.DocumentType);

            var type = document.Settings.DataSettings.AuditLogging ? MessageType.log_enabled : MessageType.log_disabled;
            var docType = document.DocumentType;
            result.UndoRedo = new LogMessage(LogLevel.undo_redo, type, docType, false);
            result.Summary = new LogMessage(LogLevel.summary, type, docType, false);
            result.AllInfo = new List<DetailLogMessage> { new DetailLogMessage(LogLevel.all_info, type, docType, string.Empty, false) };

            return result;
        }

        public static AuditLogEntry CreateExceptionEntry(LogException ex)
        {
            // ReSharper disable PossibleNullReferenceException
            if (ex.OldUndoRedoMessage == null)
            {
                return CreateSingleMessageEntry(new MessageInfo(MessageType.log_error, SrmDocument.DOCUMENT_TYPE.none, ex.InnerException.GetType().Name), ex.InnerException.StackTrace);
            }
            else
            {
                var entry = CreateSingleMessageEntry(new MessageInfo(MessageType.empty_single_arg, SrmDocument.DOCUMENT_TYPE.none, ex.OldUndoRedoMessage), ex.InnerException.StackTrace);
                return entry.AppendAllInfo(ImmutableList.Singleton(new MessageInfo(MessageType.log_error_old_msg, SrmDocument.DOCUMENT_TYPE.none,
                    ex.InnerException.GetType().Name, ex.OldUndoRedoMessage)));
            }
            // ReSharper enable PossibleNullReferenceException
        }

        #endregion Functions to create log entries

        /// <summary>
        /// Merges two audit log entries, by adding the other entries
        /// all info messages and extra info to the current entry
        /// </summary>
        /// <param name="other">Entry to merge into the current one</param>
        /// <param name="append">true if all info and extra info should be appended, if false they are replaced</param>
        /// <returns>A new, merged entry</returns>
        public AuditLogEntry Merge(AuditLogEntry other, bool append = true)
        {
            if (other == null)
                return this;

            var entry = append
                ? AppendAllInfo(other._mergeAllInfo)
                : ChangeAllInfo(other._mergeAllInfo.ToList());

            if (!string.IsNullOrEmpty(other.ExtraInfo))
            {
                entry = entry.ChangeExtraInfo(string.IsNullOrEmpty(ExtraInfo) || !append
                    ? other.ExtraInfo
                    : TextUtil.LineSeparate(ExtraInfo, other.ExtraInfo));
            }
            return entry;
        }

        /// <summary>
        /// Merges the entries created by the given creator list into the current entry
        /// </summary>
        /// <param name="docPair">Documents used to construct new entries</param>
        /// <param name="creatorList">Entries to be constructed</param>
        /// <param name="append">see <see cref="Merge(AuditLogEntry,bool)"/></param>
        /// <returns>A new, merged entry</returns>
        public AuditLogEntry Merge(SrmDocumentPair docPair, AuditLogEntryCreatorList creatorList, bool append = true)
        {
            return creatorList.EntryCreators.Aggregate(this, (e, c) => e.Merge(c.Create(docPair), append));
        }

        /// <summary>
        /// Updates the document with the given AuditLogEntry. If the entry is non-null and logging is enabled,
        /// it is added to the document. If audit logging changed from being disabled to enabled and the document is different
        /// from the default document(no children, default settings) a "start from existing document" entry is added. If audit logging
        /// changes from being enabled to disabled, the log is cleared.
        /// </summary>
        /// <param name="entry">The entry to add</param>
        /// <param name="docPair">Document pair containing the new document the entry should get added to</param>
        /// <returns>A new document with this entry added</returns>
        public static SrmDocument UpdateDocument(AuditLogEntry entry, SrmDocumentPair docPair)
        {
            var newDoc = docPair.NewDoc;
            /*if (Settings.Default.AuditLogging || CountEntryType == MessageType.log_cleared)
            {
                newDoc = newDoc.ChangeAuditLog(ChangeParent(docPair.NewDoc.AuditLog.AuditLogEntries));
            }
            else
            {
                var entry = CreateUnloggedEntry(document, out var replace);

                // This is the only property we have to copy over, since we don't care about the content of the log message
                // but still want the ability to undo unlogged entries. We only change the undo action for the first
                // unlogged message entry, otherwise clicking the undo button in the grid would undo the unlogged changes one-by-one
                // instead of in a single "batch undo." TODO: Is this how it should be?
                // (This one-by-one behavior can still be achieved by using the undo redo buffer)
                if (!replace)
                    entry = entry.ChangeUndoAction(_undoAction);

                var oldEntries = document.AuditLog.AuditLogEntries;
                var newEntries = replace
                    ? entry.ChangeParent(oldEntries?.Parent)
                    : entry.ChangeParent(oldEntries);

                newDoc = document.ChangeAuditLog(newEntries);
            }*/

            var oldLogging = docPair.OldDoc.Settings.DataSettings.AuditLogging;
            var newLogging = docPair.NewDoc.Settings.DataSettings.AuditLogging;

            if (oldLogging && !newLogging)
                return newDoc.ChangeAuditLog(ROOT);
            else if (!oldLogging && newLogging)
            {
                var startEntry = GetAuditLoggingStartExistingDocEntry(newDoc, docPair.OldDocumentType);
                if (startEntry != null && entry != null)
                    startEntry = startEntry.ChangeParent(entry);
                entry = startEntry ?? entry;
            }

            if (newLogging)
                newDoc = entry?.AppendEntryToDocument(newDoc) ?? newDoc;

            return newDoc;
        }

        public SrmDocument AppendEntryToDocument(SrmDocument doc)
        {
            return doc.ChangeAuditLog(ChangeParent(doc.AuditLog.AuditLogEntries));
        }

        public static bool ConvertPathsToFileNames { get; set; }

        /// <summary>
        /// Compares the settings objects of the given documents and creates an entry
        /// for the differences
        /// </summary>
        /// <param name="documentPair">The pair of documents to compare</param>
        /// <returns>A log entry containing the changes</returns>
        public static AuditLogEntry SettingsLogFunction(SrmDocumentPair documentPair)
        {
            var property = RootProperty.Create(typeof(SrmSettings), @"Settings");
            var objInfo = new ObjectInfo<object>(documentPair.OldDoc.Settings, documentPair.NewDoc.Settings,
                documentPair.OldDoc, documentPair.NewDoc, documentPair.OldDoc, documentPair.NewDoc);

            var tree = DiffTree.FromEnumerator(Reflector<SrmSettings>.EnumerateDiffNodes(objInfo, property, documentPair.OldDocumentType, false));
            return tree.Root != null ? CreateSettingsChangeEntry(tree, documentPair.OldDocumentType) : null;
        }

        public static PropertyName GetNodeName(SrmDocument doc, DocNode docNode)
        {
            DocNode nextNode = null;
            if (docNode is TransitionDocNode)
            {
                nextNode = doc.MoleculeTransitionGroups.FirstOrDefault(group =>
                    group.Transitions.Any(t => ReferenceEquals(t.Id, docNode.Id)));
            }
            else if (docNode is TransitionGroupDocNode)
            {
                nextNode = doc.Molecules.FirstOrDefault(group =>
                    group.TransitionGroups.Any(t => ReferenceEquals(t.Id, docNode.Id)));
            }
            else if (docNode is PeptideDocNode)
            {
                nextNode = doc.MoleculeGroups.FirstOrDefault(group =>
                    group.Molecules.Any(m => ReferenceEquals(m.Id, docNode.Id)));
            }

            // TODO: add other interface to these doc nodes?
            var auditLogObj = docNode as IAuditLogObject;
            
            if (auditLogObj == null)
                return null;

            var text = auditLogObj.AuditLogText;

            return nextNode == null
                ? PropertyName.ROOT.SubProperty(text)
                : GetNodeName(doc, nextNode).SubProperty(text);
        }

        #region Implementation of IXmlSerializable

        private AuditLogEntry()
        {
            Parent = ROOT;
            Count = 1;
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        private enum ATTR
        {
            format_version,
            skyline_version,
            time_stamp,
            user,
            count_type,
            insert_undo_redo,
            mode
        }

        private enum EL
        {
            message,
            undo_redo,
            summary,
            all_info,
            reason,
            extra_info,
            en_extra_info,
            hash,
        }

        public static AuditLogEntry Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new AuditLogEntry());
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttribute(ATTR.skyline_version, SkylineVersion);
            writer.WriteAttribute(ATTR.time_stamp, FormatSerializationString(TimeStampUTC, TimeZoneOffset)); // e.g 2008-10-01T17:04:32-8 or  2008-10-01T17:04:32+2 or  2008-10-01T17:04:32Z);
            writer.WriteAttribute(ATTR.user, User);
            if (DocumentType != SrmDocument.DOCUMENT_TYPE.none && DocumentType != SrmDocument.DOCUMENT_TYPE.proteomic)
            {
                writer.WriteAttribute(ATTR.mode, DocumentTypeSerializationValues[DocumentType]);
            }

            if (CountEntryType.HasValue)
                writer.WriteAttribute(ATTR.count_type, CountEntryType);

            if (!string.IsNullOrEmpty(Reason))
                writer.WriteElementString(EL.reason, Reason);

            if (!string.IsNullOrEmpty(ExtraInfo))
                writer.WriteElementString(EL.extra_info, ExtraInfo.EscapeNonPrintableChars());

            writer.WriteElement(EL.undo_redo, UndoRedo);
            writer.WriteElement(EL.summary, Summary);

            foreach (var allInfo in _allInfoNoUndoRedo)
                writer.WriteElement(EL.all_info, allInfo);

            if (!string.IsNullOrEmpty(ExtraInfo) && LogMessage.ExpansionToken.EnumerateTokens(ExtraInfo).Any())
            {
                EnExtraInfo = null;
                writer.WriteElementString(EL.en_extra_info, EnExtraInfo);
            }

            writer.WriteElementString(EL.hash, Hash.ActualHash);
        }

        public void ReadXml(XmlReader reader)
        {
            LogIndex = Interlocked.Increment(ref _logIndexCounter);
            var skylineVersion = reader.GetAttribute(ATTR.skyline_version); // If this is null, it's a sign that this is an older 4.2 log without hashes
            SkylineVersion = skylineVersion ?? reader.GetAttribute(ATTR.format_version) ?? string.Empty; // Skyline version at time of original event creation (4.2 wrote format_version here, which amounted to the same thing)
            TimeStampUTC = ParseSerializedTimeStamp(reader.GetAttribute(ATTR.time_stamp), out var timeZoneOffset);
            TimeZoneOffset = timeZoneOffset;
            User = reader.GetAttribute(ATTR.user);

            var mode = reader.GetAttribute(ATTR.mode);
            DocumentType = string.IsNullOrEmpty(mode) ? SrmDocument.DOCUMENT_TYPE.none : DocumentTypeSerializationValues.FirstOrDefault(x => Equals(x.Value, mode)).Key;

            var countType = reader.GetAttribute(ATTR.count_type);
            if (countType == null)
                CountEntryType = null;
            else
                CountEntryType = (MessageType) Enum.Parse(typeof(MessageType), countType);

            reader.ReadStartElement();

            Reason = reader.IsStartElement(EL.reason) ? reader.ReadElementString() : string.Empty;
            ExtraInfo = reader.IsStartElement(EL.extra_info) ? reader.ReadElementString().UnescapeNonPrintableChars() : string.Empty;

            EL allInfoEnum;
            if (reader.IsStartElement(EL.undo_redo))
            {
                UndoRedo = reader.DeserializeElement<LogMessage>(EL.undo_redo).ChangeLevel(LogLevel.undo_redo).ChangeDocumentType(DocumentType);
                Summary = reader.DeserializeElement<LogMessage>(EL.summary).ChangeLevel(LogLevel.summary).ChangeDocumentType(DocumentType);
                allInfoEnum = EL.all_info;
            }
            else // Backward compatibility
            {
                UndoRedo = reader.DeserializeElement<LogMessage>(EL.message).ChangeLevel(LogLevel.undo_redo).ChangeDocumentType(DocumentType);
                Summary = reader.DeserializeElement<LogMessage>(EL.message).ChangeLevel(LogLevel.summary).ChangeDocumentType(DocumentType);
                allInfoEnum = EL.message;
            }

            var list = new List<DetailLogMessage>();
            while (reader.IsStartElement(allInfoEnum))
                list.Add((DetailLogMessage) reader.DeserializeElement<DetailLogMessage>(allInfoEnum).ChangeLevel(LogLevel.all_info).ChangeDocumentType(DocumentType));

            AllInfo = list;

            // Think about how and if we want to store these english strings
            EnExtraInfo = reader.IsStartElement(EL.en_extra_info)
                ? reader.ReadElementString(EL.en_extra_info.ToString())
                : null;

            var hash = reader.IsStartElement(EL.hash)
                ? reader.ReadElementString(EL.hash.ToString())
                : null;

            Hash = new AuditLogHash(this, hash == null? null : AuditLog.Hash.FromBase64(hash));

            if (hash == null && reader.GetAttribute(ATTR.format_version) != null)
            {
                // This was an older format that didn't save hashes, set it now
                Hash = Hash.ChangeSkylHash(Hash.ActualHash);
            }

            if (!Hash.SkylAndActualHashesEqual())
            {
                // Reset all english strings so that they get recalculated when
                // accessed the next time
                EnExtraInfo = null;
                UndoRedo = UndoRedo.ResetEnExpanded();
                Summary = Summary.ResetEnExpanded();
                AllInfo = _allInfoNoUndoRedo.Select(l => (DetailLogMessage) l.ResetEnExpanded()).ToList();
            }

            reader.ReadEndElement();
        }


        //
        // Read and write ISO / XML xsd:dateTime standard timestamps
        //
        public static string FormatSerializationString(DateTime timeUTC, TimeSpan timezoneOffset)
        {
            var localTime = timeUTC + timezoneOffset;
            var tzShift = timezoneOffset.TotalHours; // Decimal hours eg 8.5 or -0.5 etc
            return localTime.ToString(@"s", DateTimeFormatInfo.InvariantInfo) +
                   (tzShift == 0
                       ? @"Z"
                       : (tzShift < 0 ? @"-" : @"+") + timezoneOffset.ToString(@"hh\:mm"));
        }

        public static DateTime ParseSerializedTimeStamp(string timeStampSerializationString, out TimeSpan timezoneoffset)
        {
            var timezoneIndicatorPosition = timeStampSerializationString.LastIndexOfAny(new[] { 'Z', '+', '-' }); // Look for timezone info 2008-10-01T17:04:32-8 or  2008-10-01T17:04:32+2 or  2008-10-01T17:04:32Z 
            DateTime result;
            if (timezoneIndicatorPosition < 0)
            {
                // Pre-4.21 we logged bare UTC with no timezone info
                result = DateTime.Parse(timeStampSerializationString, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();
                timezoneoffset = TimeSpan.Zero;
            }
            else
            {
                // But now we log local time with UTC offset
                result = DateTime.Parse(timeStampSerializationString.Substring(0, timezoneIndicatorPosition), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();
                if (timeStampSerializationString[timezoneIndicatorPosition] == 'Z') // Z means GMT, no offset
                {
                    timezoneoffset = TimeSpan.Zero;
                }
                else
                {
                    var offset = timeStampSerializationString.Substring(timezoneIndicatorPosition + 1);
                    // Want to parse timespan as something like hh:mm:ss but we save hh or maybe hh:mm
                    timezoneoffset = TimeSpan.Parse(@"00:" + offset + (offset.Contains(':') ? @":00" : @":00:00")); 
                    if (timeStampSerializationString[timezoneIndicatorPosition] == '-')
                    {
                        timezoneoffset = -timezoneoffset;
                    }
                    result -= timezoneoffset; // We log local time, shift it to UTC using logged tzOffset e.g. Seattle offset is -8, so add 8 to get to GMT
                }
            }

            return result;
        }

        #endregion

        public byte[] GetAuditLogHash()
        {
            if (User == null || UndoRedo == null || Summary == null || _allInfoNoUndoRedo == null)
                return null;

            using (var sha1 = new SHA1CryptoServiceProvider())
            {
                var enc = Encoding.UTF8;
                var encodedBytes = new List<byte[]>(7+_allInfoNoUndoRedo.Count());
                encodedBytes.Add(enc.GetBytes(User));
                if (DocumentType != SrmDocument.DOCUMENT_TYPE.none && DocumentType != SrmDocument.DOCUMENT_TYPE.proteomic)
                    encodedBytes.Add(BitConverter.GetBytes((char)DocumentType));
                if (!string.IsNullOrEmpty(EnExtraInfo))
                    encodedBytes.Add(enc.GetBytes(EnExtraInfo));
                encodedBytes.Add(UndoRedo.GetBytesForHash(enc, CultureInfo.InvariantCulture));
                encodedBytes.Add(Summary.GetBytesForHash(enc, CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(Reason))
                    encodedBytes.Add(enc.GetBytes(Reason));
                _allInfoNoUndoRedo.ForEach(l => encodedBytes.Add(l.GetBytesForHash(enc, CultureInfo.InvariantCulture)));
                encodedBytes.Add(enc.GetBytes(SkylineVersion));
                encodedBytes.Add(GetTimeStampBytesForHash());
                encodedBytes.Add(enc.GetBytes(TimeZoneOffset.TotalHours.ToString(CultureInfo.InvariantCulture)));

                // Avoid heap thrash performance issue by carefully allocating hash buffer size
                var blockHash = new BlockHash(sha1, encodedBytes.Sum(b => b.Length)); 
                encodedBytes.ForEach(b => blockHash.ProcessBytes(b));

                return blockHash.FinalizeHashBytes();
            }
        }

        // For hash creation
        private byte[] GetTimeStampBytesForHash()
        {
            var bytes = BitConverter.GetBytes(TimeStampUTC.ToFileTime() / TimeSpan.TicksPerSecond); // We only serialize to hour:min:sec precision, lose the ticks so we can roundtrip
            if (!BitConverter.IsLittleEndian)
                return bytes.Reverse().ToArray(); // For crossplatform stability
            return bytes;
        }

        public void Validate()
        {
            // Whenever the entry changes, we set the hash to null
            // so that the next time it gets used it gets recalculated
            Hash = Hash?.ChangeActualHash(null);
        }
    }


    public class AuditLogHash : Immutable, IEquatable<AuditLogHash>
    {
        public AuditLogHash()
        {
        }

        public AuditLogHash(AuditLogEntry entry, Hash skylHash)
        {
            ActualHash = entry.GetAuditLogHash();
            SkylHash = skylHash;
        }

        public Hash ActualHash { get; private set; }
        public Hash SkylHash { get; private set; }

        public AuditLogHash ChangeActualHash(Hash hash)
        {
            return ChangeProp(ImClone(this), im => im.ActualHash = hash);
        }

        public AuditLogHash ChangeSkylHash(Hash hash)
        {
            return ChangeProp(ImClone(this), im => im.SkylHash = hash);
        }

        public bool SkylAndActualHashesEqual()
        {
            if (string.IsNullOrEmpty(ActualHash?.HashString) && string.IsNullOrEmpty(SkylHash?.HashString))
                return true;

            if (string.IsNullOrEmpty(ActualHash?.HashString) || string.IsNullOrEmpty(SkylHash?.HashString))
                return false;

            return ActualHash.Equals(SkylHash);
        }

        public bool Equals(AuditLogHash other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(ActualHash, other.ActualHash) && Equals(SkylHash, other.SkylHash);
        }

    }

    public class Hash : IEquatable<Hash>
    {
        public Hash(byte[] hash)
        {
            HashString = BlockHash.SafeToBase64(hash);
        }

        public string HashString { get; } // Base64 representation of hashed input bytes

        public static implicit operator Hash(byte[] bytes)
        {
            return new Hash(bytes);
        }

        public static Hash FromBase64(string base64)
        {
            return new Hash(base64 != null ? Convert.FromBase64String(base64) : null);
        }



        public override string ToString()
        {
            return HashString;
        }

        // IEquatable members
        public bool Equals(Hash other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(HashString, other.HashString);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Hash) obj);
        }

        public override int GetHashCode()
        {
            return (HashString != null ? HashString.GetHashCode() : 0);
        }

        public static bool operator ==(Hash left, Hash right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Hash left, Hash right)
        {
            return !Equals(left, right);
        }
    }


    /// <summary>
    /// Base class for objects that represent settings for an operation
    /// that creates log messages.
    /// </summary>
    /// <typeparam name="T">Type of the derived settings type</typeparam>
    public class AuditLogOperationSettings<T> : Immutable where T : AuditLogOperationSettings<T>
    {
        /// <summary>
        /// Info used in constructing an entry from this settings object.
        /// Should be overriden, unless entry gets merged into another entry
        /// </summary>
        public virtual MessageInfo MessageInfo
        {
            get { return new MessageInfo(MessageType.none, SrmDocument.DOCUMENT_TYPE.none); }
        }
        
        /// <summary>
        /// Returns an object that can create an audit log entry from this settings object.
        /// This is useful when a form gets disposed, but the entry should be constructed
        /// later without accessing the disposed form.
        /// </summary>
        public AuditLogEntryCreator EntryCreator
        {
            get { return new AuditLogEntryCreator(ImClone(this).CreateEntry); }
        }

        protected virtual AuditLogEntry CreateBaseEntry()
        {
            return MessageInfo.Type == MessageType.none
                ? AuditLogEntry.CreateEmptyEntry().ChangeAllInfo(new DetailLogMessage[0])
                : AuditLogEntry.CreateSingleMessageEntry(MessageInfo);
        }

        protected virtual AuditLogEntry CreateEntry(SrmDocumentPair docPair)
        {
            var baseEntry = CreateBaseEntry();
            var rootProp = RootProperty.Create(typeof(T));

            var objectInfo = new ObjectInfo<object>()
                    .ChangeObjectPair(ObjectPair<object>.Create(null, this))
                    .ChangeRootObjectPair(docPair.ToObjectType());

            var diffTree =
                DiffTree.FromEnumerator(Reflector<T>.EnumerateDiffNodes(docPair.ToObjectType(), rootProp, docPair.OldDocumentType, (T)this), DateTime.UtcNow);
            if (diffTree.Root == null)
                return baseEntry;

            var settingsString = Reflector<T>.ToString(objectInfo.RootObjectPair, docPair.OldDocumentType, diffTree.Root,
                ToStringState.DEFAULT.ChangeFormatWhitespace(true));
            var entry = AuditLogEntry.CreateSettingsChangeEntry(diffTree, docPair.OldDocumentType, settingsString);
            return baseEntry.Merge(entry);
        }
    }
    
    /// <summary>
    /// An optional interface for forms that have a settings object representing
    /// them
    /// </summary>
    /// <typeparam name="T">Type of the settings object</typeparam>
    public interface IAuditLogModifier<out T> where T : AuditLogOperationSettings<T>
    {
        T FormSettings { get; }
    }     
}
