/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using pwiz.Skyline.Model.AuditLog;

namespace pwiz.Skyline.Model.ElementLocators
{
    /// <summary>
    /// Reference to a particular entry in an audit log.
    /// The "Name" of any AuditLogRef is an integer specifying the distance of the AuditLogEntry from the Root
    /// (which is equal to AuditLogEntry.Count)
    /// </summary>
    public class AuditLogEntryRef : ElementRef
    {
        public static readonly AuditLogEntryRef PROTOTYPE = new AuditLogEntryRef();

        private AuditLogEntryRef() : base(DocumentRef.PROTOTYPE)
        {
        }

        public override string ElementType
        {
            get { return @"AuditLogEntry"; }
        }

        protected override IEnumerable<ElementRef> EnumerateSiblings(SrmDocument document)
        {
            int count = document.AuditLog.AuditLogEntries?.Count ?? 0;
            return Enumerable.Range(1, count).Select(index => ChangeName(index.ToString(CultureInfo.InvariantCulture)));
        }

        public AuditLogEntry FindAuditLogEntry(SrmDocument document)
        {
            int index = int.Parse(Name, CultureInfo.InvariantCulture);
            foreach (var entry in document.AuditLog.AuditLogEntries.Enumerate())
            {
                if (entry.Count == index)
                {
                    return entry;
                }
            }

            return null;
        }

        public static string GetEntryName(AuditLogEntry entry)
        {
            return entry.Count.ToString(CultureInfo.InvariantCulture);
        }
    }

    public class AuditLogDetailRef : ElementRef
    {
        public static readonly AuditLogDetailRef PROTOTYPE = new AuditLogDetailRef();

        private AuditLogDetailRef() : base(AuditLogEntryRef.PROTOTYPE)
        {
        }

        public override string ElementType
        {
            get { return @"AuditLogDetail"; }
        }

        protected override IEnumerable<ElementRef> EnumerateSiblings(SrmDocument document)
        {
            var entry = ((AuditLogEntryRef) Parent).FindAuditLogEntry(document);
            return Enumerable.Range(0, entry.AllInfo.Count)
                .Select(i => ChangeName(i.ToString(CultureInfo.InvariantCulture)));
        }
    }
}
