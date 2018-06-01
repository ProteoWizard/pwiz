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
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.AuditLog.Databinding
{
    public class AuditLogRow : SkylineObject
    {
        private AuditLogEntry _entry;

        public AuditLogRow(SkylineDataSchema dataSchema, AuditLogEntry entry, string skylineVersion,
            DocumentFormat documentFormat, DateTime timeStamp, string user) : base(dataSchema)
        {
            Assume.IsNotNull(entry);
            _entry = entry;

            SkylineVersion = skylineVersion;
            DocumentFormat = documentFormat.AsDouble();
            TimeStamp = timeStamp.ToString(CultureInfo.CurrentCulture);

            User = user;

            Details = ImmutableList.ValueOf(
                Enumerable.Range(0, entry.AllInfo.Count).Select(i => new AuditLogDetailRow(this, i)));
        }

        public AuditLogEntry GetEntry() { return _entry; }

        public string TimeStamp { get; private set; }

        [Format(Width=512)]
        public string UndoRedoMessage { get { return _entry.UndoRedo.ToString(); } }

        [Format(Width=512)]
        public string SummaryMessage { get { return _entry.Summary.ToString(); } }
            

        public string SkylineVersion { get; private set; }
        public double DocumentFormat { get; private set; }
        public string User { get; private set; }

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
                if (newEntry.AllInfo.Count == 1)
                {
                    var infoCopy = newEntry.AllInfo.ToArray();
                    infoCopy[0] = newEntry.AllInfo[0].ChangeReason(value);
                    newEntry = newEntry.ChangeAllInfo(infoCopy);
                }

                ModifyDocument(EditDescription.SetColumn("Reason", // Not L10N
                        value), d => ChangeEntry(d, newEntry));

                _entry = newEntry;
            }
        }

        public SrmDocument ChangeEntry(SrmDocument document, AuditLogEntry auditLogEntry)
        {
            var copy = new List<AuditLogEntry>(document.AuditLog.AuditLogEntries);
            var index = copy.FindIndex(e => ReferenceEquals(e, _entry)); // This is not found??
            if (index >= 0)
            {
                copy[index] = auditLogEntry;
                return document.ChangeAuditLog(ImmutableList.ValueOf(copy));
            }

            return document;
        }

    }
}