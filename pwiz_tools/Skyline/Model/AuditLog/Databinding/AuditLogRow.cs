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

        public AuditLogRow(SkylineDataSchema dataSchema, AuditLogEntry entry) : base(dataSchema)
        {
            Assume.IsNotNull(entry);
            _entry = entry;
            Details = ImmutableList.ValueOf(
                Enumerable.Range(0, entry.AllInfo.Count).Select(i => new AuditLogDetailRow(this, i)));
        }

        public AuditLogEntry Entry
        {
            get { return _entry; }
        }

        public string TimeStamp { get { return _entry.TimeStamp.ToString(CultureInfo.CurrentCulture); } }

        public class AuditLogRowText
        {
            public AuditLogRowText(string text, string extraText, Action undoAction)
            {
                Text = text;
                ExtraText = extraText;
                UndoAction = undoAction;
            }

            public string Text { get; private set; }
            public string ExtraText { get; private set; }
            public Action UndoAction { get; private set; }

            public override string ToString()
            {
                return Text;
            }
        }

        [DataGridViewColumnType(typeof(AuditLogColumn))]
        [Format(Width = 512)]
        public AuditLogRowText UndoRedoMessage
        {
            get
            {
                return new AuditLogRowText(_entry.UndoRedo.ToString(),
                    LogMessage.LocalizeLogStringProperties(_entry.ExtraText), _entry.UndoAction);
            }
        }

        [DataGridViewColumnType(typeof(AuditLogColumn))]
        [Format(Width=512)]
        public AuditLogRowText SummaryMessage
        {
            get
            {
                return new AuditLogRowText(_entry.Summary.ToString(),
                    LogMessage.LocalizeLogStringProperties(_entry.ExtraText), _entry.UndoAction);
            }
        }

        public string SkylineVersion { get { return _entry.SkylineVersion; } }
        public double DocumentFormat { get { return _entry.FormatVersion.AsDouble(); } }
        public string User { get { return _entry.User; } }

        [OneToMany(ForeignKey = "AuditLogRow")] // Not L10N
        public IList<AuditLogDetailRow> Details { get; private set; }

        public string Reason
        {
            get
            {
                return _entry.Reason;
            }
            set
            {
                var newEntry = _entry.ChangeReason(value);
                ModifyDocument(EditDescription.SetColumn("Reason", // Not L10N
                        value), d => ChangeEntry(d, newEntry));
            }
        }

        public SrmDocument ChangeEntry(SrmDocument document, AuditLogEntry auditLogEntry)
        {
            var copy = new List<AuditLogEntry>(document.AuditLog.AuditLogEntries);
            var index = copy.FindIndex(e => ReferenceEquals(e, _entry));
            if (index >= 0)
            {
                copy[index] = auditLogEntry;
                return document.ChangeAuditLog(ImmutableList.ValueOf(copy));
            }

            return document;
        }
    }
}