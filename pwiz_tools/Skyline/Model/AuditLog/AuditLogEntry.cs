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
    [XmlRoot(XML_ROOT)]
    public class AuditLogList : Immutable, IXmlSerializable
    {
        public const string XML_ROOT = "audit_log"; // Not L10N
        public const string EXT = ".skyl"; // Not L10N

        public static bool IgnoreTestChecks { get; set; }

        public AuditLogList(AuditLogEntry entries)
        {
            AuditLogEntries = entries;
        }

        public AuditLogList() : this(AuditLogEntry.ROOT)
        {
        }


        public AuditLogEntry AuditLogEntries { get; private set; }

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
            if (!reader.IsStartElement(AuditLogEntry.XML_ROOT))
                return AuditLogEntry.ROOT;

            return reader.DeserializeElement<AuditLogEntry>()
                .ChangeParent(ReadEntries(reader));
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

            foreach (var entry in AuditLogEntries.Enumerate())
            {
                Assume.IsTrue(entry.TimeStamp <= time && entry.LogIndex > logIndex,
                    AuditLogStrings.AuditLogList_Validate_Audit_log_is_corrupted__Audit_log_entry_time_stamps_and_indices_should_be_decreasing);

                time = entry.TimeStamp;
                logIndex = entry.LogIndex;
            }
        }

        public void WriteXml(XmlWriter writer)
        {
            AuditLogEntries.Enumerate().ForEach(writer.WriteElement);
        }

        private enum EL
        {
            document_hash
        }

        public string GetHash()
        {
            // Surprisingly, the XmlTextWriter disposes the stream
            using (var stream = new MemoryStream())
            {
                using (var writer = new XmlTextWriter(stream, Encoding.UTF8) { Formatting = Formatting.Indented })
                {
                    WriteToXmlWriter(writer);

                    stream.Seek(0, SeekOrigin.Begin);

                    // Leave stream open, otherwise XmlTextWriter will try to close it which causes an exception
                    using (var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, true))
                    {
                        return AuditLogEntry.Hash(reader.ReadToEnd());
                    }
                }
            }
        }

        public void WriteToFile(string fileName, string documentHash)
        {
            using (var writer = new XmlTextWriter(fileName, Encoding.UTF8) {Formatting = Formatting.Indented})
            {
                WriteToXmlWriter(writer, documentHash);
            }
        }

        private void WriteToXmlWriter(XmlWriter writer, string documentHash = null)
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("audit_log_root"); // Not L10N
            if (!string.IsNullOrEmpty(documentHash))
                writer.WriteElementString(EL.document_hash, documentHash);
            writer.WriteElement(this);
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        public static AuditLogList ReadFromFile(string fileName, out string documentHash)
        {
            using (var reader = new XmlTextReader(fileName))
            {
                reader.ReadStartElement();
                documentHash = reader.ReadElementString(EL.document_hash.ToString());
                var result = reader.DeserializeElement<AuditLogList>();
                reader.ReadEndElement();
                return result;
            }
        }
    }

    public class DocumentNodeCounts : AuditLogOperationSettings<DocumentNodeCounts>
    {
        public DocumentNodeCounts(SrmDocument doc)
        {
            IsPeptideOnly = doc.DocumentType == SrmDocument.DOCUMENT_TYPE.proteomic;
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
                get { return _propertyName + "_smallmol"; } // Not L10N
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
    public class AuditLogEntry : Immutable, IXmlSerializable
    {
        public const string XML_ROOT = "audit_log_entry"; // Not L10N

        private ImmutableList<LogMessage> _allInfo;
        private Action<AuditLogEntry> _undoAction;
        private static int _logIndexCounter;

        public static AuditLogEntry ROOT = new AuditLogEntry { Count = 0, LogIndex = int.MaxValue };

        public bool IsRoot
        {
            get { return ReferenceEquals(this, ROOT); }
        }

        private AuditLogEntry(SrmDocument document, DateTime timeStamp, string reason, bool insertIntoUndoRedo = false,
            string extraInfo = null) : this()
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
            //InsertUndoRedoIntoAllInfo = insertIntoUndoRedo;
        }

        /// Parent node, topmost node will be <see cref="ROOT" />
        public AuditLogEntry Parent { get; private set; }
        // The number of nodes in this linked list, including this node itself
        public int Count { get; private set; }

        public string SkylineVersion { get; private set; }
        public DocumentFormat FormatVersion { get; private set; }
        public DateTime TimeStamp { get; private set; }
        public string User { get; private set; }
        public string Reason { get; private set; }
        public string ExtraInfo { get; private set; }
        public LogMessage UndoRedo { get; private set; }
        public LogMessage Summary { get; private set; }

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
            get { return _allInfo != null && _allInfo.Count == (InsertUndoRedoIntoAllInfo ? 2 : 1); }
        }

        public int LogIndex { get; private set; }

        public static string Hash(byte[] bytes)
        {
            using (var sha1 = new SHA1CryptoServiceProvider())
            {
                var hash = sha1.ComputeHash(bytes);
                return string.Join(string.Empty, hash.Select(b => b.ToString("X2"))); // Not L10N
            }
        }

        public static string Hash(string s)
        {
            return Hash(Encoding.UTF8.GetBytes(s));
        }

        public AuditLogEntry this[int i]
        {
            get { return Enumerate().ElementAt(i); }
        }

        public IEnumerable<AuditLogEntry> Enumerate()
        {
            if (IsRoot)
                yield break;

            yield return this;
            foreach (var entry in Parent.Enumerate())
                yield return entry;
        }

        #region Property change functions

        public AuditLogEntry ChangeParent(AuditLogEntry parent)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.Parent = parent;
                im.Count = parent.Count + 1;
            });
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
            var countEntry = doc.AuditLog.AuditLogEntries;

            replace = countEntry != null && countEntry.CountEntryType == MessageType.log_unlogged_changes;
            if (!replace)
            {
                countEntry = CreateSimpleEntry(doc, MessageType.log_unlogged_changes, 0)
                    // This entry needs a non-undoredo all info message to work properly
                    .ChangeAllInfo(ImmutableList.Singleton(new MessageInfo(MessageType.log_unlogged_changes, 0)));
                countEntry.CountEntryType = MessageType.log_unlogged_changes;
            }
            return countEntry.ChangeUndoRedo(GetUnloggedMessages(int.Parse(countEntry.UndoRedo.Names[0]) + 1))
                .ChangeSummary(GetUnloggedMessages(int.Parse(countEntry.Summary.Names[0]) + 1))
                .ChangeAllInfo(ImmutableList.Singleton(GetUnloggedMessages(
                    int.Parse(countEntry._allInfoNoUndoRedo.First().Names[0]) + _allInfoNoUndoRedo.Count())));
        }

        public static AuditLogEntry GetAuditLoggingStartExistingDocEntry(SrmDocument doc)
        {
            // Don't want to have these entries in tests (except for the AuditLogSaving test which actually tests this type of entry)
            if (Program.FunctionalTest && !AuditLogList.IgnoreTestChecks)
                return null;

            var defaultDoc = new SrmDocument(SrmSettingsList.GetDefault());
            var docPair = SrmDocumentPair.Create(defaultDoc, doc);

            var changeFromDefaultSettings = SettingsLogFunction(docPair);
            var initialNodeCounts = doc.Children.Count > 0 ? new DocumentNodeCounts(doc).EntryCreator.Create(docPair) : null;

            var entry = CreateSimpleEntry(doc, MessageType.start_log_existing_doc)
                .Merge(initialNodeCounts).Merge(changeFromDefaultSettings);

            if (changeFromDefaultSettings != null || initialNodeCounts != null)
                return entry;

            return null;
        }

        public static AuditLogEntry GetUndocumentedChangeEntry(SrmDocument doc)
        {
            return CreateSimpleEntry(doc, MessageType.undocumented_change);
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
            if (tree?.Root == null)
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

            var diffTree = DiffTree.FromEnumerator(
                Reflector<Targets>.EnumerateDiffNodes(objInfo, property, false,
                    ignoreTransitions
                        ? (Func<DiffNode, bool>) (node => !IsTransitionDiff(node.Property.PropertyType))
                        : null),
                DateTime.Now);

            if (diffTree.Root != null)
            {
                var message = new MessageInfo(action, actionParameters);
                var entry = CreateSettingsChangeEntry(documentPair.OldDoc, diffTree)
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
            var result = new AuditLogEntry(document, DateTime.Now, string.Empty);

            var type = document.Settings.DataSettings.AuditLogging ? MessageType.log_enabled : MessageType.log_disabled;

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
                var startEntry = GetAuditLoggingStartExistingDocEntry(newDoc);
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

            //writer.WriteAttribute(ATTR.insert_undo_redo, InsertUndoRedoIntoAllInfo);

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

            //InsertUndoRedoIntoAllInfo = reader.GetBoolAttribute(ATTR.insert_undo_redo);

            var countType = reader.GetAttribute(ATTR.count_type);
            if (countType == null)
                CountEntryType = null;
            else
                CountEntryType = (MessageType) Enum.Parse(typeof(MessageType), countType);

            reader.ReadStartElement();

            Reason = reader.IsStartElement(EL.reason) ? reader.ReadElementString() : string.Empty;
            ExtraInfo = reader.IsStartElement(EL.extra_info) ? reader.ReadElementString().UnescapeNonPrintableChars() : string.Empty;

            UndoRedo = reader.DeserializeElement<LogMessage>().ChangeLevel(LogLevel.undo_redo);
            Summary = reader.DeserializeElement<LogMessage>().ChangeLevel(LogLevel.summary);

            var list = new List<LogMessage>();
            while (reader.IsStartElement(EL.message))
                list.Add(reader.DeserializeElement<LogMessage>().ChangeLevel(LogLevel.all_info));

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
    public interface IAuditLogModifier<out T> where T : AuditLogOperationSettings<T>
    {
        T FormSettings { get; }
    }     
}