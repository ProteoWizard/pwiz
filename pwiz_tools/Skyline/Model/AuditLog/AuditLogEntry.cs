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
using System.Threading;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.AuditLog
{
    [XmlRoot(XML_ROOT)]
    public class AuditLogList : Immutable, IXmlSerializable
    {
        public const string XML_ROOT = "audit_log"; // Not L10N

        public static bool CanStoreAuditLog = Program.FunctionalTest;

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
            {
                var entry = reader.DeserializeElement<AuditLogEntry>();
                auditLogEntries.Add(entry);
            }
                
            AuditLogEntries = ImmutableList.ValueOf(auditLogEntries);
            reader.ReadEndElement();
        }

        public void Validate()
        {
            if (AuditLogEntries.Count == 0)
                return;

            var logIndex = AuditLogEntries[0].LogIndex;
            var time = AuditLogEntries[0].TimeStamp;

            foreach (var entry in AuditLogEntries.Skip(1))
            {
                Assume.IsTrue(entry.TimeStamp >= time && entry.LogIndex > logIndex,
                    AuditLogStrings.AuditLogList_Validate_Audit_log_is_corrupted__Audit_log_entry_time_stamps_and_indices_should_be_increasing);
            }
        }

        public void WriteXml(XmlWriter writer)
        {
            foreach (var entry in AuditLogEntries)
                writer.WriteElement(entry);
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
            Add(docPair => AuditLogEntry.CreateEmptyEntry(docPair.OldDoc).ChangeAllInfo(allInfoMessages));
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
        public LogException(Exception innerException) : base(null, innerException)
        {
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

        public string OldUndoRedoMessage { get; set; }
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
    public class AuditLogEntry : Immutable, IXmlSerializable
    {
        public const string XML_ROOT = "audit_log_entry"; // Not L10N

        private ImmutableList<LogMessage> _allInfo;
        private Action<AuditLogEntry> _undoAction;
        private static int _logIndexCounter;

        private AuditLogEntry(SrmDocument document, DateTime timeStamp, string reason, bool insertIntoUndoRedo = false, string extraInfo = null)
        {
            LogIndex = Interlocked.Increment(ref _logIndexCounter);

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

        public string SkylineVersion { get; private set; }
        public DocumentFormat FormatVersion { get; private set; }
        public DateTime TimeStamp { get; private set; }
        public string User { get; private set; }
        public string Reason { get; private set; }
        public string ExtraInfo { get; private set; }
        public LogMessage UndoRedo { get; private set; }
        public LogMessage Summary { get; private set; }
        public bool InsertUndoRedoIntoAllInfo { get; private set; }
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

        private IEnumerable<LogMessage> _mergeAllInfo
        {
            get
            {
                return !InsertUndoRedoIntoAllInfo
                    ? _allInfo
                    : (_allInfo.Count == 1 ? _allInfo : _allInfoNoUndoRedo);
            }
        }

        private IEnumerable<LogMessage> _allInfoNoUndoRedo
        {
            get { return InsertUndoRedoIntoAllInfo ? _allInfo.Skip(1) : _allInfo; }
        }

        public IList<LogMessage> AllInfo
        {
            get { return _allInfo; }
            private set
            {
                _allInfo = ImmutableList.ValueOf(InsertUndoRedoIntoAllInfo
                    ? ImmutableList.Singleton(UndoRedo).Concat(value)
                    : value);
            }
        }

        public bool HasSingleAllInfoRow
        {
            get { return _allInfo.Count == (InsertUndoRedoIntoAllInfo ? 2 : 1); }
        }

        public int LogIndex { get; private set; }

        #region Property change functions

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
                    im.AllInfo = im._allInfoNoUndoRedo.ToList();
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
            return ChangeAllInfo(allInfo.Select(info => info.ToMessage(LogLevel.all_info)).ToList());
        }

        public AuditLogEntry ChangeAllInfo(IList<LogMessage> allInfo)
        {
            return ChangeProp(ImClone(this), im => im.AllInfo = allInfo);
        }

        public AuditLogEntry AppendAllInfo(IEnumerable<LogMessage> allInfo)
        {
            return ChangeProp(ImClone(this), im => im.AllInfo = _allInfoNoUndoRedo.Concat(allInfo).ToList());
        }

        public AuditLogEntry AppendAllInfo(IEnumerable<MessageInfo> allInfo)
        {
            return ChangeProp(ImClone(this),
                im => im.AllInfo = _allInfoNoUndoRedo
                    .Concat(allInfo.Select(msgInfo => msgInfo.ToMessage(LogLevel.all_info))).ToList());
        }

        public AuditLogEntry ClearAllInfo()
        {
            return ChangeAllInfo(new LogMessage[0]);
        }

        public AuditLogEntry ChangeUndoAction(Action<AuditLogEntry> undoAction)
        {
            return ChangeProp(ImClone(this), im => im._undoAction = undoAction);
        }

        public AuditLogEntry ChangeExtraInfo(string extraInfo)
        {
            return ChangeProp(ImClone(this), im => im.ExtraInfo = extraInfo);
        }

        #endregion

        #region Functions to create log entries

        private static MessageInfo GetLogClearedInfo(int clearedCount)
        {
            return new MessageInfo(clearedCount > 1 ? MessageType.log_cleared : MessageType.log_cleared_single,
                clearedCount);
        }

        private static MessageInfo GetUnloggedMessages(int unloggedCount)
        {
            return new MessageInfo(
                unloggedCount == 1 ? MessageType.log_unlogged_change : MessageType.log_unlogged_changes,
                unloggedCount);
        }

        private AuditLogEntry CreateUnloggedEntry(SrmDocument doc, out bool replace)
        {
            var logEntries = new List<AuditLogEntry>(doc.AuditLog.AuditLogEntries);

            var countEntry = logEntries.LastOrDefault();

            replace = countEntry != null && countEntry.CountEntryType == MessageType.log_unlogged_changes;
            if (!replace)
            {
                countEntry = CreateSimpleEntry(doc, MessageType.log_unlogged_changes, 0);
                countEntry.CountEntryType = MessageType.log_unlogged_changes;
            }
            return countEntry.ChangeUndoRedo(GetUnloggedMessages(int.Parse(countEntry.UndoRedo.Names[0]) + 1))
                .ChangeSummary(GetUnloggedMessages(int.Parse(countEntry.Summary.Names[0]) + 1))
                .ChangeAllInfo(ImmutableList.Singleton(GetUnloggedMessages(
                    int.Parse(countEntry._allInfoNoUndoRedo.First().Names[0]) + AllInfo.Count)));
        }

        /// <summary>
        /// Creates a log entry that- and how many changes were cleared
        /// </summary>
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
                allInfoCount += int.Parse(countEntry._allInfoNoUndoRedo.First().Names[0]);
            }

            undoRedoCount += entries.Count - countEntries.Length;
            allInfoCount += entries.Sum(e => e._allInfoNoUndoRedo.Count()) - countEntries.Length;

            var entry = CreateEmptyEntry(doc);
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
        public static AuditLogEntry CreateEmptyEntry(SrmDocument document)
        {
            return new AuditLogEntry(document, DateTime.Now, string.Empty);
        }

        /// <summary>
        /// Creates a simple entry only containing one message in each category with the given type and names
        /// extra info
        /// </summary>
        public static AuditLogEntry CreateSimpleEntry(SrmDocument document, MessageType type, params object[] args)
        {
            return CreateSingleMessageEntry(document, new MessageInfo(type, args));
        }

        /// <summary>
        /// Creates an entry that depends on whether there are 1 or multiple elements
        /// in a collection.
        /// </summary>
        /// <param name="document">Document change was made to</param>
        /// <param name="singular">Message to show if there's 1 element in the collection. Only element gets passed as argument to the message</param>
        /// <param name="plural">Message to show if there are multiple elements. The count gets passed to the message</param>
        /// <param name="items">Items to consider</param>
        /// <param name="singularArgsFunc">Converts the element to MessageArgs that get passed to the singular message</param>
        /// <param name="pluralArgs">Args to be passed to plural. If null, count is passed as single arg</param>
        public static AuditLogEntry CreateCountChangeEntry<T>(SrmDocument document, MessageType singular,
            MessageType plural, ICollection<T> items, Func<T, MessageArgs> singularArgsFunc, MessageArgs pluralArgs)
        {
            var singularArgs = items.Count == 1 ? singularArgsFunc(items.FirstOrDefault()) : null;
            return CreateCountChangeEntry(document, singular, plural, items.Count, singularArgs,
                pluralArgs);
        }

        /// <summary>
        /// Creates an entry that depends on whether there are 1 or multiple elements
        /// in a collection.
        /// </summary>
        /// <param name="document">Document change was made to</param>
        /// <param name="singular">Message to show if there's 1 element in the collection. Only element gets passed as argument to the message</param>
        /// <param name="plural">Message to show if there are multiple elements. The count gets passed to the message</param>
        /// <param name="items">Items to consider</param>
        /// <param name="count">Number of elements in IEnumerable. If null, all items are enumerated</param>
        /// <param name="singularArgsFunc">Converts the element to MessageArgs that get passed to the singular message</param>
        /// <param name="pluralArgs">Args to be passed to plural. If null, count is passed as single arg</param>
        public static AuditLogEntry CreateCountChangeEntry<T>(SrmDocument document, MessageType singular,
            MessageType plural, IEnumerable<T> items, int? count, Func<T, MessageArgs> singularArgsFunc, MessageArgs pluralArgs)
        {
            if (!count.HasValue)
            {
                var collection = items as ICollection<T> ?? items.ToArray();
                return CreateCountChangeEntry(document, singular, plural, collection, singularArgsFunc, pluralArgs);
            }

            var singularArgs = count.Value == 1 ? singularArgsFunc(items.FirstOrDefault()) : null;
            return CreateCountChangeEntry(document, singular, plural, count.Value, singularArgs, pluralArgs);
        }

        // Overload for common case
        public static AuditLogEntry CreateCountChangeEntry(SrmDocument document, MessageType singular,
            MessageType plural, ICollection<string> items)
        {
            return CreateCountChangeEntry(document, singular, plural, items, MessageArgs.DefaultSingular, null);
        }

        // Overload for common case
        public static AuditLogEntry CreateCountChangeEntry(SrmDocument document, MessageType singular,
            MessageType plural, IEnumerable<string> items, int? count)
        {
            if (!count.HasValue)
            {
                var collection = items as ICollection<string> ?? items.ToArray();
                return CreateCountChangeEntry(document, singular, plural, collection, MessageArgs.DefaultSingular, null);
            }

            return CreateCountChangeEntry(document, singular, plural, items, count, MessageArgs.DefaultSingular, null);
        }

        private static AuditLogEntry CreateCountChangeEntry(SrmDocument document, MessageType singular,
            MessageType plural, int count, MessageArgs singularArgs, MessageArgs pluralArgs)
        {
            switch (count)
            {
                case 1:
                    return CreateSimpleEntry(document, singular, singularArgs.Args);
                default:
                    return CreateSimpleEntry(document, plural,
                        pluralArgs != null ? pluralArgs.Args : MessageArgs.Create(count).Args);
            }
        }

        /// <summary>
        /// Creates a simple entry only containing one message in each category with the given type and names and
        /// extra info
        /// </summary>
        public static AuditLogEntry CreateSingleMessageEntry(SrmDocument document,
            MessageInfo info, string extraInfo = null)
        {
            var result = new AuditLogEntry(document, DateTime.Now, string.Empty, true, extraInfo)
            {
                UndoRedo = info.ToMessage(LogLevel.undo_redo),
                Summary = info.ToMessage(LogLevel.summary),
                AllInfo = new LogMessage[0]//new[] { info.ToMessage(LogLevel.all_info) }
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
        /// <param name="document">Document changes were made to</param>
        /// <param name="tree">Tree that should be logged</param>
        /// <param name="extraInfo">Text that should be displayed when clicking the magnifying glass in the audit log form</param>
        /// <returns></returns>
        public static AuditLogEntry CreateSettingsChangeEntry(SrmDocument document, DiffTree tree, string extraInfo = null)
        {
            if (tree.Root == null)
                return null;

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

        /// <summary>
        /// Creates a log entry indicating that logging was enabled or disabled
        /// </summary>
        public static AuditLogEntry CreateLogEnabledDisabledEntry(SrmDocument document)
        {
            var result = new AuditLogEntry(document, DateTime.Now, string.Empty);

            var type = Settings.Default.AuditLogging ? MessageType.log_enabled : MessageType.log_disabled;

            result.UndoRedo = new LogMessage(LogLevel.undo_redo, type, string.Empty, false);
            result.Summary = new LogMessage(LogLevel.summary, type, string.Empty, false);
            result.AllInfo = new List<LogMessage> { new LogMessage(LogLevel.all_info, type, string.Empty, false) };

            return result;
        }

        public static AuditLogEntry CreateExceptionEntry(SrmDocument doc, LogException ex)
        {
            // ReSharper disable PossibleNullReferenceException
            if (ex.OldUndoRedoMessage == null)
            {
                return CreateSingleMessageEntry(doc,
                    new MessageInfo(MessageType.log_error, ex.InnerException.GetType().Name), ex.InnerException.StackTrace);
            }
            else
            {
                var entry = CreateSingleMessageEntry(doc,
                    new MessageInfo(MessageType.empty_single_arg, ex.OldUndoRedoMessage), ex.InnerException.StackTrace);
                return entry.AppendAllInfo(ImmutableList.Singleton(new MessageInfo(MessageType.log_error_old_msg,
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
        /// <param name="append">see <see cref="Merge(pwiz.Skyline.Model.AuditLog.AuditLogEntry,bool)"/></param>
        /// <returns>A new, merged entry</returns>
        public AuditLogEntry Merge(SrmDocumentPair docPair, AuditLogEntryCreatorList creatorList, bool append = true)
        {
            return creatorList.EntryCreators.Aggregate(this, (e, c) => e.Merge(c.Create(docPair), append));
        }

        /// <summary>
        /// Adds the current entry to the given document
        /// </summary>
        /// <param name="document">Document to add the entry to</param>
        /// <param name="modifyDocument">Function used to modify the document</param>
        public void AddToDocument(SrmDocument document, Action<Func<SrmDocument, SrmDocument>> modifyDocument)
        {
            modifyDocument(d =>
            {
                SrmDocument newDoc;
                if (Settings.Default.AuditLogging || CountEntryType == MessageType.log_cleared)
                {
                    newDoc = d.ChangeAuditLog(
                        ImmutableList.ValueOf(d.AuditLog.AuditLogEntries.Concat(new[] { this })));
                }
                else
                {
                    bool replace;
                    var entry = CreateUnloggedEntry(document, out replace);

                    var oldEntries = d.AuditLog.AuditLogEntries;
                    var newEntries = replace
                        ? oldEntries.ReplaceAt(oldEntries.Count - 1, entry)
                        : oldEntries.Concat(ImmutableList.Singleton(entry));

                    newDoc = d.ChangeAuditLog(ImmutableList.ValueOf(newEntries));
                }

                if (OnAuditLogEntryAdded != null)
                    OnAuditLogEntryAdded(this, new AuditLogEntryAddedEventArgs(this));
                return newDoc;
            });
        }

        // For testing
        public class AuditLogEntryAddedEventArgs : EventArgs
        {
            public AuditLogEntryAddedEventArgs(AuditLogEntry entry)
            {
                Entry = entry;
            }

            public AuditLogEntry Entry { get; private set; }
        }

        public static event EventHandler<AuditLogEntryAddedEventArgs> OnAuditLogEntryAdded;

        public static bool ConvertPathsToFileNames { get; set; }

        /// <summary>
        /// Compares the settings objects of the given documents and creates an entry
        /// for the differences
        /// </summary>
        /// <param name="documentPair">The pair of documents to compare</param>
        /// <returns>A log entry containing the changes</returns>
        public static AuditLogEntry SettingsLogFunction(SrmDocumentPair documentPair)
        {
            var property = RootProperty.Create(typeof(SrmSettings), "Settings"); // Not L10N
            var objInfo = new ObjectInfo<object>(documentPair.OldDoc.Settings, documentPair.NewDoc.Settings,
                documentPair.OldDoc, documentPair.NewDoc, documentPair.OldDoc, documentPair.NewDoc);

            var tree = DiffTree.FromEnumerator(Reflector<SrmSettings>.EnumerateDiffNodes(objInfo, property, false));
            return tree.Root != null ? CreateSettingsChangeEntry(documentPair.OldDoc, tree) : null;
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
                writer.WriteElementString(EL.extra_info, ExtraInfo.EscapeNonPrintableChars());

            writer.WriteElement(EL.message, UndoRedo);
            writer.WriteElement(EL.message, Summary);

            var startIndex = InsertUndoRedoIntoAllInfo ? 1 : 0;
            for (var i = startIndex; i < _allInfo.Count; ++i)
                writer.WriteElement(EL.message, _allInfo[i]);
        }

        public void ReadXml(XmlReader reader)
        {
            LogIndex = Interlocked.Increment(ref _logIndexCounter);
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
            ExtraInfo = reader.IsStartElement(EL.extra_info) ? reader.ReadElementString().UnescapeNonPrintableChars() : string.Empty;

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
            get { return new MessageInfo(MessageType.none); }
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

        protected virtual AuditLogEntry CreateBaseEntry(SrmDocumentPair docPair)
        {
            return MessageInfo.Type == MessageType.none
                ? AuditLogEntry.CreateEmptyEntry(docPair.OldDoc).ChangeAllInfo(new LogMessage[0])
                : AuditLogEntry.CreateSingleMessageEntry(docPair.OldDoc, MessageInfo);
        }

        protected virtual AuditLogEntry CreateEntry(SrmDocumentPair docPair)
        {
            var baseEntry = CreateBaseEntry(docPair);
            var rootProp = RootProperty.Create(typeof(T));

            var objectInfo = new ObjectInfo<object>()
                    .ChangeObjectPair(ObjectPair<object>.Create(null, this))
                    .ChangeRootObjectPair(docPair.ToObjectType());

            var diffTree =
                DiffTree.FromEnumerator(Reflector<T>.EnumerateDiffNodes(docPair.ToObjectType(), rootProp, (T)this), DateTime.Now);
            if (diffTree.Root == null)
                return baseEntry;

            var settingsString = Reflector<T>.ToString(objectInfo.RootObjectPair, diffTree.Root,
                ToStringState.DEFAULT.ChangeFormatWhitespace(true));
            var entry = AuditLogEntry.CreateSettingsChangeEntry(docPair.OldDoc, diffTree, settingsString);
            return baseEntry.Merge(entry);
        }
    }
    
    /// <summary>
    /// An optional interface for forms that have a settings object representing
    /// them
    /// </summary>
    /// <typeparam name="T">Type of the settings object</typeparam>
    public interface IAuditLogModifier<T> where T : AuditLogOperationSettings<T>
    {
        T FormSettings { get; }
    }     
}