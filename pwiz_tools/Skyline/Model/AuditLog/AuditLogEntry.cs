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

    public class MessageTypeNamesPair
    {
        public MessageTypeNamesPair(MessageType type, params object[] names)
        {
            Type = type;
            Names = names.Select(obj => obj == null ? string.Empty : obj.ToString()).ToArray();
        }

        public LogMessage ToMessage(LogLevel logLevel)
        {
            return new LogMessage(logLevel, Type, String.Empty, false, Names);
        }

        public MessageType Type { get; private set; }
        public string[] Names { get; private set; }
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

        public IList<LogMessage> AllInfoMessages(SrmDocumentPair docPair)
        {
            return CreateEntries(docPair).SelectMany(entry => entry.AllInfoNoUndoRedo).ToList();
        }

        public List<AuditLogEntryCreator> EntryCreators { get; private set; }
    }

    [XmlRoot(XML_ROOT)]
    public class AuditLogEntry : Immutable, IXmlSerializable
    {
        public const string XML_ROOT = "audit_log_entry"; // Not L10N

        private ImmutableList<LogMessage> _allInfo;
        private Action<AuditLogEntry> _undoAction;

        private AuditLogEntry(SrmDocument document, DateTime timeStamp, string reason, bool insertIntoUndoRedo = false, string extraInfo = null)
        {
            SkylineVersion = Install.Version;
            if (Install.Is64Bit)
                SkylineVersion += " (64-Bit)"; // Not L10N

            FormatVersion = document.FormatVersion;
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

        // Functions to create audit log entries

        public static AuditLogEntry CreateEmptyEntry(SrmDocument document,
            string extraText = null)
        {
            return new AuditLogEntry(document, DateTime.Now, string.Empty, true, extraText);
        }

        public static AuditLogEntry CreateSingleMessageEntry(SrmDocument document,
            MessageTypeNamesPair typeNamesPair, string extraInfo = null)
        {
            var result = new AuditLogEntry(document, DateTime.Now, string.Empty, true, extraInfo);

            result.UndoRedo = typeNamesPair.ToMessage(LogLevel.undo_redo);
            result.Summary = typeNamesPair.ToMessage(LogLevel.summary);
            result.AllInfo = new[] { typeNamesPair.ToMessage(LogLevel.all_info) };

            return result;
        }

        public static AuditLogEntry CreateSimpleEntry(SrmDocument document, MessageType type, params object[] args)
        {
            return CreateSingleMessageEntry(document, new MessageTypeNamesPair(type, args));
        }

        public static AuditLogEntry CreateSettingsChangeEntry(SrmDocument document, DiffTree tree, string extraInfo = null)
        {
            var result = new AuditLogEntry(document, tree.TimeStamp, string.Empty, true, extraInfo);

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

        public static AuditLogEntry CreateLogEnabledDisabledEntry(SrmDocument document)
        {
            var result = new AuditLogEntry(document, DateTime.Now, string.Empty);

            var type = Settings.Default.AuditLogging ? MessageType.log_enabled : MessageType.log_disabled;

            result.UndoRedo = new LogMessage(LogLevel.undo_redo, type, string.Empty, false);
            result.Summary = new LogMessage(LogLevel.summary, type, string.Empty, false);
            result.AllInfo = new List<LogMessage> { new LogMessage(LogLevel.all_info, type, string.Empty, false) };

            return result;
        }

        public static AuditLogEntry CreateDialogLogEntry<T>(SrmDocumentPair docPair, MessageType type, T dialogSettings, params object[] args) where T : class
        {
            var rootProp = RootProperty.Create(typeof(T));

            var objectInfo =
                new ObjectInfo<object>()
                    .ChangeObjectPair(ObjectPair<object>.Create(null, dialogSettings))
                    .ChangeRootObjectPair(docPair.ToObjectType());

            var settings = Reflector.ToString(objectInfo, rootProp, true);

            var tree = Reflector<T>.BuildDiffTree(docPair.ToObjectType(),
                rootProp,
                dialogSettings);

            var message = new MessageTypeNamesPair(type, args);
            return CreateSettingsChangeEntry(docPair.OldDoc, tree, settings)
                .ChangeUndoRedo(message)
                .ChangeSummary(message);
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

        public IEnumerable<LogMessage> AllInfoNoUndoRedo
        {
            get { return InsertUndoRedoIntoAllInfo ? _allInfo.Skip(1) : _allInfo; }
        }

        public IList<LogMessage> AllInfo
        {
            get { return _allInfo; }
            private set { _allInfo = ImmutableList.ValueOf(InsertUndoRedoIntoAllInfo ? CollectionUtil.FromSingleItem(UndoRedo).Concat(value) : value); }
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
                    im.AllInfo = im.AllInfoNoUndoRedo.ToList();
            });
        }

        public AuditLogEntry ChangeSummary(MessageTypeNamesPair summary)
        {
            return ChangeProp(ImClone(this), im => im.Summary = summary.ToMessage(LogLevel.summary));
        }

        public AuditLogEntry ChangeSummary(LogMessage summary)
        {
            return ChangeProp(ImClone(this), im => im.Summary = summary);
        }

        public AuditLogEntry ChangeAllInfo(IList<MessageTypeNamesPair> allInfo)
        {
            return ChangeProp(ImClone(this),
                im => im.AllInfo = allInfo.Select(info => info.ToMessage(LogLevel.all_info)).ToList());
        }

        public AuditLogEntry AppendAllInfo(IEnumerable<LogMessage> allInfo)
        {
            return ChangeProp(ImClone(this), im => im.AllInfo = AllInfoNoUndoRedo.Concat(allInfo).ToList());
        }

        public AuditLogEntry ChangeAllInfo(IList<LogMessage> allInfo)
        {
            return ChangeProp(ImClone(this), im => im.AllInfo = allInfo);
        }

        public AuditLogEntry ChangeUndoAction(Action<AuditLogEntry> undoAction)
        {
            return ChangeProp(ImClone(this), im => im._undoAction = undoAction);
        }

        public AuditLogEntry ChangeExtraInfo(string extraInfo)
        {
            return ChangeProp(ImClone(this), im => im.ExtraInfo = extraInfo);
        }

        public void AddToDocument(SrmDocument document, Action<Func<SrmDocument, SrmDocument>> modifyDocument)
        {
            modifyDocument(d =>
            {
                if (true || CountEntryType == MessageType.log_cleared)
                {
                    return d.ChangeAuditLog(
                        ImmutableList.ValueOf(d.AuditLog.AuditLogEntries.Concat(new[] { this })));
                }
                else
                {
                    bool replace;
                    var entry = UnloggedEntry(document, out replace);

                    var oldEntries = d.AuditLog.AuditLogEntries;
                    var newEntries = replace
                        ? oldEntries.ReplaceAt(oldEntries.Count - 1, entry)
                        : oldEntries.Concat(CollectionUtil.FromSingleItem(entry));

                    return d.ChangeAuditLog(ImmutableList.ValueOf(newEntries));
                }
            });
        }

        public static AuditLogEntry ClearLogEntry(SrmDocument doc)
        {
            var entries = doc.AuditLog.AuditLogEntries;
            var countEntries = entries.Where(e =>
                e.CountEntryType == MessageType.log_cleared ||
                e.CountEntryType == MessageType.log_unlogged_changes).ToArray();

            var undoRedoCount = 0;
            var allInfoCount = 0;
            foreach (var countEntry in countEntries)
            {
                undoRedoCount += int.Parse(countEntry.UndoRedo.Names[0]);
                allInfoCount += int.Parse(countEntry.AllInfoNoUndoRedo.First().Names[0]);
            }

            undoRedoCount += entries.Count - countEntries.Length;
            allInfoCount += entries.Sum(e => e.AllInfoNoUndoRedo.Count()) - countEntries.Length;

            var entry = CreateSimpleEntry(doc, MessageType.log_cleared);
            entry.CountEntryType = MessageType.log_cleared;

            return entry.ChangeUndoRedo(new MessageTypeNamesPair(MessageType.log_cleared,
                    undoRedoCount))
                .ChangeSummary(new MessageTypeNamesPair(MessageType.log_cleared,
                    undoRedoCount))
                .ChangeAllInfo(CollectionUtil.FromSingleItem(new MessageTypeNamesPair(MessageType.log_cleared,
                    allInfoCount)));
        }

        public AuditLogEntry UnloggedEntry(SrmDocument doc, out bool replace)
        {
            var logEntries = new List<AuditLogEntry>(doc.AuditLog.AuditLogEntries);

            var countEntry = logEntries.LastOrDefault();

            replace = countEntry != null && countEntry.CountEntryType == MessageType.log_unlogged_changes;
            if (!replace)
            {
                countEntry = CreateSimpleEntry(doc, MessageType.log_unlogged_changes, 0);
                countEntry.CountEntryType = MessageType.log_unlogged_changes;
            }

            return countEntry.ChangeUndoRedo(new MessageTypeNamesPair(MessageType.log_unlogged_changes,
                    int.Parse(countEntry.UndoRedo.Names[0]) + 1))
                .ChangeSummary(new MessageTypeNamesPair(MessageType.log_unlogged_changes,
                    int.Parse(countEntry.Summary.Names[0]) + 1))
                .ChangeAllInfo(CollectionUtil.FromSingleItem(new MessageTypeNamesPair(MessageType.log_unlogged_changes,
                    int.Parse(countEntry.AllInfoNoUndoRedo.First().Names[0]) + AllInfoNoUndoRedo.Count())));
        }

        public static AuditLogEntry SettingsLogFunction(SrmDocumentPair documentPair)
        {
            var property = RootProperty.Create(typeof(SrmSettings), "Settings"); // Not L10N
            var objInfo = new ObjectInfo<object>(documentPair.OldDoc.Settings, documentPair.NewDoc.Settings,
                documentPair.OldDoc, documentPair.NewDoc, documentPair.OldDoc, documentPair.NewDoc);
            var tree = Reflector<SrmSettings>.BuildDiffTree(objInfo, property, DateTime.Now);
            return tree != null && tree.Root != null ? CreateSettingsChangeEntry(documentPair.OldDoc, tree) : null;
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

            var auditLogObj = docNode as IAuditLogObject; // TODO: add other interface to these doc nodes?
            if (auditLogObj == null)
                return null;

            var text = auditLogObj.AuditLogText;

            if (nextNode == null)
                return PropertyName.ROOT.SubProperty(text);

            return GetNodeName(doc, nextNode).SubProperty(text);
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

    public abstract class AuditLogDialog<T> : FormEx
    {
        public AuditLogEntry CreateEntry(SrmDocumentPair docPair)
        {
            var rootProp = RootProperty.Create(typeof(T));

            var objectInfo =
                new ObjectInfo<object>()
                    .ChangeObjectPair(ObjectPair<object>.Create(null, DialogSettings))
                    .ChangeRootObjectPair(docPair.ToObjectType());

            var settings = Reflector.ToString(objectInfo, rootProp, true);

            var tree = Reflector<T>.BuildDiffTree(docPair.ToObjectType(),
                rootProp,
                DialogSettings);

            return AuditLogEntry.CreateSettingsChangeEntry(docPair.OldDoc, tree, settings)
                .ChangeUndoRedo(MessageInfo)
                .ChangeSummary(MessageInfo);
        }

        public virtual MessageTypeNamesPair MessageInfo
        {
            // Often times only the all info of dialogs is used,
            // in which case the message type is not used anyways
            get { return new MessageTypeNamesPair(MessageType.none); }
        }

        public abstract T DialogSettings { get; }
    }
}