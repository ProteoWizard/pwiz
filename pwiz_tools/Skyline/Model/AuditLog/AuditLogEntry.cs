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
using System.Security.Principal;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.AuditLog
{
    [XmlRoot(XML_ROOT)]
    public class AuditLogList : Immutable, IXmlSerializable
    {
        public const string XML_ROOT = "audit_log"; // Not L10N
        
        public AuditLogList(ImmutableList<AuditLogEntry> auditLogEntries)
        {
            AuditLogEntries = auditLogEntries;
        }

        public AuditLogList() : this(ImmutableList<AuditLogEntry>.EMPTY)
        {
        }

        public ImmutableList<AuditLogEntry> AuditLogEntries { get; private set; }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public static AuditLogList Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new AuditLogList());
        }

        public void ReadXml(XmlReader reader)
        {
            reader.ReadStartElement();
            var auditLogEntries = new List<AuditLogEntry>();

            while (reader.IsStartElement(AuditLogEntry.XML_ROOT))
                auditLogEntries.Add(reader.DeserializeElement<AuditLogEntry>());

            AuditLogEntries = ImmutableList.ValueOf(auditLogEntries);
            reader.ReadEndElement();
        }

        public void WriteXml(XmlWriter writer)
        {
            foreach (var entry in AuditLogEntries)
                writer.WriteElement(entry);
        }
    }

    [XmlRoot(XML_ROOT)]
    public class AuditLogEntry : Immutable, IXmlSerializable
    {
        public const string XML_ROOT = "audit_log_entry"; // Not L10N

        private ImmutableList<LogMessage> _allInfo;
        private Action<AuditLogEntry> _undoAction;

        private AuditLogEntry(DocumentFormat formatVersion, DateTime timeStamp, string reason, bool insertIntoUndoRedo = false, string extraInfo = null)
        {
            SkylineVersion = Install.Version;
            if (Install.Is64Bit)
                SkylineVersion += " (64-Bit)"; // Not L10N

            FormatVersion = formatVersion;
            TimeStamp = timeStamp;
            ExtraInfo = extraInfo;

            using (var identity = WindowsIdentity.GetCurrent())
            {
                // ReSharper disable once PossibleNullReferenceException
                User = identity.Name;
            }

            Reason = reason ?? string.Empty;
            InsertUndoRedoIntoAllInfo = insertIntoUndoRedo;
        }


        private static PropertyName RemoveTopmostParent(PropertyName name)
        {
            if (name == PropertyName.ROOT || name.Parent == PropertyName.ROOT)
                return name;

            if (name.Parent.Parent == PropertyName.ROOT)
                return PropertyName.ROOT.SubProperty(name);

            return RemoveTopmostParent(name.Parent).SubProperty(name);
        }

        public class MessageTypeNamesPair
        {
            public MessageTypeNamesPair(MessageType type, params string[] names)
            {
                Type = type;
                Names = names;
            }

            public LogMessage ToMessage(LogLevel logLevel)
            {
                return new LogMessage(logLevel, Type, string.Empty, false, Names);
            }

            public MessageType Type { get; private set; }
            public string[] Names { get; private set; }
        }

        // Functions to create audit log entries

        public static AuditLogEntry CreateEmptyEntry(DocumentFormat formatVersion,
            string extraText = null)
        {
            return new AuditLogEntry(formatVersion, DateTime.Now, string.Empty, true, extraText);
        }

        public static AuditLogEntry CreateSingleMessageEntry(DocumentFormat formatVersion,
            MessageTypeNamesPair typeNamesPair, string extraInfo = null)
        {
            var result = new AuditLogEntry(formatVersion, DateTime.Now, string.Empty, true, extraInfo);

            result.UndoRedo = typeNamesPair.ToMessage(LogLevel.undo_redo);
            result.Summary = typeNamesPair.ToMessage(LogLevel.summary);
            result.AllInfo = new[] { typeNamesPair.ToMessage(LogLevel.all_info) };

            return result;
        }

        public static AuditLogEntry CreateSettingsChangeEntry(DocumentFormat formatVersion, DiffTree tree, string extraInfo = null)
        {
            var result = new AuditLogEntry(formatVersion, tree.TimeStamp, string.Empty, true, extraInfo);

            var nodeNamePair = tree.Root.FindFirstMultiChildParent(tree, PropertyName.ROOT, true, false);
            // Remove "Settings" from property name if possible
            if (nodeNamePair.Name != null && nodeNamePair.Name.Parent != PropertyName.ROOT)
            {
                var name = nodeNamePair.Name;
                while (name.Parent.Parent != PropertyName.ROOT)
                    name = name.Parent;

                if (name.Parent.Name == "{0:Settings}") // Not L10N
                {
                    name = RemoveTopmostParent(nodeNamePair.Name);
                    nodeNamePair = nodeNamePair.ChangeName(name);
                }
            }

            result.UndoRedo = nodeNamePair.ToMessage(LogLevel.undo_redo);
            result.Summary = tree.Root.FindFirstMultiChildParent(tree, PropertyName.ROOT, false, false)
                .ToMessage(LogLevel.summary);
            result.AllInfo = tree.Root.FindAllLeafNodes(tree, PropertyName.ROOT, true)
                .Select(n => n.ToMessage(LogLevel.all_info)).ToArray();
            
            return result;
        }

        public static AuditLogEntry CreateLogEnabledDisabledEntry(DocumentFormat formatVersion)
        {
            var result = new AuditLogEntry(formatVersion, DateTime.Now, string.Empty);

            var type = Settings.Default.AuditLogging ? MessageType.log_enabled : MessageType.log_disabled;

            result.UndoRedo = new LogMessage(LogLevel.undo_redo, type, string.Empty, false);
            result.Summary = new LogMessage(LogLevel.summary, type, string.Empty, false);
            result.AllInfo = new List<LogMessage> { new LogMessage(LogLevel.all_info, type, string.Empty, false) };

            return result;
        }

        public static AuditLogEntry CreateCountEntry(MessageType type, DocumentFormat formatVersion,
            int undoRedoCount, int allInfoCount)
        {
            if (type != MessageType.log_unlogged_changes && type != MessageType.log_cleared)
                throw new ArgumentException();

            // ReSharper disable once UseObjectOrCollectionInitializer
            var result = new AuditLogEntry(formatVersion, DateTime.Now, string.Empty);

            result.UndoRedo = new LogMessage(LogLevel.undo_redo, type, string.Empty, false,
                undoRedoCount.ToString());
            result.Summary = new LogMessage(LogLevel.summary, type, string.Empty, false,
                undoRedoCount.ToString());

            result.AllInfo = new List<LogMessage>
            {
                new LogMessage(LogLevel.all_info, type, string.Empty, false,
                    allInfoCount.ToString())
            };

            result.CountEntryType = type;

            return result;
        }

        public string SkylineVersion { get; private set; }
        public DocumentFormat FormatVersion { get; private set; }
        public DateTime TimeStamp { get; private set; }
        public string User { get; private set; }
        public string Reason { get; private set; }
        public string ExtraInfo { get; private set; }
        public LogMessage UndoRedo { get; private set; }
        public LogMessage Summary { get; private set; }

        public Action UndoAction
        {
            get
            {
                if (_undoAction == null)
                    return null;

                return () => _undoAction(this);
            }
        }

        public IList<LogMessage> AllInfo
        {
            get { return _allInfo; }
            private set { _allInfo = ImmutableList.ValueOf(InsertUndoRedoIntoAllInfo ? new[] { UndoRedo }.Concat(value) : value); }
        }

        public bool InsertUndoRedoIntoAllInfo { get; private set; }

        public bool HasSingleAllInfoRow
        {
            get { return _allInfo.Count == (InsertUndoRedoIntoAllInfo ? 2 : 1); }
        }

        public MessageType? CountEntryType { get; private set; }

        // Property change functions

        public AuditLogEntry ChangeReason(string reason)
        {
            return ChangeProp(ImClone(this), im => im.Reason = reason);
        }

        public AuditLogEntry ChangeUndoRedo(MessageTypeNamesPair undoRedo)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.UndoRedo = undoRedo.ToMessage(LogLevel.undo_redo);
                // Since the all info list might contain the undo redo message,
                // changing it requires updating the all info
                if (InsertUndoRedoIntoAllInfo)
                    im.AllInfo = im._allInfo.Skip(1).ToList();
            });
        }

        public AuditLogEntry ChangeSummary(MessageTypeNamesPair summary)
        {
            return ChangeProp(ImClone(this), im => im.Summary = summary.ToMessage(LogLevel.summary));
        }

        public AuditLogEntry ChangeAllInfo(IList<MessageTypeNamesPair> allInfo)
        {
            return ChangeProp(ImClone(this),
                im => im.AllInfo = allInfo.Select(info => info.ToMessage(LogLevel.all_info)).ToList());
        }

        public AuditLogEntry ChangeAllInfo(IList<LogMessage> allInfo)
        {
            return ChangeProp(ImClone(this), im => im.AllInfo = allInfo);
        }

        public AuditLogEntry ChangeUndoAction(Action<AuditLogEntry> undoAction)
        {
            return ChangeProp(ImClone(this), im => im._undoAction = undoAction);
        }

        public void AddToDocument(SrmDocument document, Action<Func<SrmDocument, SrmDocument>> modifyDocument)
        {
            if (Settings.Default.AuditLogging || CountEntryType == MessageType.log_cleared)
            {
                modifyDocument(d => d.ChangeAuditLog(
                    ImmutableList.ValueOf(d.AuditLog.AuditLogEntries.Concat(new[] { this }))));
            }
            else
            {
                UpdateCountLogEntry(document, modifyDocument, 1, AllInfo.Count, MessageType.log_unlogged_changes);
            }
        }

        public static AuditLogEntry UpdateCountLogEntry(SrmDocument document,
            Action<Func<SrmDocument, SrmDocument>> modifyDocument, int undoRedoCount, int allInfoCount,
            MessageType type, bool addToDoc = true)
        {
            var logEntries = new List<AuditLogEntry>(document.AuditLog.AuditLogEntries);
            var countEntry = logEntries.FirstOrDefault(e => e.CountEntryType == type);

            if (countEntry != null)
            {
                var countEntries = logEntries.Where(e =>
                    e.CountEntryType == MessageType.log_cleared ||
                    e.CountEntryType == MessageType.log_unlogged_changes).ToArray();

                undoRedoCount += countEntries.Sum(e => int.Parse(e.UndoRedo.Names[0])) - countEntries.Length;
                allInfoCount += countEntries.Sum(e => int.Parse(e.AllInfo[0].Names[0])) - countEntries.Length;
                logEntries.Remove(countEntry);
            }

            var newCountEntry = CreateCountEntry(type, document.FormatVersion, undoRedoCount, allInfoCount);
            if (addToDoc)
            {
                logEntries.Add(newCountEntry);

                modifyDocument(d => d.ChangeAuditLog(ImmutableList<AuditLogEntry>.ValueOf(logEntries)));
            }

            return newCountEntry;
        }

        public static AuditLogEntry SettingsLogFunction(SrmDocumentPair documentPair)
        {
            var property = RootProperty.Create(typeof(SrmSettings), "Settings"); // Not L10N
            var tree = Reflector<SrmSettings>.BuildDiffTree(documentPair, property, documentPair.OldDoc.Settings, documentPair.NewDoc.Settings, DateTime.Now);
            return tree != null && tree.Root != null ? CreateSettingsChangeEntry(documentPair.OldDoc.FormatVersion, tree) : null;
        }

        #region Implementation of IXmlSerializable

        private AuditLogEntry()
        {
            
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        private enum ATTR
        {
            format_version,
            time_stamp,
            user,
            count_type,
            insert_undo_redo
        }

        private enum EL
        {
            message,
            reason,
            extra_info
        }

        public static AuditLogEntry Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new AuditLogEntry());
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttribute(ATTR.format_version, FormatVersion.AsDouble());
            writer.WriteAttribute(ATTR.time_stamp, TimeStamp.ToUniversalTime().ToString(CultureInfo.InvariantCulture));
            writer.WriteAttribute(ATTR.user, User);

            writer.WriteAttribute(ATTR.insert_undo_redo, InsertUndoRedoIntoAllInfo);

            if (CountEntryType.HasValue)
                writer.WriteAttribute(ATTR.count_type, CountEntryType);

            if (!string.IsNullOrEmpty(Reason))
                writer.WriteElementString(EL.reason, Reason);

            if (!string.IsNullOrEmpty(ExtraInfo))
                writer.WriteElementString(EL.extra_info, ExtraInfo);
            
            writer.WriteElement(EL.message, UndoRedo);
            writer.WriteElement(EL.message, Summary);

            var startIndex = InsertUndoRedoIntoAllInfo ? 1 : 0;
            for (var i = startIndex; i < _allInfo.Count; ++i)
                writer.WriteElement(EL.message, _allInfo[i]);
        }

        public void ReadXml(XmlReader reader)
        {
            FormatVersion = new DocumentFormat(reader.GetDoubleAttribute(ATTR.format_version));
            var time = DateTime.Parse(reader.GetAttribute(ATTR.time_stamp), CultureInfo.InvariantCulture);
            TimeStamp = DateTime.SpecifyKind(time, DateTimeKind.Utc).ToLocalTime();
            User = reader.GetAttribute(ATTR.user);

            InsertUndoRedoIntoAllInfo = reader.GetBoolAttribute(ATTR.insert_undo_redo);

            var countType = reader.GetAttribute(ATTR.count_type);
            if (countType == null)
                CountEntryType = null;
            else
                CountEntryType = (MessageType) Enum.Parse(typeof(MessageType), countType);

            reader.ReadStartElement();

            Reason = reader.IsStartElement(EL.reason) ? reader.ReadElementString() : string.Empty;
            ExtraInfo = reader.IsStartElement(EL.extra_info) ? reader.ReadElementString() : string.Empty;

            UndoRedo = reader.DeserializeElement<LogMessage>();
            Summary = reader.DeserializeElement<LogMessage>();

            var list = new List<LogMessage>();
            while (reader.IsStartElement(EL.message))
                list.Add(reader.DeserializeElement<LogMessage>());

            AllInfo = list;

            reader.ReadEndElement();
        }
        #endregion
    }
}