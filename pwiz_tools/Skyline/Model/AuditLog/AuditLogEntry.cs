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

        private AuditLogEntry(DocumentFormat formatVersion, DateTime timeStamp, string reason)
        {
            SkylineVersion = Install.Version;
            if (Install.Is64Bit)
                SkylineVersion += " (64-Bit)"; // Not L10N

            FormatVersion = formatVersion;
            TimeStamp = timeStamp;

            using (var identity = WindowsIdentity.GetCurrent())
            {
                // ReSharper disable once PossibleNullReferenceException
                User = identity.Name;
            }

            Reason = reason ?? string.Empty;
        }

        public AuditLogEntry(DocumentFormat formatVersion, DiffTree tree)
            : this(formatVersion, tree.TimeStamp, string.Empty)
        {
            var nodeNamePair = tree.Root.FindFirstMultiChildParent(tree, PropertyName.Root, true, false);
            // Remove "Settings" from property name if possible
            if (nodeNamePair.Name.Parent != PropertyName.Root)
            {
                var name = nodeNamePair.Name;
                while (name.Parent.Parent != PropertyName.Root)
                    name = name.Parent;

                if (name.Parent.Name == "{0:Settings}") // Not L10N
                {
                    PropertyName.Root.SubProperty(name, false);
                    nodeNamePair = new DiffNodeNamePair(nodeNamePair.Node, nodeNamePair.Name, false);
                }
            }

            UndoRedo = nodeNamePair.ToMessage(LogLevel.undo_redo);
            Summary = tree.Root.FindFirstMultiChildParent(tree, PropertyName.Root, false, false).ToMessage(LogLevel.summary);
            AllInfo = tree.Root.FindAllLeafNodes(tree, PropertyName.Root, true)
                .Select(n => n.ToMessage(LogLevel.all_info)).ToArray();
        }

        public static AuditLogEntry MakeLogSettingsChangeEntry(DocumentFormat formatVersion, DateTime timeStamp)
        {
            var result = new AuditLogEntry(formatVersion, timeStamp, string.Empty);

            var type = Settings.Default.AuditLogging ? MessageType.log_enabled : MessageType.log_disabled;

            result.UndoRedo = new LogMessage(LogLevel.undo_redo, type, string.Empty, false);
            result.Summary = new LogMessage(LogLevel.summary, type, string.Empty, false);
            result.AllInfo = new List<LogMessage> { new LogMessage(LogLevel.all_info, type, string.Empty, false) };

            return result;
        }

        public static AuditLogEntry MakeCountEntry(MessageType type, DocumentFormat formatVersion,
            DateTime timeStamp, int undoRedoCount, int allInfoCount)
        {
            if (type != MessageType.log_unlogged_changes && type != MessageType.log_cleared)
                throw new ArgumentException();

            // ReSharper disable once UseObjectOrCollectionInitializer
            var result = new AuditLogEntry(formatVersion, timeStamp, string.Empty);

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
        public LogMessage UndoRedo { get; private set; }
        public LogMessage Summary { get; private set; }
        public IList<LogMessage> AllInfo { get; private set; }

        public MessageType? CountEntryType { get; private set; }

        public AuditLogEntry ChangeReason(string reason)
        {
            return ChangeProp(ImClone(this), im => im.Reason = reason);
        }

        public AuditLogEntry ChangeAllInfo(IList<LogMessage> allInfo)
        {
            return ChangeProp(ImClone(this), im => im.AllInfo = allInfo);
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
            count_type
        }

        private enum EL
        {
            message,
            reason
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

            if (CountEntryType.HasValue)
                writer.WriteAttribute(ATTR.count_type, CountEntryType);

            if (!string.IsNullOrEmpty(Reason))
                writer.WriteElementString(EL.reason, Reason);
            
            writer.WriteElement(EL.message, UndoRedo);
            writer.WriteElement(EL.message, Summary);

            foreach (var info in AllInfo)
            {
                writer.WriteElement(EL.message, info);
            }
        }

        public void ReadXml(XmlReader reader)
        {
            FormatVersion = new DocumentFormat(reader.GetDoubleAttribute(ATTR.format_version));
            var time = DateTime.Parse(reader.GetAttribute(ATTR.time_stamp), CultureInfo.InvariantCulture);
            TimeStamp = DateTime.SpecifyKind(time, DateTimeKind.Utc).ToLocalTime();
            User = reader.GetAttribute(ATTR.user);

            var countType = reader.GetAttribute(ATTR.count_type);
            if (countType == null)
                CountEntryType = null;
            else
                CountEntryType = (MessageType) Enum.Parse(typeof(MessageType), countType);

            reader.ReadStartElement();

            Reason = reader.IsStartElement(EL.reason) ? reader.ReadElementString() : string.Empty;

            UndoRedo = reader.DeserializeElement<LogMessage>();
            Summary = reader.DeserializeElement<LogMessage>();

            AllInfo = new List<LogMessage>();
            while (reader.IsStartElement(EL.message))
            {
                AllInfo.Add(reader.DeserializeElement<LogMessage>());
            }

            reader.ReadEndElement();
        }
        #endregion
    }
}