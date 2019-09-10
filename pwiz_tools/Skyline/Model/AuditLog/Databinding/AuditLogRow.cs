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
using System.ComponentModel;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.AuditLog.Databinding
{
    public class AuditLogRow : SkylineObject
    {
        private readonly AuditLogEntry _entry;
        private readonly bool _isMultipleUndo;

        public class AuditLogRowId
        {
            public AuditLogRowId(int major, int minor)
            {
                Major = major;
                Minor = minor;
            }

            public override string ToString()
            {
                if (Minor == 0)
                    return Major.ToString();
                else
                    return string.Format(@"{0}.{1}", Major, Minor);
            }

            public int Major { get; private set; }
            public int Minor { get; private set; }
        }


        public AuditLogRow(SkylineDataSchema dataSchema, AuditLogEntry entry, int id) : base(dataSchema)
        {
            Assume.IsNotNull(entry);
            _entry = entry;
            Id = new AuditLogRowId(id, 0);
            Details = ImmutableList.ValueOf(entry.AllInfo.Select((l, i) =>
                new AuditLogDetailRow(this, new AuditLogRowId(id, i + 1))));
            _isMultipleUndo = GetIsMultipleUndo();
        }

        [Browsable(false)]
        public AuditLogEntry Entry
        {
            get { return _entry; }
        }

        [InvariantDisplayName("AuditLogRowId")]
        public AuditLogRowId Id { get; private set; }

        public AuditLogRowTime Time
        {
            get { return new AuditLogRowTime(_entry.TimeStampUTC, _entry.TimeZoneOffset); }
        }

        public class AuditLogRowText
        {
            public AuditLogRowText(string text, string extraInfo, Action undoAction, bool isMultipleUndo)
            {
                Text = text;
                ExtraInfo = extraInfo;
                UndoAction = undoAction;
                IsMultipleUndo = isMultipleUndo;
            }

            public string Text { get; private set; }
            public string ExtraInfo { get; private set; }
            public Action UndoAction { get; private set; }
            public bool IsMultipleUndo { get; private set; }

            public override string ToString()
            {
                return Text;
            }
        }

        private bool GetIsMultipleUndo()
        {
            var foundUndoableEntry = false;
            foreach (var entry in SrmDocument.AuditLog.AuditLogEntries.Enumerate())
            {
                if (entry.LogIndex == _entry.LogIndex)
                    return foundUndoableEntry;

                if (entry.UndoAction != null)
                    foundUndoableEntry = true;
            }
            return false;
        }

        [Browsable(false)]
        public bool IsMultipleUndo
        {
            get { return _isMultipleUndo; }
        }

        [DataGridViewColumnType(typeof(AuditLogColumn))]
        [Format(Width = 512)]
        public AuditLogRowText UndoRedoMessage
        {
            get
            {
                return new AuditLogRowText(_entry.UndoRedo.ToString(),
                    LogMessage.ParseLogString(_entry.ExtraInfo, LogLevel.all_info, _entry.DocumentType), _entry.UndoAction,
                    IsMultipleUndo);
            }
        }

        [DataGridViewColumnType(typeof(AuditLogColumn))]
        [Format(Width = 512)]
        public AuditLogRowText SummaryMessage
        {
            get
            {
                return new AuditLogRowText(_entry.Summary.ToString(),
                    LogMessage.ParseLogString(_entry.ExtraInfo, LogLevel.all_info, _entry.DocumentType), _entry.UndoAction,
                    IsMultipleUndo);
            }
        }

        public string SkylineVersion { get { return _entry.SkylineVersion; } }
        public string User { get { return _entry.User; } }

        [OneToMany(ForeignKey = @"AuditLogRow")]
        public IList<AuditLogDetailRow> Details { get; private set; }

        public string Reason
        {
            get
            {
                return _entry.Reason;
            }
            set
            {
                var newEntry = Entry.ChangeReason(value);
                ModifyDocument(EditColumnDescription(nameof(Reason), value), d => ChangeEntry(d, newEntry),
                    docPair => null);
            }
        }

        public SrmDocument ChangeEntry(SrmDocument document, AuditLogEntry auditLogEntry)
        {
            var entries = new Stack<AuditLogEntry>();
            foreach (var entry in document.AuditLog.AuditLogEntries.Enumerate())
            {
                if (entry.LogIndex == Entry.LogIndex)
                {
                    var newEntry = auditLogEntry.ChangeParent(entry.Parent);
                    foreach (var e in entries)
                        newEntry = e.ChangeParent(newEntry);
                    return document.ChangeAuditLog(newEntry);
                }
                else
                {
                    entries.Push(entry);
                }
            }

            return document;
        }
    }
}
