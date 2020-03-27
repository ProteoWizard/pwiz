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
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.AuditLog.Databinding
{
    public class AuditLogDetailRow : SkylineObject
    {
        private readonly int _detailIndex;

        public AuditLogDetailRow(AuditLogRow row, AuditLogRow.AuditLogRowId id) : base(row.DataSchema)
        {
            AuditLogRow = row;
            Id = id;
            _detailIndex = id.Minor - 1;
        }

        public AuditLogRow AuditLogRow { get; private set; }

        [InvariantDisplayName("AuditLogDetailRowId")]
        public AuditLogRow.AuditLogRowId Id { get; private set; }

        [DataGridViewColumnType(typeof(AuditLogColumn))]
        [Format(Width = 512)]
        public AuditLogRow.AuditLogRowText AllInfoMessage
        {
            get
            {
                string extraText = null;
                Action undoAction = null;

                if (AuditLogRow.Entry.InsertUndoRedoIntoAllInfo && _detailIndex == 0)
                {
                    extraText = LogMessage.ParseLogString(AuditLogRow.Entry.ExtraInfo, LogLevel.all_info, AuditLogRow.Entry.DocumentType);
                    undoAction = AuditLogRow.Entry.UndoAction;
                }

                return new AuditLogRow.AuditLogRowText(AuditLogRow.Entry.AllInfo[_detailIndex].ToString(), extraText,
                    undoAction, AuditLogRow.IsMultipleUndo);
            }
        }

        public string DetailReason
        {
            get
            {
                var entry = AuditLogRow.Entry;
                if (entry.InsertUndoRedoIntoAllInfo && _detailIndex == 0 || entry.HasSingleAllInfoRow)
                    return entry.Reason;

                return AuditLogRow.Entry.AllInfo[_detailIndex].Reason;
            }
            set
            {
                var entry = AuditLogRow.Entry;

                if (entry.InsertUndoRedoIntoAllInfo && _detailIndex == 0 || entry.HasSingleAllInfoRow)
                {
                    AuditLogRow.Reason = value;
                    return;
                }

                var index = _detailIndex;
                // Don't manually insert the special undo redo row, it gets inserted by the AuditLogEntry
                var list = (IEnumerable<DetailLogMessage>)entry.AllInfo;
                if (entry.InsertUndoRedoIntoAllInfo)
                {
                    list = list.Skip(1);
                    --index; // All items shift to lower indices
                }
                    
                var allInfoCopy = list.ToArray();
                allInfoCopy[index] = entry.AllInfo[_detailIndex].ChangeReason(value);
                entry = entry.ChangeAllInfo(allInfoCopy);

                ModifyDocument(EditColumnDescription(nameof(DetailReason), value),
                    doc => AuditLogRow.ChangeEntry(doc, entry));
            }
        }

        public override string ToString()
        {
            return TextUtil.SpaceSeparate(AllInfoMessage.Text, DetailReason);
        }

        public override ElementRef GetElementRef()
        {
            return AuditLogEntryRef.PROTOTYPE.ChangeParent(AuditLogRow.GetElementRef()).ChangeName(Id.Minor.ToString(CultureInfo.InvariantCulture));
        }
    }
}
